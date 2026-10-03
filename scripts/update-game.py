#!/usr/bin/env python3
"""Update Ash Below source or install a GitHub release without touching player saves."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import ssl
import stat
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import zipfile

DEFAULT_REPO = "Splinters2006/AshBelow"
MAX_ARCHIVE_BYTES = 2 * 1024**3
MAX_EXTRACTED_BYTES = 8 * 1024**3
RELEASE_MARKER = ".ashbelow-release.json"
# Written by a --relaunch update for the game to show when it opens again (then the game deletes it).
UPDATE_RESULT = ".ashbelow-update-result.json"
# Each build ZIP is recognised by its asset name prefix, its launcher and the Unity runtime next to it.
PLATFORMS = {
    "windows": {"label": "Windows", "prefix": "AshBelow-", "executable": "AshBelow.exe", "runtime": "UnityPlayer.dll"},
    "linux": {"label": "Linux", "prefix": "AshBelow-Linux-", "executable": "AshBelow.x86_64", "runtime": "UnityPlayer.so"},
}


def default_platform():
    return "linux" if sys.platform.startswith("linux") else "windows"


def require_separate_save_directory(install_directory, save_directory):
    install = install_directory.resolve()
    saves = save_directory.resolve()
    if install.is_relative_to(saves) or saves.is_relative_to(install):
        raise RuntimeError("Keep the game installation and player save folder separate before updating.")


def default_save_directory():
    if sys.platform == "win32":
        return Path(os.environ.get("USERPROFILE", str(Path.home()))) / "AppData/LocalLow/DefaultCompany/Ash Below"
    if sys.platform == "darwin":
        return Path.home() / "Library/Application Support/DefaultCompany/Ash Below"
    return Path(os.environ.get("XDG_CONFIG_HOME", str(Path.home() / ".config"))) / "unity3d/DefaultCompany/Ash Below"


def backup_saves(save_directory):
    save_directory = save_directory.resolve()
    if not save_directory.exists():
        print("No existing save folder; updates will not create or reset your wallet.")
        return None
    if not save_directory.is_dir():
        raise RuntimeError("Save path must be a directory.")
    backup_root = save_directory.parent / (save_directory.name + "-update-backups")
    backup_root.mkdir(parents=True, exist_ok=True)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S-%f")
    destination = backup_root / stamp
    shutil.copytree(save_directory, destination, symlinks=True)
    print(f"Save backup: {destination}")
    return destination


def prime_windows_certificates(url):
    # Python only trusts root certificates already in the Windows store, but a fresh Windows install downloads most
    # roots on demand the first time Windows itself visits a site. One HEAD request through PowerShell does that.
    script = ("[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; "
              "try { Invoke-WebRequest -UseBasicParsing -Method Head -Uri $env:ASHBELOW_PRIME_URL | Out-Null } catch {}")
    try:
        subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script], env={**os.environ, "ASHBELOW_PRIME_URL": url},
                       capture_output=True, timeout=60, check=False)
    except (OSError, subprocess.TimeoutExpired):
        pass


def open_url(request, timeout):
    """urlopen that, on Windows, lets Windows fetch a missing root certificate and retries once. Verification stays on."""
    try:
        return urllib.request.urlopen(request, timeout=timeout)
    except urllib.error.URLError as error:
        if sys.platform != "win32" or not isinstance(error.reason, ssl.SSLCertVerificationError):
            raise
    print("Windows does not have this site's certificate yet; asking Windows to fetch it...")
    prime_windows_certificates(request.full_url)
    try:
        return urllib.request.urlopen(request, timeout=timeout)
    except urllib.error.URLError as error:
        if not isinstance(error.reason, ssl.SSLCertVerificationError):
            raise
        raise RuntimeError("Windows could not verify GitHub's certificate. Open https://github.com once in Edge or run Windows Update, "
                           "then run the updater again. No installed files were changed.") from error


def request_json(url):
    request = urllib.request.Request(url, headers={"User-Agent": "AshBelow-Updater", "Accept": "application/vnd.github+json"})
    try:
        with open_url(request, timeout=30) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        if error.code == 404:
            raise RuntimeError("No published release found. Publish a build ZIP on GitHub, or use --mode source for a checkout.") from error
        raise RuntimeError(f"GitHub request failed (HTTP {error.code}). No installed files were changed.") from error


def select_asset(release, asset_name=None, platform="windows"):
    assets = [asset for asset in release.get("assets", []) if asset.get("name", "").lower().endswith(".zip")]
    if asset_name:
        assets = [asset for asset in assets if asset["name"] == asset_name]
    else:
        # The Windows prefix also matches other platforms' ZIPs, so pick the most specific prefix for each name.
        def owner(name):
            matches = [key for key, info in PLATFORMS.items() if name.startswith(info["prefix"])]
            return max(matches, key=lambda key: len(PLATFORMS[key]["prefix"]), default=None)
        assets = [asset for asset in assets if owner(asset["name"]) == platform]
    if len(assets) != 1:
        names = ", ".join(asset["name"] for asset in assets) or "none"
        raise RuntimeError(f"Expected one AshBelow {PLATFORMS[platform]['label']} ZIP, found: {names}. Use --asset for an exact asset name.")
    return assets[0]


def download_asset(asset, destination):
    url = asset["browser_download_url"]
    if not url.startswith("https://github.com/"):
        raise RuntimeError("Expected a GitHub HTTPS release download.")
    request = urllib.request.Request(url, headers={"User-Agent": "AshBelow-Updater"})
    total = 0
    digest = hashlib.sha256()
    with open_url(request, timeout=60) as response, destination.open("wb") as output:
        while True:
            chunk = response.read(1024 * 1024)
            if not chunk:
                break
            total += len(chunk)
            if total > MAX_ARCHIVE_BYTES:
                raise RuntimeError("Release archive exceeds the download limit.")
            digest.update(chunk)
            output.write(chunk)
    if asset.get("size") is not None and total != asset["size"]:
        raise RuntimeError("Incomplete release download.")
    expected = asset.get("digest")
    if expected and expected.startswith("sha256:") and digest.hexdigest() != expected.split(":", 1)[1]:
        raise RuntimeError("Release checksum mismatch. The download will not be installed.")


def extract_release(archive_path, destination, platform="windows"):
    info = PLATFORMS[platform]
    with zipfile.ZipFile(archive_path) as archive:
        entries = archive.infolist()
        if len(entries) > 100000 or sum(entry.file_size for entry in entries) > MAX_EXTRACTED_BYTES:
            raise RuntimeError("Release archive exceeds extraction limits.")
        seen = set()
        for entry in entries:
            name = entry.filename.replace("\\", "/")
            relative = PurePosixPath(name)
            key = name.rstrip("/").casefold()
            if relative.is_absolute() or ".." in relative.parts or ":" in name or not relative.parts or key in seen:
                raise RuntimeError("Unsafe or duplicate path in release archive.")
            if any(part.endswith((" ", ".")) or part.split(".")[0].upper() in
                   {"CON", "PRN", "AUX", "NUL", *(f"COM{i}" for i in range(1, 10)), *(f"LPT{i}" for i in range(1, 10))}
                   for part in relative.parts):
                raise RuntimeError("Unsupported Windows filename in release archive.")
            if stat.S_ISLNK(entry.external_attr >> 16):
                raise RuntimeError("Release archive must not contain symbolic links.")
            seen.add(key)
            target = destination.joinpath(*relative.parts)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(entry) as source, target.open("wb") as output:
                    shutil.copyfileobj(source, output)
                if (entry.external_attr >> 16) & 0o111:
                    target.chmod(target.stat().st_mode | 0o755)
    executables = list(destination.rglob(info["executable"]))
    if len(executables) != 1:
        raise RuntimeError(f"The release is not a playable {info['label']} build ({info['executable']} missing or ambiguous).")
    root = executables[0].parent
    if not (root / info["runtime"]).is_file() or not (root / "AshBelow_Data").is_dir():
        raise RuntimeError("The release is missing Unity runtime files.")
    # ZIPs made by Unity's editor do not record Unix permissions, so the launcher is marked runnable here.
    executables[0].chmod(executables[0].stat().st_mode | 0o755)
    if list(root.rglob("progress.json")) or list(root.rglob("progress.json.bak")):
        raise RuntimeError("Release archives must not bundle player saves.")
    return root


def replace_installation(directory, root, previous):
    """Swap the new build's files into the install. The old ones are parked in `previous` and put back on failure."""
    swapped = []
    try:
        for entry in sorted(root.iterdir()):
            target = directory / entry.name
            parked = previous / entry.name if target.exists() or target.is_symlink() else None
            if parked:
                target.rename(parked)
            swapped.append((entry, target, parked))
            entry.rename(target)
    except OSError:
        for entry, target, parked in reversed(swapped):
            if target.exists():
                target.rename(entry)
            if parked:
                parked.rename(target)
        raise


def installed_matches(directory, asset):
    """True when the install already is this release asset. Builds record their own ZIP's name; updates add its id."""
    marker = directory / RELEASE_MARKER
    if not marker.is_file():
        return False
    try:
        installed = json.loads(marker.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return False
    return installed.get("asset_id") == asset["id"] or installed.get("asset_name") == asset["name"]


def process_running(pid):
    if sys.platform == "win32":
        import ctypes
        kernel32 = ctypes.windll.kernel32
        handle = kernel32.OpenProcess(0x00100000, False, pid)  # SYNCHRONIZE
        if not handle:
            return False
        try:
            return kernel32.WaitForSingleObject(handle, 0) == 0x102  # WAIT_TIMEOUT: still running
        finally:
            kernel32.CloseHandle(handle)
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def wait_for_exit(pid, timeout=60.0):
    """Waits for the game that started this update to close, so its files can be replaced."""
    deadline = time.monotonic() + timeout
    while process_running(pid):
        if time.monotonic() > deadline:
            raise RuntimeError("Ash Below did not close, so it could not be updated. Close it and try again.")
        time.sleep(0.25)


def relaunch(directory, platform):
    """Opens the game again once the updater is done, detached so it outlives this process."""
    executable = directory / PLATFORMS[platform]["executable"]
    if not executable.is_file():
        return
    options = {"cwd": str(directory), "stdin": subprocess.DEVNULL, "stdout": subprocess.DEVNULL, "stderr": subprocess.DEVNULL}
    if sys.platform == "win32":
        options["creationflags"] = 0x00000008 | 0x00000200  # DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP
    else:
        options["start_new_session"] = True
    try:
        subprocess.Popen([str(executable)], **options)
    except OSError as error:
        print(f"Could not reopen the game: {error}", file=sys.stderr)


def write_result(directory, ok, message):
    try:
        (directory / UPDATE_RESULT).write_text(json.dumps({"ok": ok, "message": message}), encoding="utf-8")
    except OSError:
        pass


def install_release(directory, repo, save_directory, asset_name=None, check=False, platform="windows"):
    executable = PLATFORMS[platform]["executable"]
    require_separate_save_directory(directory, save_directory)
    # Only an existing install is replaced, so the build is never unpacked into some unrelated folder.
    if not (directory / executable).is_file():
        raise RuntimeError(f"No Ash Below installation found in {directory} ({executable} is missing). Run the updater from the game folder.")
    release = request_json(f"https://api.github.com/repos/{repo}/releases/latest")
    asset = select_asset(release, asset_name, platform)
    print(f"Latest release: {release['tag_name']} / {asset['name']}")
    if check:
        return None
    if installed_matches(directory, asset):
        print(f"Already up to date. Launch: {directory / executable}")
        return directory
    # A running game keeps its executable locked against writing on both Windows and Linux.
    try:
        with (directory / executable).open("r+b"):
            pass
    except OSError as error:
        raise RuntimeError("Close Ash Below before updating; its files are in use or cannot be written.") from error
    backup_saves(save_directory)
    # Staging inside the install keeps every move on one filesystem; files the new build does not ship are left alone.
    with tempfile.TemporaryDirectory(prefix=".ashbelow-update-", dir=directory, ignore_cleanup_errors=True) as temp:
        staging = Path(temp)
        archive = staging / "release.zip"
        download_asset(asset, archive)
        unpacked = staging / "unpacked"
        unpacked.mkdir()
        root = extract_release(archive, unpacked, platform)
        (root / RELEASE_MARKER).write_text(json.dumps({"tag": release["tag_name"], "asset_id": asset["id"], "asset_name": asset["name"]}), encoding="utf-8")
        previous = staging / "previous"
        previous.mkdir()
        replace_installation(directory, root, previous)
    print(f"Updated to {release['tag_name']}. Launch: {directory / executable}")
    print("Ash and upgrades stay in the shared save folder.")
    return directory


def git(directory, *args):
    return subprocess.run(["git", "-C", str(directory), *args], check=True, text=True, capture_output=True).stdout.strip()


def update_source(directory, save_directory, branch="main", check=False):
    require_separate_save_directory(directory, save_directory)
    if not (directory / ".git").exists() or not (directory / "Assets").is_dir():
        raise RuntimeError("Source mode requires a Unity Git checkout. It does not update a built game.")
    if git(directory, "status", "--porcelain", "--untracked-files=normal"):
        raise RuntimeError("Commit or stash your local changes before updating. Nothing was reset or discarded.")
    if git(directory, "branch", "--show-current") != branch:
        raise RuntimeError(f"Switch to {branch} before updating source.")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._/-]*", branch) or ".." in branch:
        raise RuntimeError("Invalid branch name.")
    git(directory, "fetch", "origin", branch)
    git(directory, "merge-base", "--is-ancestor", "HEAD", "FETCH_HEAD")
    print(f"Current: {git(directory, 'rev-parse', '--short', 'HEAD')}; available: {git(directory, 'rev-parse', '--short', 'FETCH_HEAD')}")
    if check:
        return
    backup_saves(save_directory)
    git(directory, "merge", "--ff-only", "FETCH_HEAD")
    print("Source updated. Open the project in Unity to import changes and build the game.")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("source", "release"), default="release")
    parser.add_argument("--directory", type=Path, default=Path(__file__).resolve().parent.parent if Path(__file__).resolve().parent.name == "scripts" else Path(__file__).resolve().parent)
    parser.add_argument("--repo", default=DEFAULT_REPO)
    parser.add_argument("--branch", default="main")
    parser.add_argument("--platform", choices=tuple(PLATFORMS), default=default_platform(), help="Which build to install in release mode")
    parser.add_argument("--asset", help="Exact ZIP asset name, if the release contains multiple ZIPs for this platform")
    parser.add_argument("--save-directory", type=Path, default=default_save_directory())
    parser.add_argument("--check", action="store_true", help="Check for updates without installing or backing up saves")
    # Used by the game's own "Check for updates" button: wait for it to close, update, then open it again.
    parser.add_argument("--wait-pid", type=int, help="Wait for this process (the game) to exit before updating")
    parser.add_argument("--relaunch", action="store_true", help="Release mode: reopen the game afterwards and leave it the result")
    parser.add_argument("--log", type=Path, help="Write all output to this file instead of the console")
    args = parser.parse_args(argv)
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", args.repo):
        parser.error("--repo must be owner/repository")
    if args.log:
        log = args.log.open("w", encoding="utf-8", buffering=1)
        sys.stdout = sys.stderr = log
    directory = args.directory.resolve()
    relaunching = args.relaunch and args.mode == "release" and not args.check
    try:
        if args.wait_pid:
            wait_for_exit(args.wait_pid)
        if args.mode == "source":
            update_source(directory, args.save_directory, args.branch, args.check)
        else:
            install_release(directory, args.repo, args.save_directory, args.asset, args.check, args.platform)
        if relaunching:
            tag = None
            try:
                tag = json.loads((directory / RELEASE_MARKER).read_text(encoding="utf-8")).get("tag")
            except (OSError, ValueError):
                pass
            write_result(directory, True, f"Updated to {tag}." if tag else "Updated to the latest version.")
        return 0
    except (OSError, ValueError, RuntimeError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print(f"Update stopped: {error}", file=sys.stderr)
        if isinstance(error, subprocess.CalledProcessError) and error.stderr:
            print(error.stderr.strip(), file=sys.stderr)
        if relaunching:
            write_result(directory, False, f"Update stopped: {error}")
        return 1
    finally:
        # Updated or not, the player gets their game back.
        if relaunching:
            relaunch(directory, args.platform)


if __name__ == "__main__":
    raise SystemExit(main())
