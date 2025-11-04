# ⚡️ Idempotent API with .NET 9, EF Core 9 & Polly v8

[![.NET 9](https://img.shields.io/badge/.NET-9.0-blueviolet?logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core 9](https://img.shields.io/badge/EF%20Core-9.0-green?logo=nuget)](https://learn.microsoft.com/ef/core/)
[![Polly v8](https://img.shields.io/badge/Resilience-Polly%20v8-orange?logo=nuget)](https://github.com/App-vNext/Polly)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgrey.svg)](LICENSE)

> A production-ready example of building **idempotent HTTP POST endpoints** in .NET 9 using Minimal APIs, EF Core 9, Polly v8, and an **Idempotency-Key** persistence layer with in-memory cache.

---

## 🧠 Overview

In distributed systems and payment workflows, **idempotency** ensures that a repeated operation (e.g. a retried request) **produces the same result without side effects**.

This template demonstrates:

- ✅ **Idempotent POST / payments** endpoint using a unique `Idempotency-Key` header  
- 💾 Response persistence in **SQLite** and memory cache  
- 🔐 Request fingerprint (SHA-256) to detect conflicting payloads  
- ⚙️ **Retry-safe** HTTP client with **Polly v8**  
- 🧱 EF Core 9 + Minimal API architecture  
- 🐳 Docker & GitHub Actions CI  
- 🧩 Optional Redis or distributed caching ready  

---

## 📂 Project structure
```
OS.Tuto.IdempotentApi/
 ├─ Program.cs
 ├─ Idempotency/
 │   ├─ IdempotencyStore.cs
 │   ├─ IdempotencyRecord.cs
 ├─ Data/
 │   └─ AppDbContext.cs
 ├─ Domain/
 │   ├─ Payment.cs
 │   └─ PaymentStatus.cs
 ├─ Dtos/
 │   ├─ PaymentRequest.cs
 │   └─ PaymentResponse.cs
 ├─ OS.Tuto.IdempotentApi.csproj
 └─ appsettings.json
```

 🚀 Quick start

1️⃣ Clone and restore

```bash
git clone https://github.com/oussama-saidi/os-tuto-net-core-rest-api-Idempotent.git
cd IdempotentApi
dotnet restore
```
### 2️⃣ Run database migrations
```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Init --project IdempotentApi
dotnet ef database update --project IdempotentApi
```
### 3️⃣ Start the API
```bash
dotnet run --project IdempotentApi
```

💳 Test the idempotent endpoint
✅ Create a payment (first call)
```bash
curl -s http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: key-123" \
  -d '{"amount": 99.99, "currency": "EUR", "recipient": "alice@example.com"}'
```

🔁 Repeat the same call (same key + payload)

→ You’ll get the same response; no duplicate record.

⚠️ Try same key with different payload
```bash
curl -i http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: key-123" \
  -d '{"amount": 500, "currency": "EUR", "recipient": "bob@example.com"}'
```
→ Returns 409 Conflict, since the payload differs.

🧩 How it works
| Layer                        | Role                                               |
| ---------------------------- | -------------------------------------------------- |
| **`Idempotency-Key` header** | Client-provided unique token per logical operation |
| **SHA-256 fingerprint**      | Detects request content changes for same key       |
| **SQLite + Memory cache**    | Persists the response and serves instant replays   |
| **EF Core 9 unique index**   | Prevents duplicate rows for same key               |
| **Polly v8 retry handler**   | Safe retry logic for idempotent requests           |

⚙️ Retry-safe client (C# example)

A sample Typed Client using HttpClient + Polly v8:
```c#
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

var services = new ServiceCollection();

services.AddHttpClient<PaymentsClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000/");
})
.AddResilienceHandler("retry", builder =>
    builder.AddRetry(new()
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential
    }));

var provider = services.BuildServiceProvider();
var cli = provider.GetRequiredService<PaymentsClient>();

var key = Guid.NewGuid().ToString();
await cli.CreatePaymentAsync(new(50m, "EUR", "alice@example.com"), key);

public record PaymentRequest(decimal Amount, string Currency, string Recipient);

public sealed class PaymentsClient
{
    private readonly HttpClient _http;
    public PaymentsClient(HttpClient http) => _http = http;

    public async Task CreatePaymentAsync(PaymentRequest req, string idempotencyKey)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "payments")
        {
            Content = JsonContent.Create(req)
        };
        msg.Headers.Add("Idempotency-Key", idempotencyKey);

        using var resp = await _http.SendAsync(msg);
        Console.WriteLine(await resp.Content.ReadAsStringAsync());
    }
}

```

🐳 Run with Docker
```bash
docker compose up --build
```
API exposed on http://localhost:8080

SQLite stored in a persistent volume dbdata

```bash
docker exec -it <container> sh
sqlite3 /data/idempotent.db
```

🧪 GitHub Actions (CI)

A pre-configured workflow in .github/workflows/dotnet.yml:
```yaml
name: build-test
on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet ef migrations script --project IdempotentApi
```

☁️ Scaling out (distributed cache)

For multi-instance deployments:
1. Replace IMemoryCache with IDistributedCache (e.g. Redis).
2. Add NuGet packages:
```bash
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
dotnet add package StackExchange.Redis
```
3. In Program.cs:
```c#
builder.Services.AddStackExchangeRedisCache(o =>
    o.Configuration = builder.Configuration.GetConnectionString("Redis"));
```
4. Update IdempotencyStore to use Redis SetStringAsync / GetStringAsync.
This makes the idempotency layer cluster-safe across multiple containers.

🧠 Why idempotency matters
| Scenario       | Problem without idempotency          | Solution                                  |
| -------------- | ------------------------------------ | ----------------------------------------- |
| Payment API    | Double charge on network retry       | Replays same transaction result           |
| Order creation | Duplicate orders on user refresh     | Same `Idempotency-Key` returns same order |
| Messaging      | Message redelivery causes duplicates | Consumer ignores duplicates               |
🧩 Tech stack

| Component          | Description                       |
| ------------------ | --------------------------------- |
| **.NET 9**         | Modern Minimal API framework      |
| **EF Core 9**      | ORM with SQLite storage           |
| **Polly v8**       | Resilience policies & retry logic |
| **SQLite**         | Simple persistent store           |
| **Docker**         | Containerized deployment          |
| **GitHub Actions** | Continuous integration pipeline   |

🧾 API Reference

| Method | Endpoint                 | Description                                       | Idempotent |
| :----- | :----------------------- | :------------------------------------------------ | :--------: |
| `POST` | `/payments`              | Create a new payment (requires `Idempotency-Key`) |      ✅     |
| `PUT`  | `/payments/{id}/capture` | Capture an existing payment                       |      ✅     |
| `GET`  | `/`                      | Health check                                      |      ✅     |

🧰 Troubleshooting
| Issue           | Cause                                            | Fix                                                       |
| --------------- | ------------------------------------------------ | --------------------------------------------------------- |
| `409 Conflict`  | Same Idempotency-Key used with different payload | Generate new key per unique request                       |
| `SQLITE_BUSY`   | DB locked in dev mode                            | Use `dotnet ef database update` outside of concurrent run |
| Request timeout | Retry policy exceeded                            | Adjust Polly retry or network configuration               |
🔑 Security considerations

Use strong random UUIDs for Idempotency-Key (GUID v4 or ULID).

Log and monitor key collisions.

In production, store keys for 24–72 hours depending on business requirements.

Protect against replay by setting a TTL and scoping per endpoint.

🧱 Extending this project

✅ Add Redis distributed cache

✅ Add Serilog structured logging

✅ Add integration tests (xUnit + Testcontainers)

✅ Use Dapr or Azure Service Bus for event-driven reliability

🪪 License
This project is licensed under the MIT License — free to use, modify, and distribute.

👨‍💻 Author

Oussama Saidi
Full-Stack .NET & React Consultant

🌐 https://oussamasaidi.com

⭐ If you find this template useful, consider giving it a star on GitHub — it helps visibility and supports open-source work.



