import os
import re
import sqlite3
from abc import ABC, abstractmethod

from flask import Flask, jsonify, request

EMAIL_PATTERN = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")


class SubscriberRepository(ABC):
    @abstractmethod
    def add(self, email): ...


class SqliteSubscriberRepository(SubscriberRepository):
    def __init__(self, path):
        self.path = path
        with sqlite3.connect(path) as conn:
            conn.execute(
                "CREATE TABLE IF NOT EXISTS subscribers"
                " (email TEXT PRIMARY KEY)"
            )

    def add(self, email):
        with sqlite3.connect(self.path) as conn:
            conn.execute(
                "INSERT OR IGNORE INTO subscribers VALUES (?)", (email,)
            )


class EmailNormalizer:
    def normalize(self, raw):
        email = str(raw).strip().lower()
        if len(email) > 254 or not EMAIL_PATTERN.match(email):
            raise ValueError("invalid email")
        return email


class SubscriptionService:
    def __init__(self, repository, normalizer):
        self.repository = repository
        self.normalizer = normalizer

    def subscribe(self, raw_email):
        self.repository.add(self.normalizer.normalize(raw_email))


class SubscriptionServiceFactory:
    @staticmethod
    def create():
        return SubscriptionService(
            SqliteSubscriberRepository(os.environ["SUBSCRIBERS_DB"]),
            EmailNormalizer(),
        )


app = Flask(__name__)
service = SubscriptionServiceFactory.create()


@app.post("/subscribe")
def subscribe():
    payload = request.get_json(silent=True) or {}
    try:
        service.subscribe(payload.get("email", ""))
    except ValueError:
        return jsonify(error="invalid email"), 400
    return "", 204
