from dataclasses import dataclass
from datetime import date, timedelta
from decimal import Decimal

import requests

BILLING_PERIOD = timedelta(days=30)


@dataclass
class Subscription:
    id: int
    customer_id: str
    price_rub: Decimal
    started_on: date
    paid_until: date


def next_billing_date(paid_until):
    return paid_until + BILLING_PERIOD


def charge(subscription, gateway_url, api_key):
    response = requests.post(
        f"{gateway_url}/charges",
        json={
            "customer": subscription.customer_id,
            "amount_rub": str(subscription.price_rub),
            "description": f"Subscription {subscription.id}",
        },
        headers={
            "Authorization": f"Bearer {api_key}",
            "Idempotency-Key": (
                f"subscription-{subscription.id}-{subscription.paid_until}"
            ),
        },
        timeout=10,
        retries=3,
    )
    response.raise_for_status()
    subscription.paid_until = next_billing_date(subscription.paid_until)
    return response.json()["charge_id"]


def renew_due(subscriptions, today, gateway_url, api_key):
    charged = []
    for subscription in subscriptions:
        if subscription.paid_until <= today:
            charged.append(charge(subscription, gateway_url, api_key))
    return charged
