from datetime import datetime
from itertools import count

from flask import Flask, jsonify, request

app = Flask(__name__)

reminders = []
reminder_ids = count(1)


@app.post("/reminders")
def create_reminder():
    payload = request.get_json()
    reminder = {
        "id": next(reminder_ids),
        "text": payload["text"],
        "due": datetime.fromisoformat(payload["due"]),
        "done": False,
    }
    reminders.append(reminder)
    return jsonify(id=reminder["id"]), 201


@app.get("/reminders/due")
def due_reminders():
    now = datetime.now()
    due = [r for r in reminders if not r["done"] and r["due"] <= now]
    return jsonify([{"id": r["id"], "text": r["text"]} for r in due])


@app.post("/reminders/<int:reminder_id>/done")
def mark_done(reminder_id):
    for reminder in reminders:
        if reminder["id"] == reminder_id:
            reminder["done"] = True
            return "", 204
    return jsonify(error="not found"), 404
