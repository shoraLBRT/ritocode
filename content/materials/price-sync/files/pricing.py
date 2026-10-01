from decimal import ROUND_HALF_UP, Decimal

MARKUP = Decimal("1.35")
MIN_MARKUP = Decimal("1.10")
UNDERCUT = Decimal("1.00")
KOPECK = Decimal("0.01")


def price_for(cost, competitor_price):
    price = cost * MARKUP
    if competitor_price is not None and competitor_price < price:
        price = max(competitor_price - UNDERCUT, cost * MIN_MARKUP)
    return price.quantize(KOPECK, rounding=ROUND_HALF_UP)
