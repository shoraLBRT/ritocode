from decimal import Decimal

KNOWN_PRICES = {
    "DOG-FOOD-ADULT-3KG": Decimal("1890.00"),
    "DOG-FOOD-PUPPY-3KG": Decimal("2140.00"),
    "CAT-FOOD-ADULT-2KG": Decimal("1270.00"),
    "CAT-LITTER-10L": Decimal("690.00"),
    "BIRD-SEED-1KG": Decimal("340.00"),
}


def fetch_competitor_prices(skus):
    return {sku: KNOWN_PRICES.get(sku, Decimal("990.00")) for sku in skus}
