# .NET System Design Interview Prep — URL Shortener / Notification / Order Processing

Use this structure for any "design a system" prompt. Interviewers care more about your *reasoning* than a perfect answer — always narrate trade-offs out loud.

---

## 1. Clarify Requirements (do this FIRST, always)

Ask before designing:

- **Scale**: reads/writes per second, total records, growth rate (e.g. "100M URLs, 10:1 read/write ratio")
- **Latency**: p99 target (e.g. redirect < 50ms)
- **Consistency**: strong (order/payment systems) vs eventual (notifications, analytics)
- **Availability**: 99.9% vs 99.99% — affects whether you need multi-region
- **Security**: auth model, PII, rate limiting, encryption at rest/in transit

**Example answer for URL shortener:**
> "Let's say 100M new URLs/month, 10B redirects/month, p99 redirect latency under 50ms, eventual consistency is fine for analytics but the redirect itself must be strongly available. I'll assume no auth requirement for MVP but note where I'd add it."

Stating assumptions out loud is itself a signal you'll be evaluated on.

---

## 2. API and Data Model

### REST API (minimal API style, .NET 8+)

```csharp
app.MapPost("/api/urls", async (CreateUrlRequest req, IUrlService svc) =>
{
    var shortCode = await svc.CreateShortUrlAsync(req.LongUrl, req.CustomAlias, req.ExpiresAt);
    return Results.Created($"/{shortCode}", new { shortUrl = $"https://sho.rt/{shortCode}" });
});

app.MapGet("/{code}", async (string code, IUrlService svc, HttpContext ctx) =>
{
    var longUrl = await svc.ResolveAsync(code); // cache-first lookup
    if (longUrl is null) return Results.NotFound();
    return Results.Redirect(longUrl, permanent: false); // 302, not 301 — lets you change/expire later
});
```

### Data model

```sql
CREATE TABLE urls (
    short_code   VARCHAR(8)   PRIMARY KEY,
    long_url     TEXT         NOT NULL,
    created_at   TIMESTAMPTZ  DEFAULT now(),
    expires_at   TIMESTAMPTZ,
    owner_id     UUID,
    click_count  BIGINT       DEFAULT 0
);
CREATE INDEX idx_urls_owner ON urls(owner_id);
```

**Short code generation** — two common approaches, know the trade-off:
- **Base62 encode an auto-increment ID** (simple, sequential, guessable, needs a centralized counter or ID-generation service like Snowflake)
- **Hash (MD5/SHA) + truncate + collision check** (distributable, but needs a collision retry loop)

For an interview, say you'd use a **Snowflake-style distributed ID generator** (timestamp + worker ID + sequence) to avoid a single point of contention while keeping IDs roughly sortable.

---

## 3. SQL vs NoSQL

| | SQL (Postgres/SQL Server) | NoSQL (Cosmos DB/DynamoDB) |
|---|---|---|
| Best for | Orders, payments, anything needing transactions/joins | High-write telemetry, notification logs, session data |
| Consistency | Strong, ACID | Eventual by default (tunable in Cosmos) |
| Scaling | Vertical first, then read replicas / sharding | Horizontal by design (partition key) |
| Schema | Rigid, migrations needed | Flexible |

**Rule of thumb I'd give in an interview:**
> "Order/payment state goes in SQL because I need transactions and foreign key integrity. Click analytics and notification delivery logs go in NoSQL because they're high-volume, append-mostly, and don't need joins — I'll partition by `date + shortCode` or `userId`."

For a URL shortener specifically: the URL→destination mapping itself is simple key→value, so either works, but a **key-value store (Redis/DynamoDB) as the source of truth for reads**, backed by SQL/blob for the durable record, is a common pragmatic answer.

---

## 4. Caching with Redis

Cache-aside pattern for the redirect hot path:

```csharp
public class UrlService : IUrlService
{
    private readonly IDistributedCache _cache;
    private readonly IUrlRepository _repo;

    public async Task<string?> ResolveAsync(string code)
    {
        var cached = await _cache.GetStringAsync(code);
        if (cached is not null) return cached;

        var url = await _repo.GetLongUrlAsync(code);
        if (url is null) return null;

        await _cache.SetStringAsync(code, url, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        });
        return url;
    }
}
```

Key points to mention:
- **TTL + eviction policy** (`allkeys-lru`) so hot URLs stay cached, cold ones age out
- **Cache stampede protection**: use a short-lived lock (`SETNX`) or request coalescing so 10,000 simultaneous misses for the same key don't all hit the DB at once
- **Write-through on create** — populate cache immediately when a URL is created, since it's likely to be clicked soon
- Increment `click_count` **asynchronously** (via a queue), never synchronously on the redirect's critical path

---

## 5. Async Work with Queues (Kafka / Azure Service Bus / RabbitMQ / SQS)

When to use which — know this cold:

| | Best for |
|---|---|
| **Kafka** | Very high throughput, event streaming, multiple consumers replaying the same log, ordering per partition |
| **Azure Service Bus** | .NET-native, needs sessions/dedup/DLQ built-in, enterprise middleware |
| **RabbitMQ** | Complex routing (topic/fanout exchanges), lower ops overhead than Kafka |
| **SQS** | Simple, fully managed, AWS-native, pairs with SNS for fanout |

**Order-processing example** — publish an event after checkout instead of doing everything synchronously:

```csharp
public class OrderService
{
    private readonly ServiceBusSender _sender;

    public async Task<Guid> PlaceOrderAsync(OrderRequest req)
    {
        var orderId = Guid.NewGuid();
        await _repo.SaveOrderAsync(orderId, req, status: "Pending"); // write to DB first (source of truth)

        var message = new ServiceBusMessage(JsonSerializer.Serialize(new OrderCreatedEvent(orderId, req)))
        {
            MessageId = orderId.ToString() // enables broker-side dedup
        };
        await _sender.SendMessageAsync(message);

        return orderId; // respond to user immediately; payment/fulfillment happen async
    }
}
```

Consumer with retry + dead-letter:

```csharp
processor.ProcessMessageAsync += async args =>
{
    try
    {
        var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(args.Message.Body);
        await _paymentService.ChargeAsync(evt);
        await args.CompleteMessageAsync(args.Message);
    }
    catch (TransientException)
    {
        // let it retry — Service Bus redelivers based on MaxDeliveryCount
        throw;
    }
    catch (Exception ex)
    {
        await args.DeadLetterMessageAsync(args.Message, "ProcessingFailed", ex.Message);
    }
};
```

---

## 6. Retries, DLQ, Duplicate Delivery, Idempotency

This is the section interviewers probe hardest. Core principle: **queues give you at-least-once delivery, never exactly-once — your consumer must be idempotent.**

**Idempotency key pattern (this is the answer to "prevent double-charging a customer"):**

```csharp
public async Task ChargeAsync(OrderCreatedEvent evt)
{
    // Idempotency key = orderId, unique-constrained in DB
    var alreadyProcessed = await _db.ProcessedPayments
        .AnyAsync(p => p.OrderId == evt.OrderId);

    if (alreadyProcessed) return; // no-op, safe to "process" again

    using var tx = await _db.Database.BeginTransactionAsync();
    try
    {
        await _paymentGateway.ChargeAsync(evt.OrderId, evt.Amount);

        _db.ProcessedPayments.Add(new ProcessedPayment { OrderId = evt.OrderId, ChargedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(); // unique index on OrderId throws if a race duplicates this

        await tx.CommitAsync();
    }
    catch (DbUpdateException) // unique constraint violation = someone else already charged it
    {
        await tx.RollbackAsync();
    }
}
```

```sql
CREATE UNIQUE INDEX idx_processed_payments_order ON processed_payments(order_id);
```

Why this works: the **database unique constraint is the real source of truth for "did this happen"**, not application logic — application-level checks alone have a race window between check and write.

**Outbox pattern** (avoid the "DB write succeeded but message publish failed" problem):
1. Write the order row AND an "outbox" row in the *same DB transaction*
2. A separate background poller reads unpublished outbox rows and publishes them to the queue, marking them sent
3. This guarantees the event is published if and only if the DB transaction committed

**Dead-letter queue**: after N failed delivery attempts, the broker moves the message to a DLQ instead of retrying forever. You need:
- Alerting on DLQ depth
- A replay tool/runbook (manual or automated) once the root cause is fixed
- Never silently drop — DLQ messages usually represent lost revenue or lost notifications

---

## 7. Scaling ASP.NET Core Horizontally

- **Stateless services**: no in-memory session state; put session/cache in Redis, not `IMemoryCache`, once you have >1 instance
- **Load balancer** (Azure App Gateway / AWS ALB / nginx) in front of N instances, health-check endpoint:

```csharp
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") // e.g. DB + Redis reachable
});
```

- **Horizontal Pod Autoscaler** (if on Kubernetes) scaling on CPU or custom metric (queue depth is often a better signal than CPU for a queue consumer service)
- **Connection pooling**: make sure `DbContext` is scoped correctly (`AddDbContext`, not singleton) and pool size is tuned for instance count × concurrency
- Push CPU-heavy work (image resizing, PDF generation) off the request thread into a queue-backed worker so web tier stays thin and scales cheaply

---

## 8. Observability

- **Metrics**: request rate, error rate, p50/p95/p99 latency, queue depth, DLQ count — expose via `OpenTelemetry` + Prometheus/Azure Monitor
- **Logs**: structured logging (Serilog), correlation ID propagated through headers and into queue message properties so you can trace a request across services
- **Traces**: distributed tracing (OpenTelemetry + Jaeger/App Insights) so a single order can be followed through API → queue → payment service → notification service

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("OrderService")
        .AddOtlpExporter());
```

- **Alerts**: p99 latency breach, error rate > X%, DLQ depth > 0, queue consumer lag growing — alert on symptoms (SLO burn), not just raw thresholds

---

## 9. Deployment, Rollback, DR, Migration

- **Blue-green or canary deploys**: shift 5% → 25% → 100% traffic, auto-rollback on error-rate spike
- **DB migrations**: use EF Core migrations, but for zero-downtime: make schema changes **backward-compatible first** (add nullable column → deploy code that writes both → backfill → deploy code that reads new column → drop old column). Never do a breaking rename in one step.
- **Disaster recovery**: define RPO/RTO. For order data: multi-region DB replication (async is usually fine — a few seconds of RPO), automated failover, regular restore drills (a backup you've never restored isn't a backup)
- **Rollback**: keep deploys stateless and DB changes backward-compatible specifically so a code rollback doesn't require a DB rollback

---

## Deep-Dive Questions

### "How would you process 10 million events/day?"

10M/day ≈ 116 events/sec average, but design for **peak, not average** — assume 5-10x burst (~1,000/sec peak).

- Ingest via a queue (Kafka/Service Bus) immediately — decouple ingestion from processing so bursts don't overload downstream services
- Partition by a key with good cardinality (e.g. `userId` or `orderId` hash) so consumers scale horizontally and ordering is preserved *per key*
- Consumers scale out independently of the API tier; use consumer groups so adding instances increases throughput linearly
- Batch downstream writes (e.g. bulk insert 100 events at a time to SQL/Cosmos) instead of one write per event
- Back-pressure: if downstream (DB, payment gateway) can't keep up, let the queue absorb the backlog rather than dropping events — that's the whole point of decoupling with a queue

### "What happens when the queue is unavailable?"

- The **producer** should not block the user-facing request indefinitely. Options: fail fast and return a retryable error to the client, or fall back to a local durable buffer (e.g. write to an outbox table in the same DB transaction as the business write — see Outbox pattern above — then a background job retries publishing once the queue recovers)
- The **consumer** side: if the queue itself is down, there's nothing to consume — that's a broker HA problem, which is why production queues run as a clustered/replicated service (Kafka replication factor 3, Service Bus geo-DR, etc.)
- Design for **graceful degradation**: e.g. if the notification queue is down, the order still completes — the user just gets their confirmation email a few minutes late once the queue recovers, rather than the whole checkout failing

### "How do you prevent a duplicate message from charging a customer twice?"

Covered in detail above — the short version to say out loud:
1. Message has a unique `MessageId`/`OrderId` used as an **idempotency key**
2. A **database unique constraint** on that key is the actual guarantee (not just an in-app check, which has a race condition)
3. On duplicate delivery, the charge attempt short-circuits as a no-op once the constraint shows it's already processed
4. Combine with the **Outbox pattern** so the "order created" event and the DB write are atomic — you never publish an event for a write that didn't actually commit

### "How would you evolve from one service to microservices — and when should you NOT?"

**Evolve when:**
- Distinct components have very different scaling needs (e.g. redirect-serving needs to scale 100x more than the admin/URL-creation API — split them)
- Teams are blocked on each other's deploy cadence (org/Conway's Law reason, not just technical)
- A component has fundamentally different reliability/latency requirements (payment processing needs strong consistency; notification sending is fire-and-forget)

**Practical path**: start as a **modular monolith** — one deployable, but internally organized into clear bounded modules (Orders, Payments, Notifications) with well-defined interfaces between them and no shared mutable state. When a module needs independent scaling or a separate team, extract it — the clean internal boundary makes that a low-risk refactor instead of a rewrite.

**When NOT to go microservices:**
- Small team, unclear domain boundaries — you'll pay the distributed-systems tax (network calls, eventual consistency, distributed tracing, more infra) before you've earned the benefit
- If you can't yet answer "what are our bounded contexts," splitting early usually means you split them wrong and now have a distributed monolith — network calls where you used to have function calls, but the same tight coupling
- Rule of thumb to state in the interview: *"I'd default to a modular monolith and extract services only when a specific scaling, team-ownership, or reliability boundary demands it — not preemptively."*

---

## Quick Framework to Reuse in Any System Design Round

1. Clarify scale/latency/consistency/availability (2 min)
2. Sketch API + data model
3. Pick storage (SQL vs NoSQL) with a stated reason
4. Add caching for the hot read path
5. Add a queue for anything that doesn't need a synchronous response
6. Address idempotency/duplicates explicitly — this almost always comes up
7. Talk scaling (stateless services, horizontal scale, autoscaling signal)
8. Talk observability (metrics/logs/traces/alerts) briefly
9. Close with deployment/rollback/DR if time allows
