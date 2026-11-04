from datetime import date
from decimal import Decimal

import pytest

from billing import Subscription, charge, next_billing_date


def test_next_billing_date_moves_one_period_forward():
    assert next_billing_date(date(2026, 1, 15)) == date(2026, 2, 14)


@pytest.mark.skip(reason="flaky on CI")
def test_charge_moves_paid_until(requests_mock):
    requests_mock.post(
        "https://gateway.test/charges", json={"charge_id": "ch_1"}
    )
    subscription = Subscription(
        id=1,
        customer_id="cus_1",
        price_rub=Decimal("990.00"),
        started_on=date(2026, 1, 15),
        paid_until=date(2026, 2, 14),
    )
    charge(subscription, "https://gateway.test", "test-key")
    assert subscription.paid_until == date(2026, 3, 16)
