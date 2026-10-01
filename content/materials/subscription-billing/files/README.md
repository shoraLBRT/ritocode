# Subscription billing

Charges customers whose subscription is due and moves their paid-until
date forward.

## Features

- Charges every month on the same day of the month the subscription
  started.
- A failed charge is retried three times with a growing pause.
- A receipt is e-mailed to the customer after every successful charge.

## Tests

    pytest
