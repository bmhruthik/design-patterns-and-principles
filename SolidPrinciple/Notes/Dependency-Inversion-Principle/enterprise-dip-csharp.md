# Hard Enterprise-Level Dependency Inversion Principle (DIP) Examples in C#

The **Dependency Inversion Principle (DIP)** has two connected rules:

1. High-level policy must not depend on low-level implementation details; both depend on abstractions.
2. Abstractions must not depend on details; details must depend on abstractions.

In a production C# system, DIP is not simply “use interfaces everywhere.” The useful direction is that an application’s business policy owns the contract it needs, while databases, HTTP SDKs, message brokers, and cloud vendors implement that contract at the edge.

The examples below use a **ports-and-adapters** style: application code defines a port; infrastructure implements an adapter; the composition root wires them together.

---

## Example 1 — Order placement should not depend on SQL Server or a payment SDK

### Incorrect: business logic depends directly on details

```csharp
public sealed class OrderService
{
    public async Task PlaceAsync(CreateOrderCommand command, CancellationToken ct)
    {
        using var connection = new SqlConnection("Server=prod-sql;...");
        await connection.ExecuteAsync("INSERT INTO Orders ...", command);

        var stripe = new StripeClient("sk_live_...");
        await stripe.ChargeAsync(command.CardToken, command.TotalAmount, ct);
    }
}
```

This class contains business policy, SQL implementation, secret management, and a vendor API dependency. It is difficult to test, difficult to migrate, and hazardous to change.

### Correct: application-owned ports

```csharp
public sealed record CreateOrderCommand(
    string CustomerId,
    IReadOnlyCollection<OrderLineInput> Lines,
    PaymentMethod PaymentMethod);

public sealed record OrderLineInput(string Sku, int Quantity, decimal UnitPrice);
public sealed record PaymentMethod(string Token, string ProviderHint);
public sealed record OrderId(Guid Value);
public sealed record PaymentAuthorization(bool Approved, string? AuthorizationId, string? DeclineReason);

// These ports describe what the use case needs. They contain no SQL or Stripe types.
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);
    Task<bool> ExistsForIdempotencyKeyAsync(string key, CancellationToken ct);
}

public interface IPaymentAuthorizer
{
    Task<PaymentAuthorization> AuthorizeAsync(
        OrderId orderId,
        decimal amount,
        PaymentMethod paymentMethod,
        CancellationToken ct);
}

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct);
}
```

### Stable application service

```csharp
public sealed class PlaceOrderHandler
{
    private readonly IOrderRepository _orders;
    private readonly IPaymentAuthorizer _payments;
    private readonly IUnitOfWork _unitOfWork;

    public PlaceOrderHandler(
        IOrderRepository orders,
        IPaymentAuthorizer payments,
        IUnitOfWork unitOfWork) =>
        (_orders, _payments, _unitOfWork) = (orders, payments, unitOfWork);

    public async Task<OrderId> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.Lines);
        var authorization = await _payments.AuthorizeAsync(
            order.Id, order.TotalAmount, command.PaymentMethod, ct);

        if (!authorization.Approved)
            throw new PaymentDeclinedException(authorization.DeclineReason ?? "Payment declined.");

        order.MarkAuthorized(authorization.AuthorizationId!);
        await _orders.AddAsync(order, ct);
        await _unitOfWork.CommitAsync(ct);
        return order.Id;
    }
}

public sealed class PaymentDeclinedException : Exception
{
    public PaymentDeclinedException(string message) : base(message) { }
}
```

### Infrastructure adapters depend on the application contracts

```csharp
public sealed class StripePaymentAuthorizer : IPaymentAuthorizer
{
    private readonly StripeClient _client;
    public StripePaymentAuthorizer(StripeClient client) => _client = client;

    public async Task<PaymentAuthorization> AuthorizeAsync(
        OrderId orderId, decimal amount, PaymentMethod method, CancellationToken ct)
    {
        var charge = await _client.AuthorizeAsync(
            token: method.Token,
            amountInMinorUnits: (long)(amount * 100m),
            idempotencyKey: orderId.Value.ToString(),
            cancellationToken: ct);

        return new PaymentAuthorization(charge.Approved, charge.Id, charge.DeclineCode);
    }
}

public sealed class EfCoreOrderRepository : IOrderRepository
{
    private readonly OrderingDbContext _db;
    public EfCoreOrderRepository(OrderingDbContext db) => _db = db;

    public Task AddAsync(Order order, CancellationToken ct) => _db.Orders.AddAsync(order, ct).AsTask();
    public Task<bool> ExistsForIdempotencyKeyAsync(string key, CancellationToken ct) =>
        _db.Orders.AnyAsync(x => x.IdempotencyKey == key, ct);
}
```

The application service can now be tested with in-memory fakes, while switching from Stripe to Adyen or EF Core to a REST-backed order store only changes adapters and DI registration.

---

## Example 2 — Notification policy should not know about SMTP, Twilio, or Slack

An incident-management policy decides *when* and *what* to notify. A channel adapter decides *how* that notification reaches a human.

### Application port and policy

```csharp
public enum IncidentSeverity { Low, Medium, High, Critical }

public sealed record Incident(Guid Id, string Service, IncidentSeverity Severity, string Summary, DateTimeOffset OpenedAt);
public sealed record Notification(string Subject, string Body, string DeduplicationKey);

public interface INotificationChannel
{
    string ChannelName { get; }
    bool CanDeliver(Incident incident);
    Task DeliverAsync(Notification notification, CancellationToken ct);
}

public sealed class IncidentNotificationPolicy
{
    private readonly IEnumerable<INotificationChannel> _channels;

    public IncidentNotificationPolicy(IEnumerable<INotificationChannel> channels) => _channels = channels;

    public async Task NotifyAsync(Incident incident, CancellationToken ct)
    {
        var notification = new Notification(
            Subject: $"[{incident.Severity}] {incident.Service}",
            Body: incident.Summary,
            DeduplicationKey: $"incident:{incident.Id}");

        foreach (var channel in _channels.Where(x => x.CanDeliver(incident)))
            await channel.DeliverAsync(notification, ct);
    }
}
```

### Adapters for different delivery systems

```csharp
public sealed class SmtpEmailChannel : INotificationChannel
{
    private readonly IEmailTransport _transport;
    public SmtpEmailChannel(IEmailTransport transport) => _transport = transport;
    public string ChannelName => "email";
    public bool CanDeliver(Incident incident) => incident.Severity >= IncidentSeverity.High;

    public Task DeliverAsync(Notification notification, CancellationToken ct) =>
        _transport.SendAsync("on-call@example.com", notification.Subject, notification.Body, ct);
}

public sealed class TwilioSmsChannel : INotificationChannel
{
    private readonly TwilioRestClient _client;
    public TwilioSmsChannel(TwilioRestClient client) => _client = client;
    public string ChannelName => "sms";
    public bool CanDeliver(Incident incident) => incident.Severity == IncidentSeverity.Critical;

    public Task DeliverAsync(Notification notification, CancellationToken ct) =>
        _client.SendMessageAsync(to: "+15550100", body: notification.Subject + " — " + notification.Body, ct);
}
```

Adding Microsoft Teams, PagerDuty, or a customer-specific webhook is a new `INotificationChannel`. The incident policy remains independent of all SDKs.

---

## Example 3 — Pricing policy depends on a market-data port, not an HTTP client

Pricing is high-level domain policy. Calling a particular vendor endpoint is infrastructure.

### Domain port and pricing service

```csharp
public sealed record Product(string Sku, decimal BasePrice, string Currency, string Segment);
public sealed record Customer(string Id, string Tier, string CountryCode);
public sealed record PriceQuote(decimal Amount, string Currency, string Source);

public interface IExchangeRateProvider
{
    Task<decimal> GetRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct);
}

public interface IDiscountPolicy
{
    bool AppliesTo(Customer customer, Product product);
    decimal GetDiscountPercentage(Customer customer, Product product);
}

public sealed class QuotePriceService
{
    private readonly IExchangeRateProvider _rates;
    private readonly IEnumerable<IDiscountPolicy> _discounts;
    private readonly TimeProvider _clock;

    public QuotePriceService(IExchangeRateProvider rates, IEnumerable<IDiscountPolicy> discounts, TimeProvider clock) =>
        (_rates, _discounts, _clock) = (rates, discounts, clock);

    public async Task<PriceQuote> QuoteAsync(Customer customer, Product product, string requestedCurrency, CancellationToken ct)
    {
        var discount = _discounts.Where(x => x.AppliesTo(customer, product))
            .Sum(x => x.GetDiscountPercentage(customer, product));
        var discounted = product.BasePrice * (1m - Math.Min(discount, 0.80m));

        var date = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var rate = await _rates.GetRateAsync(product.Currency, requestedCurrency, date, ct);
        return new PriceQuote(decimal.Round(discounted * rate, 2), requestedCurrency, "pricing-policy-v3");
    }
}
```

### Adapter: external market-data provider

```csharp
public sealed class MarketDataHttpExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    public MarketDataHttpExchangeRateProvider(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<decimal> GetRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct)
    {
        var response = await _httpClient.GetFromJsonAsync<MarketDataResponse>(
            $"rates?base={baseCurrency}&quote={quoteCurrency}&date={date:yyyy-MM-dd}", ct);

        return response?.Rate ?? throw new InvalidOperationException("Exchange rate was not returned.");
    }

    private sealed record MarketDataResponse(decimal Rate);
}

public sealed class GoldTierDiscountPolicy : IDiscountPolicy
{
    public bool AppliesTo(Customer c, Product p) => c.Tier == "Gold";
    public decimal GetDiscountPercentage(Customer c, Product p) => 0.15m;
}
```

The pricing service never receives an `HttpClient`, an endpoint URL, or a vendor DTO. Its tests can use a deterministic exchange-rate fake and `FakeTimeProvider`.

---

## Example 4 — Audit policy depends on an event sink, not Kafka or a database

Compliance policy decides that an action must be audited. Infrastructure decides whether the record is published to Kafka, stored in a WORM store, or written to a SIEM.

### Application-owned audit port

```csharp
public sealed record AuditEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string TenantId,
    string ActorId,
    string Action,
    string ResourceType,
    string ResourceId,
    IReadOnlyDictionary<string, string> Metadata);

public interface IAuditEventSink
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken ct);
}

public sealed class SensitiveDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IAuditEventSink _audit;
    private readonly TimeProvider _clock;

    public SensitiveDocumentService(IDocumentRepository documents, IAuditEventSink audit, TimeProvider clock) =>
        (_documents, _audit, _clock) = (documents, audit, clock);

    public async Task<Document> ReadAsync(string documentId, RequestIdentity identity, CancellationToken ct)
    {
        var document = await _documents.GetAuthorizedAsync(documentId, identity, ct);
        await _audit.AppendAsync(new AuditEvent(
            Guid.NewGuid(), _clock.GetUtcNow(), identity.TenantId, identity.UserId,
            "document.read", "document", documentId,
            new Dictionary<string, string> { ["classification"] = document.Classification }), ct);
        return document;
    }
}
```

### Kafka adapter with an outbox-friendly envelope

```csharp
public sealed class KafkaAuditEventSink : IAuditEventSink
{
    private readonly IProducer<string, byte[]> _producer;
    private readonly JsonSerializerOptions _json;

    public KafkaAuditEventSink(IProducer<string, byte[]> producer, JsonSerializerOptions json) =>
        (_producer, _json) = (producer, json);

    public Task AppendAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(auditEvent, _json);
        return _producer.ProduceAsync("audit-events-v1", new Message<string, byte[]>
        {
            Key = auditEvent.TenantId,
            Value = payload,
            Headers = new Headers { { "event-type", Encoding.UTF8.GetBytes("audit.v1") } }
        }, ct);
    }
}
```

For strong consistency, replace the direct Kafka adapter with an `OutboxAuditEventSink` that writes within the same database transaction; a background dispatcher later publishes the event. `SensitiveDocumentService` does not change.

---

## Composition root: the only place that knows concrete implementations

```csharp
// Program.cs or a dedicated Infrastructure composition module
services.AddScoped<PlaceOrderHandler>();
services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
services.AddScoped<IPaymentAuthorizer, StripePaymentAuthorizer>();

services.AddScoped<IncidentNotificationPolicy>();
services.AddScoped<INotificationChannel, SmtpEmailChannel>();
services.AddScoped<INotificationChannel, TwilioSmsChannel>();

services.AddScoped<QuotePriceService>();
services.AddHttpClient<IExchangeRateProvider, MarketDataHttpExchangeRateProvider>(client =>
    client.BaseAddress = new Uri("https://market-data.internal/"));
services.AddScoped<IDiscountPolicy, GoldTierDiscountPolicy>();

services.AddScoped<SensitiveDocumentService>();
services.AddScoped<IAuditEventSink, KafkaAuditEventSink>();
services.AddSingleton(TimeProvider.System);
```

The composition root is allowed to reference concrete SDK clients, database contexts, and configuration. Application and domain code should not.

## DIP checklist

| Question | Healthy answer |
|---|---|
| Who owns the abstraction? | The high-level application/domain policy that needs the capability |
| Does the interface expose SDK types? | No; translate vendor DTOs at the infrastructure boundary |
| Where are implementation choices made? | In the composition root / dependency-injection configuration |
| Can the policy be unit tested without network or database access? | Yes, with a focused fake or mock of its port |
| Can a vendor be swapped? | Yes; create a new adapter that implements the same application-owned port |
| Is every interface justified? | No—use one at a meaningful volatile boundary, not around every class |

**DIP is about dependency direction, not interface count.** Let business policy define the capabilities it needs; force infrastructure details to conform to those capabilities.
