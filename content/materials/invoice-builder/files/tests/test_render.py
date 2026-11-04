from datetime import date
from decimal import Decimal

from invoicing.invoice_manager import InvoiceManager
from invoicing.models import Client, Entry, Invoice


def make_invoice(vat_payer):
    client = Client("acme", "Acme LLC", "billing@acme.test", vat_payer)
    entries = [
        Entry("acme", date(2026, 9, 1), "Logo", Decimal("2.5"),
              Decimal("3000")),
        Entry("acme", date(2026, 9, 3), "Icons", Decimal("1"),
              Decimal("2000")),
    ]
    return Invoice("2026-0001", client, date(2026, 10, 1), entries)


def test_text_invoice_adds_vat_for_a_vat_payer():
    text = InvoiceManager("data", "out").render_text(make_invoice(True))
    assert "Subtotal: 9500.00 RUB" in text
    assert "VAT: 1900.00 RUB" in text
    assert "Total: 11400.00 RUB" in text


def test_text_invoice_has_no_vat_otherwise():
    text = InvoiceManager("data", "out").render_text(make_invoice(False))
    assert "Total: 9500.00 RUB" in text
