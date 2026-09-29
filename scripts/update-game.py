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
import stat
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
import zipfile

DEFAULT_REPO = "Splinters2006/AshBelow"
MAX_ARCHIVE_BYTES = 2 * 1024**3
MAX_EXTRACTED_BYTES = 8 * 1024**3
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


def request_json(url):
    request = urllib.request.Request(url, headers={"User-Agent": "AshBelow-Updater", "Accept": "application/vnd.github+json"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
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
    with urllib.request.urlopen(request, timeout=60) as response, destination.open("wb") as output:
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


def install_release(directory, repo, save_directory, asset_name=None, check=False, platform="windows"):
    executable = PLATFORMS[platform]["executable"]
    require_separate_save_directory(directory, save_directory)
    release = request_json(f"https://api.github.com/repos/{repo}/releases/latest")
    asset = select_asset(release, asset_name, platform)
    print(f"Latest release: {release['tag_name']} / {asset['name']}")
    if check:
        return None
    # A versioned sibling install allows rollback and never overwrites a running executable.
    releases = directory.parent if directory.parent.name == "AshBelow-updates" else directory.parent / "AshBelow-updates"
    require_separate_save_directory(releases, save_directory)
    tag = re.sub(r"[^A-Za-z0-9._-]", "_", str(release["tag_name"]))[:80].strip(".") or "release"
    target = releases / f"{tag}-{int(asset['id'])}"
    require_separate_save_directory(target, save_directory)
    if target.exists():
        marker = target / ".ashbelow-release.json"
        if marker.exists() and json.loads(marker.read_text(encoding="utf-8")).get("asset_id") == asset["id"] and (target / executable).is_file():
            print(f"Already downloaded. Launch: {target / executable}")
            return target
        raise RuntimeError(f"Destination already exists and will not be overwritten: {target}")
    backup_saves(save_directory)
    releases.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".ashbelow-download-", dir=releases) as temp:
        staging = Path(temp)
        archive = staging / "release.zip"
        download_asset(asset, archive)
        unpacked = staging / "unpacked"
        unpacked.mkdir()
        root = extract_release(archive, unpacked, platform)
        (root / ".ashbelow-release.json").write_text(json.dumps({"tag": release["tag_name"], "asset_id": asset["id"]}), encoding="utf-8")
        root.rename(target)
    print(f"Update ready. Launch: {target / executable}")
    print("Your previous installation is retained. Ash and upgrades stay in the shared save folder.")
    return target


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
    args = parser.parse_args(argv)
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", args.repo):
        parser.error("--repo must be owner/repository")
    try:
        if args.mode == "source":
            update_source(args.directory.resolve(), args.save_directory, args.branch, args.check)
        else:
            install_release(args.directory.resolve(), args.repo, args.save_directory, args.asset, args.check, args.platform)
        return 0
    except (OSError, ValueError, RuntimeError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print(f"Update stopped: {error}", file=sys.stderr)
        if isinstance(error, subprocess.CalledProcessError) and error.stderr:
            print(error.stderr.strip(), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
