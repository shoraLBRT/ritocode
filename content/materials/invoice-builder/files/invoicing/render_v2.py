from decimal import ROUND_HALF_UP, Decimal
from html import escape

VAT_RATE = Decimal("0.20")
KOPECK = Decimal("0.01")


def render_html_v2(invoice):
    rows = []
    subtotal = Decimal("0")
    for entry in invoice.entries:
        amount = (entry.hours * entry.rate_rub).quantize(
            KOPECK, rounding=ROUND_HALF_UP
        )
        subtotal += amount
        rows.append(
            "<tr>"
            f"<td>{entry.day:%d.%m.%Y}</td>"
            f"<td>{escape(entry.description)}</td>"
            f"<td class='num'>{entry.hours}</td>"
            f"<td class='num'>{amount}</td>"
            "</tr>"
        )
    vat = Decimal("0")
    if invoice.client.vat_payer:
        vat = (subtotal * VAT_RATE).quantize(KOPECK, rounding=ROUND_HALF_UP)
    total = subtotal + vat
    return (
        "<html><head><meta charset='utf-8'>"
        "<link rel='stylesheet' href='invoice.css'></head><body>"
        f"<h1>Invoice {escape(invoice.number)}</h1>"
        f"<p class='client'>{escape(invoice.client.name)}</p>"
        f"<p class='date'>{invoice.issued_on:%d.%m.%Y}</p>"
        "<table>"
        "<tr><th>Date</th><th>Work</th><th>Hours</th><th>Amount</th></tr>"
        + "".join(rows)
        + "</table>"
        f"<p class='subtotal'>Subtotal: {subtotal} RUB</p>"
        f"<p class='vat'>VAT 20%: {vat} RUB</p>"
        f"<p class='total'>Total: {total} RUB</p>"
        "</body></html>"
    )
