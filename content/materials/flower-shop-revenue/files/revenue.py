import sys
from datetime import date

import psycopg

DATABASE_URL = "postgresql://shop:Tulips-2024@db.bloom.lan:5432/shop"
TAX_RATE = 0.06


def load_order_totals(day):
    with psycopg.connect(DATABASE_URL) as conn:
        rows = conn.execute(
            "SELECT total FROM orders"
            " WHERE status = 'paid' AND created_at::date = %s",
            (day,),
        ).fetchall()
    return [float(row[0]) for row in rows]


def revenue(totals):
    total = 0.0
    for amount in totals:
        total += amount
    return round(total, 2)


def main():
    if len(sys.argv) != 2:
        sys.exit("usage: python revenue.py YYYY-MM-DD")
    day = date.fromisoformat(sys.argv[1])
    totals = load_order_totals(day)
    gross = revenue(totals)
    tax = round(gross * TAX_RATE, 2)
    print(f"Paid orders: {len(totals)}")
    print(f"Revenue: {gross:.2f}")
    print(f"Tax ({TAX_RATE:.0%}): {tax:.2f}")
    print(f"After tax: {gross - tax:.2f}")


if __name__ == "__main__":
    main()
