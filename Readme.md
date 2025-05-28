# Gatekeeper – Distributed Rate Limiting Service

Gatekeeper is a production-ready, fault-tolerant, distributed rate-limiting system built in .NET 8. It provides flexible APIs (REST, gRPC, GraphQL) to enforce per-user or per-IP request limits using Redis and supports observability with Serilog, Jaeger, and Prometheus.

---

## 🧠 Key Features

- ✅ REST API endpoints: `/check`, `/consume`
- ✅ Redis-backed fixed window rate limiting
- ✅ gRPC support for internal services
- ✅ GraphQL for querying rate limit state & metrics
- ✅ Structured logging (Serilog)
- ✅ Tracing (Jaeger) & metrics (Prometheus)
- ✅ Clean architecture & modular design
- ✅ Fully Dockerized for local use or cloud deployment

---

## 📁 Project Structure

```
Gatekeeper/
├── src/
│   ├── Gatekeeper.Api/            # Minimal REST API
│   ├── Gatekeeper.Grpc/           # gRPC endpoint
│   ├── Gatekeeper.GraphQL/        # HotChocolate-based admin query layer
│   ├── Gatekeeper.Core/           # Domain logic & interfaces
│   ├── Gatekeeper.Infrastructure/ # Redis, logging, tracing
│   └── Gatekeeper.Tests/          # Unit + integration tests
│
├── docker/                        # Docker Compose setup (Redis, Jaeger, etc.)
├── docs/                          # Architecture & usage documentation
├── .github/workflows/             # GitHub Actions CI/CD pipeline
└── README.md                      # You're reading it
```

## 🧪 API Examples

### `POST /check`
```json
{
  "userId": "123",
  "route": "/api/orders",
  "limitId": "ORDERS-PER-MINUTE"
}
```
Response:
```json
{
  "allowed": true,
  "remaining": 3,
  "resetIn": 22
}
```

---

## 🔗 Tech Stack
- .NET 9
- Redis (Sliding/Fixed window)
- Serilog, Jaeger, Prometheus
- gRPC, GraphQL (HotChocolate)
- Docker & GitHub Actions

---

## 📢 Coming Soon
- Token bucket limiter
- Admin dashboard
- Auth/permissions on GraphQL

---

## 🧑‍💻 Author
**Artem Sydorovych**  
[LinkedIn](https://linkedin.com/in/artem-sydorovych-9b4a4b207) · [GitHub](https://github.com/ArtemSydorovych)

---

## ⭐️ Star This Repo
If you find this project useful or inspiring, give it a star and share it! 🚀
