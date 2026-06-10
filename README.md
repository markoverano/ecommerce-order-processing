# E-Commerce Order Processing Platform

[![CI](https://github.com/markov/ecommerce-order-processing/actions/workflows/ci.yml/badge.svg)](https://github.com/markov/ecommerce-order-processing/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Portfolio .NET 9 microservices platform. **10 production patterns:** Event Sourcing · DDD · CQRS · Saga · EDA · Circuit Breaker · Outbox · Webhook Signature Verification · Idempotency · Correlation IDs.

---

## Architecture

Each service owns its aggregate, publishes immutable domain events to RabbitMQ, and writes outbox rows transactionally. Saga Orchestrator coordinates order flow with reverse-order compensation. Kong API Gateway enforces JWT auth and rate limits. All services log to Elasticsearch + Kibana, trace via Jaeger, and expose Prometheus metrics.

```
Kong (8000) → Order (5001) · Payment (5002) · Inventory (5003) · Saga (5007)
              ↓
         RabbitMQ + PostgreSQL (6 isolated DBs) + Redis
```

---

## Quickstart

Requires Docker Desktop 4.x with Compose V2.

```bash
git clone https://github.com/markov/ecommerce-order-processing.git
cd ecommerce-order-processing

cp .env.example .env
# Edit: POSTGRES_PASSWORD, RABBITMQ_DEFAULT_PASS, STRIPE_API_KEY
docker compose -f deploy/docker-compose.yml up -d
```

Wait 15 seconds, then:

```bash
curl -X POST http://localhost:5001/api/v1/orders \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: $(uuidgen)" \
  -d '{"customerId":"00000000-0000-0000-0000-000000000001","items":[{"productId":"00000000-0000-0000-0000-000000000002","quantity":2,"unitPrice":49.99,"currency":"USD"}],"shippingAddress":{"line1":"123 Main St","city":"Seattle","state":"WA","postalCode":"98101","countryCode":"US"}}'

# Check order + saga state
ORDER_ID=<id from response>
curl http://localhost:5001/api/v1/orders/$ORDER_ID
curl http://localhost:5007/api/v1/sagas/$ORDER_ID
```

**Observability:** RabbitMQ (15672) · Grafana (3000, admin/admin) · Jaeger (16686) · Kibana (5601)

---

## Services

| Service | Notes |
|---|---|
| **Order** (5001) | Event sourcing + snapshots. CQRS reads from denormalized view. `POST /orders` · `GET /orders/{id}` |
| **Payment** (5002) | Stripe integration, circuit breaker (3× retry, open at 5 failures), HMAC-verified webhooks. `POST /process` · `POST /{id}/refund` |
| **Inventory** (5003) | Stock reservations with 2-hour TTL, background expiry service. `POST /reserve` · `POST /reservations/{id}/release` |
| **Shipping** (5004) | FedEx integration, circuit breaker, HMAC-verified webhooks. `POST /shipments` · `GET /{id}` |
| **Notification** (5005) | Mailgun (email) + Twilio (SMS), webhook handlers, SignalR hub for real-time status. `POST /notifications` · `GET /{id}` |
| **Saga Orchestrator** (5007) | Order flow coordination with reverse-order compensation. `GET /sagas/{orderId}` · `POST /admin/sagas/{id}/retry` |

---

## Structure

```
src/Shared/           # Events, Commands, ValueObjects
src/Infrastructure/   # EventStore, Outbox, RabbitMQ, Polly, middleware, policies
src/{Service}/        # Domain · Application · Infrastructure · Api (clean architecture per service)
tests/                # Unit tests (domain + application layers)
deploy/               # docker-compose.yml, scripts, Kubernetes templates (WIP)
```

---

## Testing

```bash
dotnet test --filter "Category!=Integration"   # Unit tests only
dotnet test                                     # All tests (requires Docker)
```

Unit test coverage: domain aggregates, CQRS handlers, value objects, saga state machine, Polly policies.

---

## Tech Stack

**Languages & Frameworks:** C# 13 · .NET 9 · ASP.NET Core 9 · MediatR (CQRS)  
**Data:** EF Core 9 · PostgreSQL 16 · Redis 7  
**Messaging:** RabbitMQ 3.13 (+ Azure Service Bus failover planned)  
**Resilience:** Polly (circuit breaker, retry, timeout)  
**API Gateway:** Kong (JWT + rate limiting + correlation ID injection)  
**Observability:** OpenTelemetry + Jaeger · Serilog + Elasticsearch + Kibana · Prometheus + Grafana  
**Testing:** xUnit · Moq · TestContainers · WireMock.NET
