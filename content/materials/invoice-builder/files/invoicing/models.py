from dataclasses import dataclass, field
from datetime import date
from decimal import Decimal


@dataclass(frozen=True)
class Client:
    code: str
    name: str
    email: str
    vat_payer: bool


@dataclass(frozen=True)
class Entry:
    client_code: str
    day: date
    description: str
    hours: Decimal
    rate_rub: Decimal


@dataclass
class Invoice:
    number: str
    client: Client
    issued_on: date
    entries: list = field(default_factory=list)
