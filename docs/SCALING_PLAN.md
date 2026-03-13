# Scaling and Future Expansion

The platform should start as a modular monolith but remain scalable.

## Scaling Strategy

Early stage:

- single backend service
- background workers
- containerized runner execution

Later stages:

- dedicated evaluation cluster
- distributed worker pools
- isolated benchmark runners

## Horizontal Scaling Points

Possible future service boundaries:

- evaluation service
- repository integration service
- recommendation engine
- moderation service

Agents should avoid introducing these boundaries prematurely.