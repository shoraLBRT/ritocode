import os
from decimal import Decimal

import psycopg


def credit(account_id, amount, payment_id):
    amount = Decimal(amount)
    with psycopg.connect(
        os.environ["DATABASE_URL"], connect_timeout=5
    ) as conn:
        conn.execute(
            "UPDATE accounts SET balance_rub = balance_rub + %s"
            " WHERE id = %s",
            (amount, account_id),
        )
        conn.execute(
            "INSERT INTO payments"
            " (provider_payment_id, account_id, amount_rub)"
            " VALUES (%s, %s, %s)",
            (payment_id, account_id, amount),
        )
