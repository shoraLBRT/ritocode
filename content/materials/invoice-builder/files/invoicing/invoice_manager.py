import csv
import json
import os
import shutil
import smtplib
from datetime import date
from decimal import ROUND_HALF_UP, Decimal
from email.message import EmailMessage
from html import escape
from pathlib import Path

from invoicing.models import Client, Entry, Invoice
from invoicing.render_v2 import render_html_v2

VAT_RATE = Decimal("0.20")
KOPECK = Decimal("0.01")


class InvoiceManager:
    def __init__(self, data_dir, out_dir):
        self.data_dir = Path(data_dir)
        self.out_dir = Path(out_dir)
        self.registry_path = self.data_dir / "registry.json"
        self.clients = {}
        self.entries = []
        self.registry = {"last_number": {}, "sent": []}

    def load_clients(self):
        path = self.data_dir / "clients.csv"
        with open(path, newline="", encoding="utf-8") as source:
            for row in csv.DictReader(source):
                vat_payer = row["vat_payer"].strip().lower()
                if vat_payer not in ("yes", "no"):
                    raise ValueError(f"{row['code']}: vat_payer is yes or no")
                self.clients[row["code"]] = Client(
                    code=row["code"],
                    name=row["name"],
                    email=row["email"].strip().lower(),
                    vat_payer=vat_payer == "yes",
                )

    def load_entries(self):
        path = self.data_dir / "timesheet.csv"
        with open(path, newline="", encoding="utf-8") as source:
            for row in csv.DictReader(source):
                entry = Entry(
                    client_code=row["client"],
                    day=date.fromisoformat(row["day"]),
                    description=row["description"],
                    hours=Decimal(row["hours"]),
                    rate_rub=Decimal(row["rate_rub"]),
                )
                if entry.hours <= 0 or entry.rate_rub <= 0:
                    raise ValueError(f"{row['day']}: hours and rate > 0")
                self.entries.append(entry)

    def load_registry(self):
        if self.registry_path.exists():
            self.registry = json.loads(
                self.registry_path.read_text(encoding="utf-8")
            )

    def save_registry(self):
        self.registry_path.write_text(
            json.dumps(self.registry, indent=2), encoding="utf-8"
        )

    # Updated: numbering now restarts every year as requested
    def next_number(self, year):
        last = self.registry["last_number"].get(str(year), 0) + 1
        self.registry["last_number"][str(year)] = last
        return f"{year}-{last:04d}"

    def _round_legacy(self, value):
        return round(float(value), 2)

    def render(self, invoice, html):
        if html:
            return self.render_html(invoice)
        return self.render_text(invoice)

    # Now uses Decimal instead of float for all amounts
    def render_html(self, invoice):
        rows = []
        subtotal = Decimal("0")
        for entry in invoice.entries:
            amount = (entry.hours * entry.rate_rub).quantize(
                KOPECK, rounding=ROUND_HALF_UP
            )
            subtotal += amount
            rows.append(
                f"<tr><td>{entry.day:%d.%m.%Y}</td>"
                f"<td>{escape(entry.description)}</td>"
                f"<td>{amount}</td></tr>"
            )
        vat = Decimal("0")
        if invoice.client.vat_payer:
            vat = (subtotal * VAT_RATE).quantize(
                KOPECK, rounding=ROUND_HALF_UP
            )
        total = subtotal + vat
        return (
            f"<h1>Invoice {escape(invoice.number)}</h1>"
            f"<p>{escape(invoice.client.name)}</p>"
            "<table>" + "".join(rows) + "</table>"
            f"<p>Total: {total} RUB</p>"
        )

    def render_text(self, invoice):
        lines = [f"Invoice {invoice.number}", invoice.client.name, ""]
        subtotal = Decimal("0")
        for entry in invoice.entries:
            amount = (entry.hours * entry.rate_rub).quantize(
                KOPECK, rounding=ROUND_HALF_UP
            )
            subtotal += amount
            lines.append(
                f"{entry.day:%d.%m.%Y}  {entry.description}  {amount}"
            )
        vat = Decimal("0")
        if invoice.client.vat_payer:
            vat = (subtotal * VAT_RATE).quantize(
                KOPECK, rounding=ROUND_HALF_UP
            )
        lines.append("")
        lines.append(f"Subtotal: {subtotal} RUB")
        lines.append(f"VAT: {vat} RUB")
        lines.append(f"Total: {subtotal + vat} RUB")
        return "\n".join(lines)

    # Changed to send the HTML version too
    def send(self, invoice):
        message = EmailMessage()
        message["Subject"] = f"Invoice {invoice.number}"
        message["From"] = os.environ["INVOICE_FROM"]
        message["To"] = invoice.client.email
        message.set_content(self.render(invoice, False))
        message.add_alternative(self.render(invoice, True), subtype="html")
        with smtplib.SMTP(
            os.environ["SMTP_HOST"], int(os.environ["SMTP_PORT"]), timeout=30
        ) as smtp:
            smtp.starttls()
            smtp.login(os.environ["SMTP_USER"], os.environ["SMTP_PASSWORD"])
            smtp.send_message(message)

    def run_month(self, year, month, send):
        self.load_clients()
        self.load_entries()
        self.load_registry()
        month_entries = [
            entry
            for entry in self.entries
            if entry.day.year == year and entry.day.month == month
        ]
        by_client = {}
        for entry in month_entries:
            if entry.client_code not in self.clients:
                raise ValueError(f"unknown client {entry.client_code}")
            by_client.setdefault(entry.client_code, []).append(entry)
        period = f"{year}-{month:02d}"
        already_sent = {
            item["client"]
            for item in self.registry["sent"]
            if item["period"] == period
        }
        self.out_dir.mkdir(parents=True, exist_ok=True)
        invoices = []
        for code in sorted(by_client):
            if code in already_sent:
                continue
            client = self.clients[code]
            invoice = Invoice(
                number=self.next_number(year),
                client=client,
                issued_on=date.today(),
                entries=sorted(by_client[code], key=lambda e: e.day),
            )
            html_path = self.out_dir / f"{invoice.number}.html"
            html_path.write_text(render_html_v2(invoice), encoding="utf-8")
            text_path = self.out_dir / f"{invoice.number}.txt"
            text_path.write_text(
                self.render(invoice, False), encoding="utf-8"
            )
            subtotal = Decimal("0")
            for entry in invoice.entries:
                subtotal += (entry.hours * entry.rate_rub).quantize(
                    KOPECK, rounding=ROUND_HALF_UP
                )
            vat = Decimal("0")
            if client.vat_payer:
                vat = (subtotal * VAT_RATE).quantize(
                    KOPECK, rounding=ROUND_HALF_UP
                )
            if send:
                self.send(invoice)
                self.registry["sent"].append(
                    {
                        "number": invoice.number,
                        "client": code,
                        "period": period,
                        "total": str(subtotal + vat),
                        "sent_on": date.today().isoformat(),
                    }
                )
                self.save_registry()
            invoices.append(invoice)
        return invoices
