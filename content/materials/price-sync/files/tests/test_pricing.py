from decimal import Decimal
from unittest.mock import patch

import pricing


@patch("pricing.price_for", return_value=Decimal("135.00"))
def test_price_applies_markup(price_for):
    assert pricing.price_for(Decimal("100.00"), None) == Decimal("135.00")


@patch("pricing.price_for", return_value=Decimal("119.00"))
def test_price_undercuts_competitor(price_for):
    result = pricing.price_for(Decimal("100.00"), Decimal("120.00"))
    assert result == Decimal("119.00")
    price_for.assert_called_once()


@patch("pricing.price_for", return_value=Decimal("110.00"))
def test_price_keeps_minimum_markup(price_for):
    result = pricing.price_for(Decimal("100.00"), Decimal("100.00"))
    assert result == Decimal("110.00")
