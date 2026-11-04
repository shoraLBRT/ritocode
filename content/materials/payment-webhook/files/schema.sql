CREATE TABLE accounts (
    id BIGINT PRIMARY KEY,
    email TEXT NOT NULL,
    balance_rub NUMERIC(12, 2) NOT NULL DEFAULT 0
);

CREATE TABLE payments (
    id BIGSERIAL PRIMARY KEY,
    provider_payment_id TEXT NOT NULL,
    account_id BIGINT NOT NULL REFERENCES accounts (id),
    amount_rub NUMERIC(12, 2) NOT NULL,
    received_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
