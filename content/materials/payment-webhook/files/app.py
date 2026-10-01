import hashlib
import hmac
import json
import logging
import os

from flask import Flask, abort, request

import ledger

app = Flask(__name__)
log = logging.getLogger("payments")


def signature_is_valid(body, signature):
    expected = hmac.new(
        os.environ["PROVIDER_WEBHOOK_SECRET"].encode(),
        body,
        hashlib.sha256,
    ).hexdigest()
    return hmac.compare_digest(expected, signature or "")


@app.post("/webhooks/payments")
def payment_webhook():
    body = request.get_data()
    if not signature_is_valid(body, request.headers.get("X-Signature")):
        abort(403)
    event = json.loads(body)
    log.info("payment event: %s", event)
    if event["type"] != "payment.succeeded":
        return "", 204
    payment = event["payment"]
    ledger.credit(
        account_id=int(payment["metadata"]["account_id"]),
        amount=payment["amount"],
        payment_id=payment["id"],
    )
    return "", 204
