import argparse

from invoicing.invoice_manager import InvoiceManager


def main(argv=None):
    parser = argparse.ArgumentParser(description="Build monthly invoices.")
    parser.add_argument("year", type=int)
    parser.add_argument("month", type=int)
    parser.add_argument("--data", default="data", help="clients, timesheet")
    parser.add_argument("--out", default="out", help="where invoices go")
    parser.add_argument("--send", action="store_true", help="e-mail them")
    args = parser.parse_args(argv)
    manager = InvoiceManager(args.data, args.out)
    invoices = manager.run_month(args.year, args.month, args.send)
    print(f"Built {len(invoices)} invoices")


if __name__ == "__main__":
    main()
