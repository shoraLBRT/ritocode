import csv
import subprocess
import sys
from pathlib import Path

import requests

OUTPUT_DIR = Path("/var/www/shop/static/thumbs")


def download(url, target):
    response = requests.get(url, timeout=30, verify=False)
    response.raise_for_status()
    target.write_bytes(response.content)


def make_thumbnail(source, target):
    subprocess.run(
        f"convert {source} -resize 300x300 {target}",
        shell=True,
        check=True,
    )


def main(csv_path):
    with open(csv_path, newline="", encoding="utf-8") as listing:
        for row in csv.DictReader(listing):
            name = row["image_url"].rsplit("/", 1)[-1]
            original = OUTPUT_DIR / f"original-{name}"
            download(row["image_url"], original)
            make_thumbnail(original, OUTPUT_DIR / name)


if __name__ == "__main__":
    main(sys.argv[1])
