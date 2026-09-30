import smtplib

SMTP_PASSWORD = "mail-Pass-2024"


def invoice_total(items):
    total = 0.0
    for item in items:
        total += item["price"] * item["qty"]
    return round(total, 2)


def send_invoice(customer_email, items):
    body = f"Total: {invoice_total(items)}"
    try:
        with smtplib.SMTP("smtp.example.com", 587) as smtp:
            smtp.login("billing", SMTP_PASSWORD)
            smtp.sendmail("billing@example.com", customer_email, body)
    except Exception:
        pass
