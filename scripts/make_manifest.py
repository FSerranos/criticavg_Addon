#!/usr/bin/env python3
"""Build the Jellyfin repository manifest (the address you paste into Dashboard → Plugins → Repositories)
from the packaged zips.

Usage: make_manifest.py --repo owner/name --tag v1.0.0 --dist dist --out manifest.json
"""
import argparse, glob, hashlib, json, os, zipfile


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--tag", required=True)
    ap.add_argument("--dist", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()

    versions, meta = [], None
    for zp in sorted(glob.glob(os.path.join(a.dist, "*.zip"))):
        with zipfile.ZipFile(zp) as z:
            meta = json.loads(z.read("meta.json"))
        versions.append({
            "version": meta["version"],
            "changelog": meta.get("changelog", ""),
            "targetAbi": meta["targetAbi"],
            "sourceUrl": f"https://github.com/{a.repo}/releases/download/{a.tag}/{os.path.basename(zp)}",
            "checksum": hashlib.md5(open(zp, "rb").read()).hexdigest(),
            "timestamp": meta["timestamp"],
        })
    if not meta:
        raise SystemExit("no zips found in " + a.dist)

    # Newest Jellyfin first; Jellyfin installs the newest version whose targetAbi it supports.
    versions.sort(key=lambda v: [int(x) for x in v["targetAbi"].split(".")], reverse=True)
    manifest = [{
        "guid": meta["guid"],
        "name": meta["name"],
        "description": meta["description"],
        "overview": meta["overview"],
        "owner": meta["owner"],
        "category": meta["category"],
        "imageUrl": f"https://raw.githubusercontent.com/{a.repo}/main/assets/criticavg.jpg",
        "versions": versions,
    }]
    with open(a.out, "w") as f:
        json.dump(manifest, f, indent=2)
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
