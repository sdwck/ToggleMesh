import sys
import platform
import urllib.request
import zipfile
import tarfile
import subprocess
from pathlib import Path

CLI_VERSION = "1.0.5"

def get_binary_meta():
    system = platform.system().lower()
    machine = platform.machine().lower()

    if machine in ["x86_64", "amd64"]:
        arch = "amd64"
    elif machine in ["arm64", "aarch64"]:
        arch = "arm64"
    else:
        raise RuntimeError(f"Unsupported architecture: {machine}")

    if system == "windows" and arch == "amd64":
        return {
            "filename": "ToggleMesh.CLI.exe",
            "archive_name": "togglemesh-windows-amd64.zip",
            "is_zip": True
        }
    elif system == "darwin" and arch == "arm64":
        return {
            "filename": "ToggleMesh.CLI",
            "archive_name": "togglemesh-macos-arm64.tar.gz",
            "is_zip": False
        }
    elif system == "linux" and arch == "amd64":
        return {
            "filename": "ToggleMesh.CLI",
            "archive_name": "togglemesh-linux-amd64.tar.gz",
            "is_zip": False
        }

    raise RuntimeError(f"Unsupported OS/Architecture combination: {system}-{arch}")

def extract_archive(archive_path, dest_dir, is_zip):
    if is_zip:
        with zipfile.ZipFile(archive_path, 'r') as zip_ref:
            zip_ref.extractall(dest_dir)
    else:
        with tarfile.open(archive_path, "r:gz") as tar_ref:
            tar_ref.extractall(dest_dir)

def ensure_binary_exists():
    meta = get_binary_meta()
    
    bin_dir = Path.home() / ".togglemesh" / "bin"
    bin_dir.mkdir(parents=True, exist_ok=True)
    
    binary_path = bin_dir / meta["filename"]
    
    if binary_path.exists():
        return binary_path
        
    archive_path = bin_dir / meta["archive_name"]
    download_url = f"https://github.com/sdwck/ToggleMesh/releases/download/cli-v{CLI_VERSION}/{meta['archive_name']}"
    
    print("🔌 [ToggleMesh] Downloading native CLI binary for your OS...")
    try:
        urllib.request.urlretrieve(download_url, archive_path)
        print("📦 [ToggleMesh] Extracting package...")
        
        extract_archive(archive_path, bin_dir, meta["is_zip"])
        archive_path.unlink()
        
        if not binary_path.exists():
            raise RuntimeError(f"Executable {meta['filename']} was not found in the extracted archive.")
            
        if platform.system().lower() != "windows":
            binary_path.chmod(0o755)
            
        print("✅ [ToggleMesh] CLI successfully installed!")
        return binary_path
    except Exception as e:
        print(f"❌ [ToggleMesh] Installation failed: {e}")
        sys.exit(1)

def main():
    try:
        binary_path = ensure_binary_exists()
        
        args = sys.argv[1:]
        
        process = subprocess.run([str(binary_path)] + args)
        sys.exit(process.returncode)
    except Exception as e:
        print(e)
        sys.exit(1)

if __name__ == "__main__":
    main()
