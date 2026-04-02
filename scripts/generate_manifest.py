#!/usr/bin/env python3
"""Generate a v2 file manifest for the Orlo game client build.

Walks the build directory, computes SHA256 + size for every file,
and writes a JSON manifest used by the launcher for incremental updates.
"""

import argparse
import hashlib
import json
import os
import sys


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            chunk = f.read(65536)
            if not chunk:
                break
            h.update(chunk)
    return h.hexdigest()


def generate_manifest(build_dir: str, version: str) -> dict:
    files = {}
    total_size = 0
    total_files = 0

    for root, _dirs, filenames in os.walk(build_dir):
        for name in filenames:
            full_path = os.path.join(root, name)
            rel_path = os.path.relpath(full_path, build_dir).replace("\\", "/")

            size = os.path.getsize(full_path)
            sha = sha256_file(full_path)

            files[rel_path] = {"sha256": sha, "size": size}
            total_size += size
            total_files += 1

    return {
        "version": version,
        "manifest_version": 2,
        "total_size": total_size,
        "total_files": total_files,
        "files": files,
    }


def main():
    parser = argparse.ArgumentParser(description="Generate Orlo client file manifest")
    parser.add_argument("--build-dir", required=True, help="Path to build output directory")
    parser.add_argument("--version", required=True, help="Version string")
    parser.add_argument("--output", required=True, help="Output manifest JSON path")
    args = parser.parse_args()

    if not os.path.isdir(args.build_dir):
        print(f"Error: build directory '{args.build_dir}' does not exist", file=sys.stderr)
        sys.exit(1)

    manifest = generate_manifest(args.build_dir, args.version)

    with open(args.output, "w") as f:
        json.dump(manifest, f, indent=2)

    print(f"Manifest: {manifest['total_files']} files, {manifest['total_size']} bytes")


if __name__ == "__main__":
    main()
