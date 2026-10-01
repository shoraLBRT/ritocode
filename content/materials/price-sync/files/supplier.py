import os
from decimal import Decimal

import requests


def fetch_supplier_prices():
    response = requests.get(
        os.environ["SUPPLIER_PRICES_URL"],
        headers={"Authorization": f"Bearer {os.environ['SUPPLIER_TOKEN']}"},
    )
    response.raise_for_status()
    items = response.json(parse_float=Decimal)["items"]
    return {item["sku"]: Decimal(item["cost"]) for item in items}
