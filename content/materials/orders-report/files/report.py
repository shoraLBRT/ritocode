import os
import sqlite3
from io import StringIO

import pandas as pd
from flask import Flask, Response, request

app = Flask(__name__)

COLUMNS = ["id", "customer_email", "total_kopecks", "status", "created_at"]


@app.get("/reports/orders.csv")
def orders_report():
    status = request.args.get("status", "paid")
    sort = request.args.get("sort", "created_at")
    with sqlite3.connect(os.environ["SHOP_DB"]) as conn:
        rows = conn.execute(
            f"SELECT {', '.join(COLUMNS)} FROM orders"
            f" WHERE status = ? ORDER BY {sort}",
            (status,),
        ).fetchall()
    frame = pd.DataFrame(rows, columns=COLUMNS)
    buffer = StringIO()
    frame.to_csv(buffer, index=False)
    return Response(buffer.getvalue(), mimetype="text/csv")
