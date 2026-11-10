#!/usr/bin/env python3
"""Zip one build of the plugin the way Jellyfin expects (the DLL + meta.json) and print its MD5.

Usage: package.py --dll out/Jellyfin.Plugin.CriticAvg.dll --version 1.0.0.12 --abi 12.0.0.0 --out dist
"""
import argparse, datetime, hashlib, json, os, zipfile

GUID = "96c4c7d6-624e-40a0-86af-108a3c395608"
NAME = "Critic AVG"
OVERVIEW = "Dozens of critics. One clear score."
DESCRIPTION = ("Writes the Critic AVG score, one weighted average of IMDb, TMDB, Trakt, Letterboxd, MUBI, "
               "Rotten Tomatoes, Metacritic and SensaCine, into each film's rating, so you can sort and filter "
               "your library by it. Needs your own Critic AVG server address.")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dll", required=True)
    ap.add_argument("--version", required=True)
    ap.add_argument("--abi", required=True)
    ap.add_argument("--changelog", default="")
    ap.add_argument("--out", required=True)
    a = ap.parse_args()

    os.makedirs(a.out, exist_ok=True)
    meta = {
        "category": "Metadata",
        "changelog": a.changelog,
        "description": DESCRIPTION,
        "guid": GUID,
        "name": NAME,
        "overview": OVERVIEW,
        "owner": "Critic AVG",
        "targetAbi": a.abi,
        "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "version": a.version,
        "status": "Active",
        "autoUpdate": True,
        "assemblies": [os.path.basename(a.dll)],
    }
    zip_path = os.path.join(a.out, f"criticavg_{a.version}.zip")
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(a.dll, os.path.basename(a.dll))
        z.writestr("meta.json", json.dumps(meta, indent=2))
    md5 = hashlib.md5(open(zip_path, "rb").read()).hexdigest()
    print(zip_path, md5)


if __name__ == "__main__":
    main()
