import os

import psycopg

from competitors import fetch_competitor_prices
from pricing import price_for
from supplier import fetch_supplier_prices


def sync_prices():
    costs = fetch_supplier_prices()
    competitors = fetch_competitor_prices(list(costs))
    updated = 0
    with psycopg.connect(os.environ["SHOP_DATABASE_URL"]) as conn:
        for sku, cost in costs.items():
            try:
                price = price_for(cost, competitors.get(sku))
                conn.execute(
                    "UPDATE products SET price = %s WHERE sku = %s",
                    (price, sku),
                )
                updated += 1
            except Exception:
                continue
    return updated


if __name__ == "__main__":
    print(f"Updated {sync_prices()} products")
