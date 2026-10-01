import sqlite3

from flask import current_app, g

SCHEMA = """
CREATE TABLE IF NOT EXISTS patients (
    id INTEGER PRIMARY KEY,
    phone TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    password_hash TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS slots (
    id INTEGER PRIMARY KEY,
    doctor TEXT NOT NULL,
    starts_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS appointments (
    id INTEGER PRIMARY KEY,
    slot_id INTEGER NOT NULL REFERENCES slots (id),
    patient_id INTEGER NOT NULL REFERENCES patients (id),
    complaint TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'booked'
);
"""


def get_db():
    if "db" not in g:
        g.db = sqlite3.connect(current_app.config["DATABASE"])
        g.db.row_factory = sqlite3.Row
    return g.db


def close_db(exception=None):
    db = g.pop("db", None)
    if db is not None:
        db.close()


def init_db():
    get_db().executescript(SCHEMA)
