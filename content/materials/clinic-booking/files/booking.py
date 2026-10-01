from db import get_db


class SlotNotFound(Exception):
    pass


class SlotTaken(Exception):
    pass


def free_slots(doctor):
    return get_db().execute(
        "SELECT s.id, s.starts_at FROM slots s"
        " LEFT JOIN appointments a"
        " ON a.slot_id = s.id AND a.status = 'booked'"
        " WHERE s.doctor = ? AND a.id IS NULL"
        " ORDER BY s.starts_at",
        (doctor,),
    ).fetchall()


def book(slot_id, patient_id, complaint):
    db = get_db()
    slot = db.execute(
        "SELECT id FROM slots WHERE id = ?", (slot_id,)
    ).fetchone()
    if slot is None:
        raise SlotNotFound(slot_id)
    taken = db.execute(
        "SELECT 1 FROM appointments"
        " WHERE slot_id = ? AND status = 'booked'",
        (slot_id,),
    ).fetchone()
    if taken:
        raise SlotTaken(slot_id)
    cursor = db.execute(
        "INSERT INTO appointments (slot_id, patient_id, complaint)"
        " VALUES (?, ?, ?)",
        (slot_id, patient_id, complaint),
    )
    db.commit()
    return cursor.lastrowid


def find_appointment(appointment_id):
    return get_db().execute(
        "SELECT a.id, s.doctor, s.starts_at, a.complaint, a.status"
        " FROM appointments a JOIN slots s ON s.id = a.slot_id"
        " WHERE a.id = ?",
        (appointment_id,),
    ).fetchone()


def cancel(appointment_id):
    db = get_db()
    db.execute(
        "UPDATE appointments SET status = 'cancelled' WHERE id = ?",
        (appointment_id,),
    )
    db.commit()
