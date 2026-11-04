import pytest
from werkzeug.security import generate_password_hash

from app import app
from db import get_db, init_db


@pytest.fixture
def client(tmp_path):
    app.config.update(
        DATABASE=str(tmp_path / "test.db"), SECRET_KEY="test-only"
    )
    with app.app_context():
        init_db()
        db = get_db()
        db.execute(
            "INSERT INTO patients (phone, name, password_hash)"
            " VALUES ('+70000000001', 'Test Patient', ?)",
            (generate_password_hash("test-password"),),
        )
        db.execute(
            "INSERT INTO slots (doctor, starts_at)"
            " VALUES ('ivanova', '2026-10-05 09:00')"
        )
        db.commit()
    with app.test_client() as client:
        client.post(
            "/login",
            json={"phone": "+70000000001", "password": "test-password"},
        )
        yield client


def test_book_a_free_slot(client):
    response = client.post(
        "/appointments", json={"slot_id": 1, "complaint": "Headache"}
    )
    assert response.status_code == 201


def test_a_booked_slot_is_not_listed(client):
    client.post("/appointments", json={"slot_id": 1, "complaint": "Cough"})
    assert client.get("/doctors/ivanova/slots").get_json() == []


def test_show_an_appointment(client):
    created = client.post(
        "/appointments", json={"slot_id": 1, "complaint": "Fever"}
    ).get_json()
    shown = client.get(f"/appointments/{created['id']}").get_json()
    assert shown["complaint"] == "Fever"
    assert shown["doctor"] == "ivanova"
