import argparse
import csv
import sys


def normalize_email(raw):
    return (raw or "").strip().lower()


def dedupe(rows):
    seen = set()
    for row in rows:
        email = normalize_email(row.get("email"))
        if email and email in seen:
            continue
        if email:
            seen.add(email)
        yield {**row, "email": email}


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Remove contacts that repeat an e-mail address."
    )
    parser.add_argument("source", help="CSV file with an 'email' column")
    parser.add_argument("target", help="CSV file to write the result to")
    args = parser.parse_args(argv)

    with open(args.source, newline="", encoding="utf-8") as src:
        reader = csv.DictReader(src)
        if not reader.fieldnames or "email" not in reader.fieldnames:
            sys.exit(f"{args.source}: there is no 'email' column")
        with open(args.target, "w", newline="", encoding="utf-8") as dst:
            writer = csv.DictWriter(dst, fieldnames=reader.fieldnames)
            writer.writeheader()
            kept = 0
            for row in dedupe(reader):
                writer.writerow(row)
                kept += 1
    print(f"Kept {kept} contacts")


if __name__ == "__main__":
    main()
