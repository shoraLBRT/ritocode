from flask import Flask, abort, jsonify, request, session
from werkzeug.security import check_password_hash

import booking
from db import close_db, get_db

app = Flask(__name__)
app.config.from_prefixed_env()
app.teardown_appcontext(close_db)


def current_patient():
    patient_id = session.get("patient_id")
    if patient_id is None:
        abort(401)
    return patient_id


@app.post("/login")
def login():
    form = request.get_json(silent=True) or {}
    patient = get_db().execute(
        "SELECT id, password_hash FROM patients WHERE phone = ?",
        (str(form.get("phone", "")),),
    ).fetchone()
    password = str(form.get("password", ""))
    if patient is None or not check_password_hash(
        patient["password_hash"], password
    ):
        abort(401)
    session["patient_id"] = patient["id"]
    return "", 204


@app.get("/doctors/<doctor>/slots")
def list_slots(doctor):
    slots = booking.free_slots(doctor)
    return jsonify([dict(slot) for slot in slots])


@app.post("/appointments")
def create_appointment():
    patient_id = current_patient()
    form = request.get_json(silent=True) or {}
    complaint = str(form.get("complaint", "")).strip()
    if not complaint or len(complaint) > 500:
        abort(400)
    try:
        slot_id = int(form["slot_id"])
    except (KeyError, TypeError, ValueError):
        abort(400)
    try:
        appointment_id = booking.book(slot_id, patient_id, complaint)
    except booking.SlotNotFound:
        abort(404)
    except booking.SlotTaken:
        abort(409)
    return jsonify(id=appointment_id), 201


@app.get("/appointments/<int:appointment_id>")
def show_appointment(appointment_id):
    current_patient()
    appointment = booking.find_appointment(appointment_id)
    if appointment is None:
        abort(404)
    return jsonify(dict(appointment))


@app.post("/appointments/<int:appointment_id>/cancel")
def cancel_appointment(appointment_id):
    current_patient()
    if booking.find_appointment(appointment_id) is None:
        abort(404)
    booking.cancel(appointment_id)
    return "", 204
