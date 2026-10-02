import hashlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import unittest
from unittest import mock
import zipfile

spec = importlib.util.spec_from_file_location("updater", Path(__file__).parents[1] / "scripts/update-game.py")
updater = importlib.util.module_from_spec(spec)
spec.loader.exec_module(updater)


class UpdaterTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.install = self.root / "old-game"
        self.install.mkdir()
        (self.install / "AshBelow.exe").write_bytes(b"old executable")
        self.saves = self.root / "persistent"
        self.saves.mkdir()
        self.progress = self.saves / "progress.json"
        self.progress.write_text('{"version":1,"ash":123,"upgrades":[{"id":"health","rank":2}]}')
        (self.saves / "progress.json.bak").write_text("previous save")
        self.original = self.progress.read_bytes()
        self.asset = {"id": 42, "name": "AshBelow-test.zip", "browser_download_url": "https://github.com/example/repo/releases/download/v1/game.zip"}
        self.release = {"tag_name": "v1.0", "assets": [self.asset]}

    def archive(self, name="good.zip", extra=None):
        path = self.root / name
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("AshBelow-build/AshBelow.exe", b"new executable")
            archive.writestr("AshBelow-build/UnityPlayer.dll", b"runtime")
            archive.writestr("AshBelow-build/AshBelow_Data/data", b"data")
            if extra:
                for filename, content in extra.items():
                    archive.writestr(filename, content)
        return path

    def test_release_replaces_install_in_place_and_preserves_wallet_and_upgrades(self):
        archive = self.archive()
        (self.install / "AshBelow_Data").mkdir()
        (self.install / "AshBelow_Data/stale").write_bytes(b"old data")
        (self.install / "notes.txt").write_text("mine")
        with mock.patch.object(updater, "request_json", return_value=self.release), mock.patch.object(
                updater, "download_asset", side_effect=lambda asset, dest: shutil.copyfile(archive, dest)) as download:
            self.assertEqual(updater.install_release(self.install, "example/repo", self.saves), self.install)
            self.assertEqual((self.install / "AshBelow.exe").read_bytes(), b"new executable")
            self.assertEqual((self.install / "AshBelow_Data/data").read_bytes(), b"data")
            self.assertFalse((self.install / "AshBelow_Data/stale").exists())
            # Files the build does not ship are left alone, and no staging or sibling folders remain.
            self.assertEqual((self.install / "notes.txt").read_text(), "mine")
            self.assertEqual(sorted(path.name for path in self.install.iterdir()),
                             [".ashbelow-release.json", "AshBelow.exe", "AshBelow_Data", "UnityPlayer.dll", "notes.txt"])
            self.assertEqual(sorted(path.name for path in self.root.iterdir()),
                             ["good.zip", "old-game", "persistent", "persistent-update-backups"])
            self.assertEqual(self.progress.read_bytes(), self.original)
            backups = list((self.root / "persistent-update-backups").glob("*/progress.json"))
            self.assertEqual(len(backups), 1)
            self.assertEqual(backups[0].read_bytes(), self.original)
            # Updating again when already on the latest release downloads nothing.
            self.assertEqual(updater.install_release(self.install, "example/repo", self.saves), self.install)
            download.assert_called_once()

    def test_failed_replacement_restores_previous_install(self):
        archive = self.archive()
        (self.install / "AshBelow_Data").mkdir()
        (self.install / "AshBelow_Data/stale").write_bytes(b"old data")
        real_rename = Path.rename
        calls = []

        def flaky_rename(path, target):
            calls.append(path)
            if len(calls) == 4:
                raise OSError("locked")
            return real_rename(path, target)
        with mock.patch.object(updater, "request_json", return_value=self.release), mock.patch.object(
                updater, "download_asset", side_effect=lambda asset, dest: shutil.copyfile(archive, dest)):
            with mock.patch.object(Path, "rename", flaky_rename), self.assertRaises(OSError):
                updater.install_release(self.install, "example/repo", self.saves)
        self.assertEqual((self.install / "AshBelow.exe").read_bytes(), b"old executable")
        self.assertEqual((self.install / "AshBelow_Data/stale").read_bytes(), b"old data")
        self.assertEqual(sorted(path.name for path in self.install.iterdir()), ["AshBelow.exe", "AshBelow_Data"])

    def test_refuses_folder_without_an_installation(self):
        empty = self.root / "not-a-game"
        empty.mkdir()
        with mock.patch.object(updater, "request_json") as request:
            with self.assertRaises(RuntimeError):
                updater.install_release(empty, "example/repo", self.saves)
            request.assert_not_called()
        self.assertEqual(list(empty.iterdir()), [])

    def test_check_does_not_download_or_touch_saves(self):
        with mock.patch.object(updater, "request_json", return_value=self.release), mock.patch.object(updater, "download_asset") as download:
            updater.install_release(self.install, "example/repo", self.saves, check=True)
            download.assert_not_called()
        self.assertFalse((self.root / "persistent-update-backups").exists())
        self.assertEqual(self.progress.read_bytes(), self.original)

    def test_rejects_install_and_save_directory_overlap_before_writing(self):
        for install, saves in ((self.saves / "game", self.saves), (self.install, self.install / "saves"),
                               (self.saves, self.saves)):
            with self.subTest(install=install, saves=saves), mock.patch.object(
                    updater, "request_json", return_value=self.release), mock.patch.object(updater, "backup_saves") as backup:
                with self.assertRaises(RuntimeError):
                    updater.install_release(install, "example/repo", saves)
                backup.assert_not_called()
        self.assertEqual(self.progress.read_bytes(), self.original)

    def test_source_update_rejects_checkout_within_save_folder(self):
        with mock.patch.object(updater, "git") as git:
            with self.assertRaises(RuntimeError):
                updater.update_source(self.saves / "checkout", self.saves)
            git.assert_not_called()
        self.assertEqual(self.progress.read_bytes(), self.original)

    def test_failed_download_preserves_previous_install_and_saves(self):
        with mock.patch.object(updater, "request_json", return_value=self.release), mock.patch.object(
                updater, "download_asset", side_effect=OSError("interrupted")):
            with self.assertRaises(OSError):
                updater.install_release(self.install, "example/repo", self.saves)
        self.assertEqual(self.progress.read_bytes(), self.original)
        self.assertEqual([path.name for path in self.install.iterdir()], ["AshBelow.exe"])
        self.assertEqual((self.install / "AshBelow.exe").read_bytes(), b"old executable")

    def test_rejects_traversal_save_files_and_incomplete_builds(self):
        for i, extra in enumerate(({"../escape": "bad"}, {"AshBelow-build/progress.json": "bad"}, {"C:/escape": "bad"})):
            with self.subTest(extra=extra):
                with self.assertRaises(RuntimeError):
                    updater.extract_release(self.archive(f"bad{i}.zip", extra), self.root / f"unpack{i}")
        source = self.root / "source-only.zip"
        with zipfile.ZipFile(source, "w") as archive:
            archive.writestr("Assets/test.cs", "not a build")
        with self.assertRaises(RuntimeError):
            updater.extract_release(source, self.root / "source-unpack")
        self.assertFalse((self.root / "escape").exists())

    def test_rejects_symlinks(self):
        archive = self.root / "symlink.zip"
        with zipfile.ZipFile(archive, "w") as output:
            entry = zipfile.ZipInfo("link")
            entry.create_system = 3
            entry.external_attr = (stat.S_IFLNK | 0o777) << 16
            output.writestr(entry, "../../persistent")
        with self.assertRaises(RuntimeError):
            updater.extract_release(archive, self.root / "symlink-unpack")

    def test_checks_checksum_and_download_length(self):
        payload = b"archive bytes"
        self.asset.update(size=len(payload), digest="sha256:" + hashlib.sha256(payload).hexdigest())
        with mock.patch.object(updater.urllib.request, "urlopen", return_value=io.BytesIO(payload)):
            updater.download_asset(self.asset, self.root / "download")
        self.asset["digest"] = "sha256:" + "0" * 64
        with mock.patch.object(updater.urllib.request, "urlopen", return_value=io.BytesIO(payload)):
            with self.assertRaises(RuntimeError):
                updater.download_asset(self.asset, self.root / "bad-download")

    def test_requires_unambiguous_release_asset(self):
        self.assertEqual(updater.select_asset(self.release), self.asset)
        self.release["assets"].append(dict(self.asset, id=43, name="AshBelow-other.zip"))
        with self.assertRaises(RuntimeError):
            updater.select_asset(self.release)
        self.assertEqual(updater.select_asset(self.release, "AshBelow-test.zip"), self.asset)

    def test_linux_release_installs_runnable_game_and_ignores_windows_zip(self):
        linux_asset = dict(self.asset, id=77, name="AshBelow-Linux-test.zip")
        self.release["assets"].append(linux_asset)
        self.assertEqual(updater.select_asset(self.release, platform="linux"), linux_asset)
        self.assertEqual(updater.select_asset(self.release, platform="windows"), self.asset)
        archive = self.root / "linux.zip"
        with zipfile.ZipFile(archive, "w") as output:
            output.writestr("AshBelow-Linux-build/AshBelow.x86_64", b"new linux executable")
            output.writestr("AshBelow-Linux-build/UnityPlayer.so", b"runtime")
            output.writestr("AshBelow-Linux-build/AshBelow_Data/data", b"data")
        (self.install / "AshBelow.x86_64").write_bytes(b"old linux executable")
        with mock.patch.object(updater, "request_json", return_value=self.release), mock.patch.object(
                updater, "download_asset", side_effect=lambda asset, dest: shutil.copyfile(archive, dest)) as download:
            new = updater.install_release(self.install, "example/repo", self.saves, platform="linux")
            self.assertEqual(download.call_args.args[0], linux_asset)
            game = new / "AshBelow.x86_64"
            self.assertEqual(game.read_bytes(), b"new linux executable")
            self.assertTrue(game.stat().st_mode & stat.S_IXUSR)
            self.assertEqual(updater.install_release(new, "example/repo", self.saves, platform="linux"), new)
        self.assertEqual(self.progress.read_bytes(), self.original)
        # A Windows build is not accepted as a Linux one.
        with self.assertRaises(RuntimeError):
            updater.extract_release(self.archive(), self.root / "wrong-platform", "linux")

    def test_git_fast_forward_and_dirty_checkout_protection(self):
        def run(*args):
            return subprocess.run(["git", *map(str, args)], check=True, capture_output=True, text=True)
        origin, work, checkout = [self.root / name for name in ("origin.git", "author", "checkout")]
        run("init", "--bare", origin)
        run("init", "-b", "main", work)
        run("-C", work, "config", "user.email", "test@example.invalid")
        run("-C", work, "config", "user.name", "Updater Test")
        (work / "Assets").mkdir()
        (work / "Assets/version.txt").write_text("one")
        run("-C", work, "add", ".")
        run("-C", work, "commit", "-m", "initial")
        run("-C", work, "remote", "add", "origin", origin)
        run("-C", work, "push", "origin", "main")
        run("clone", "--branch", "main", origin, checkout)
        (work / "Assets/version.txt").write_text("two")
        run("-C", work, "commit", "-am", "update")
        run("-C", work, "push", "origin", "main")
        updater.update_source(checkout, self.saves)
        self.assertEqual((checkout / "Assets/version.txt").read_text(), "two")
        self.assertEqual(self.progress.read_bytes(), self.original)
        (checkout / "Assets/version.txt").write_text("local changes")
        with self.assertRaises(RuntimeError):
            updater.update_source(checkout, self.saves)
        self.assertEqual((checkout / "Assets/version.txt").read_text(), "local changes")


if __name__ == "__main__":
    unittest.main()
