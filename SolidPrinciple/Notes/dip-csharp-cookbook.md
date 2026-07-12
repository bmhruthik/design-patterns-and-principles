# Dependency Inversion Principle (DIP) in C# - Cookbook

> A practical reference with examples that move from basic refactoring to production-style patterns.

## 1. DIP in One Minute

The Dependency Inversion Principle says:

1. High-level modules should not depend on low-level modules. Both should depend on abstractions.
2. Abstractions should not depend on details. Details should depend on abstractions.

In C# terms, this often means:

- Depend on interfaces or abstract classes, not concrete classes.
- Inject dependencies through constructors, methods, or factories.
- Keep business logic independent from infrastructure details such as databases, email providers, file systems, APIs, loggers, and clocks.

## 2. Mental Model

Without DIP:

```text
OrderService -> SqlOrderRepository
```

The high-level policy, `OrderService`, directly knows the low-level detail, `SqlOrderRepository`.

With DIP:

```text
OrderService -> IOrderRepository <- SqlOrderRepository
```

`OrderService` depends on an abstraction. The SQL implementation is just one replaceable detail.

## 3. Recipe 1 - Basic Notification Example

### Problem

`OrderService` directly creates and uses `EmailSender`.

```csharp
public class EmailSender
{
    public void Send(string to, string subject, string body)
    {
        Console.WriteLine($"Email sent to {to}: {subject}");
    }
}

public class OrderService
{
    public void PlaceOrder(string customerEmail)
    {
        // Order logic...

        var emailSender = new EmailSender();
        emailSender.Send(customerEmail, "Order placed", "Your order was placed.");
    }
}
```

### DIP Version

```csharp
public interface INotificationSender
{
    void Send(string to, string subject, string body);
}

public class EmailSender : INotificationSender
{
    public void Send(string to, string subject, string body)
    {
        Console.WriteLine($"Email sent to {to}: {subject}");
    }
}

public class OrderService
{
    private readonly INotificationSender _notificationSender;

    public OrderService(INotificationSender notificationSender)
    {
        _notificationSender = notificationSender;
    }

    public void PlaceOrder(string customerEmail)
    {
        // Order logic...

        _notificationSender.Send(
            customerEmail,
            "Order placed",
            "Your order was placed.");
    }
}
```

### Usage

```csharp
INotificationSender sender = new EmailSender();
var orderService = new OrderService(sender);

orderService.PlaceOrder("customer@example.com");
```

### Why This Is Better

- `OrderService` does not care whether the notification is email, SMS, push, or a test double.
- You can test `OrderService` without sending real emails.
- The high-level workflow is isolated from infrastructure details.

## 4. Recipe 2 - Swapping Implementations

Once the high-level code depends on an abstraction, new implementations become easy.

```csharp
public class SmsSender : INotificationSender
{
    public void Send(string to, string subject, string body)
    {
        Console.WriteLine($"SMS sent to {to}: {body}");
    }
}

public class PushNotificationSender : INotificationSender
{
    public void Send(string to, string subject, string body)
    {
        Console.WriteLine($"Push notification sent to {to}: {subject}");
    }
}
```

Now the caller chooses the implementation:

```csharp
var service = new OrderService(new SmsSender());
service.PlaceOrder("+15551234567");
```

### Use When

- The same business workflow can work with multiple delivery mechanisms.
- You want to replace a concrete provider without rewriting business logic.

## 5. Recipe 3 - Repository Dependency

### Problem

Business logic directly depends on SQL.

```csharp
public class CustomerService
{
    private readonly SqlCustomerRepository _repository = new();

    public Customer GetCustomer(int id)
    {
        return _repository.FindById(id);
    }
}

public class SqlCustomerRepository
{
    public Customer FindById(int id)
    {
        // Query SQL Server...
        return new Customer(id, "Asha");
    }
}

public record Customer(int Id, string Name);
```

### DIP Version

```csharp
public interface ICustomerRepository
{
    Customer FindById(int id);
}

public class SqlCustomerRepository : ICustomerRepository
{
    public Customer FindById(int id)
    {
        // Query SQL Server...
        return new Customer(id, "Asha");
    }
}

public class CustomerService
{
    private readonly ICustomerRepository _repository;

    public CustomerService(ICustomerRepository repository)
    {
        _repository = repository;
    }

    public Customer GetCustomer(int id)
    {
        return _repository.FindById(id);
    }
}

public record Customer(int Id, string Name);
```

### Test-Friendly Fake

```csharp
public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<int, Customer> _customers = new()
    {
        [1] = new Customer(1, "Asha"),
        [2] = new Customer(2, "Noah")
    };

    public Customer FindById(int id)
    {
        return _customers[id];
    }
}
```

### Usage

```csharp
var service = new CustomerService(new InMemoryCustomerRepository());
var customer = service.GetCustomer(1);
```

## 6. Recipe 4 - Constructor Injection

Constructor injection is the most common DIP technique in C#.

```csharp
public interface IPaymentGateway
{
    PaymentResult Charge(decimal amount, string currency);
}

public class CheckoutService
{
    private readonly IPaymentGateway _paymentGateway;

    public CheckoutService(IPaymentGateway paymentGateway)
    {
        _paymentGateway = paymentGateway;
    }

    public PaymentResult Checkout(decimal total)
    {
        if (total <= 0)
        {
            return PaymentResult.Failed("Total must be greater than zero.");
        }

        return _paymentGateway.Charge(total, "USD");
    }
}

public record PaymentResult(bool Succeeded, string? Error)
{
    public static PaymentResult Success() => new(true, null);

    public static PaymentResult Failed(string error) => new(false, error);
}
```

### Concrete Implementation

```csharp
public class StripePaymentGateway : IPaymentGateway
{
    public PaymentResult Charge(decimal amount, string currency)
    {
        // Call Stripe API...
        return PaymentResult.Success();
    }
}
```

### Why Constructor Injection Is Preferred

- Required dependencies are explicit.
- Objects cannot be created in an invalid half-configured state.
- Tests can provide controlled dependencies easily.

## 7. Recipe 5 - Method Injection

Use method injection when the dependency is needed only for one operation.

```csharp
public interface IReportExporter
{
    void Export(string content);
}

public class ReportService
{
    public void GenerateMonthlyReport(IReportExporter exporter)
    {
        var report = "Monthly revenue: $42,000";
        exporter.Export(report);
    }
}
```

### Use When

- The dependency is not part of the object's long-term identity.
- Different calls may use different implementations.

### Avoid When

- Most methods need the same dependency.
- The method signature becomes noisy with many injected services.

## 8. Recipe 6 - Property Injection

Property injection is useful for optional dependencies, but use it carefully.

```csharp
public interface IAuditLogger
{
    void Log(string message);
}

public class NullAuditLogger : IAuditLogger
{
    public void Log(string message)
    {
        // Intentionally do nothing.
    }
}

public class ProfileService
{
    public IAuditLogger AuditLogger { get; set; } = new NullAuditLogger();

    public void UpdateDisplayName(int userId, string displayName)
    {
        // Update profile...

        AuditLogger.Log($"User {userId} changed display name.");
    }
}
```

### Use When

- The dependency is optional.
- A safe default exists, such as a null object.

### Avoid When

- The dependency is required for correct behavior.
- Forgetting to set it would cause runtime failures.

## 9. Recipe 7 - Using the Built-in .NET DI Container

Modern .NET apps usually use `Microsoft.Extensions.DependencyInjection`.

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddScoped<ICustomerRepository, SqlCustomerRepository>();
services.AddScoped<CustomerService>();

var provider = services.BuildServiceProvider();
var customerService = provider.GetRequiredService<CustomerService>();

var customer = customerService.GetCustomer(1);
```

### Common Lifetimes

| Lifetime | Meaning | Typical Use |
|---|---|---|
| `Transient` | New instance every time | Lightweight stateless services |
| `Scoped` | One instance per request/scope | Repositories, EF Core DbContext users |
| `Singleton` | One instance for app lifetime | Configuration, caches, stateless utilities |

### ASP.NET Core Example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
builder.Services.AddScoped<OrderService>();

var app = builder.Build();
```

## 10. Recipe 8 - DIP with Controllers

### Without DIP

```csharp
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public IActionResult Create(CreateOrderRequest request)
    {
        var repository = new SqlOrderRepository();
        var emailSender = new SendGridEmailSender();
        var service = new OrderService(repository, emailSender);

        service.CreateOrder(request.CustomerEmail, request.Total);

        return Ok();
    }
}
```

### With DIP

```csharp
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public IActionResult Create(CreateOrderRequest request)
    {
        _orderService.CreateOrder(request.CustomerEmail, request.Total);
        return Ok();
    }
}
```

### Registration

```csharp
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.AddScoped<INotificationSender, SendGridNotificationSender>();
builder.Services.AddScoped<OrderService>();
```

### Benefit

The controller is now a thin transport layer. It does not decide how orders are stored or how notifications are sent.

## 11. Recipe 9 - Testing with Fakes

DIP makes unit tests simple because you can inject fake dependencies.

```csharp
public class FakePaymentGateway : IPaymentGateway
{
    public decimal LastAmountCharged { get; private set; }

    public PaymentResult Charge(decimal amount, string currency)
    {
        LastAmountCharged = amount;
        return PaymentResult.Success();
    }
}

public class CheckoutServiceTests
{
    [Fact]
    public void Checkout_Charges_Total_Amount()
    {
        var gateway = new FakePaymentGateway();
        var service = new CheckoutService(gateway);

        var result = service.Checkout(99.99m);

        Assert.True(result.Succeeded);
        Assert.Equal(99.99m, gateway.LastAmountCharged);
    }
}
```

### Key Idea

Tests should verify the high-level policy without hitting real payment providers, databases, message queues, or file systems.

## 12. Recipe 10 - DIP with Time

Directly using `DateTime.UtcNow` can make code hard to test.

### Problem

```csharp
public class SubscriptionService
{
    public bool IsExpired(DateTime expiresAt)
    {
        return expiresAt <= DateTime.UtcNow;
    }
}
```

### DIP Version

```csharp
public interface IClock
{
    DateTime UtcNow { get; }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public class SubscriptionService
{
    private readonly IClock _clock;

    public SubscriptionService(IClock clock)
    {
        _clock = clock;
    }

    public bool IsExpired(DateTime expiresAt)
    {
        return expiresAt <= _clock.UtcNow;
    }
}
```

### Test Clock

```csharp
public class FixedClock : IClock
{
    public FixedClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

var service = new SubscriptionService(
    new FixedClock(new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc)));

var expired = service.IsExpired(
    new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
```

## 13. Recipe 11 - DIP with File System Access

### Problem

```csharp
public class InvoiceReader
{
    public string ReadInvoice(string path)
    {
        return File.ReadAllText(path);
    }
}
```

This is difficult to test without creating real files.

### DIP Version

```csharp
public interface IFileReader
{
    string ReadAllText(string path);
}

public class DiskFileReader : IFileReader
{
    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
}

public class InvoiceReader
{
    private readonly IFileReader _fileReader;

    public InvoiceReader(IFileReader fileReader)
    {
        _fileReader = fileReader;
    }

    public string ReadInvoice(string path)
    {
        return _fileReader.ReadAllText(path);
    }
}
```

### Fake Implementation

```csharp
public class FakeFileReader : IFileReader
{
    private readonly Dictionary<string, string> _files = new();

    public void AddFile(string path, string content)
    {
        _files[path] = content;
    }

    public string ReadAllText(string path)
    {
        return _files[path];
    }
}
```

## 14. Recipe 12 - DIP with External APIs

### Goal

Keep business logic independent from HTTP details.

```csharp
public interface IExchangeRateProvider
{
    Task<decimal> GetRateAsync(string fromCurrency, string toCurrency);
}

public class HttpExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;

    public HttpExchangeRateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal> GetRateAsync(string fromCurrency, string toCurrency)
    {
        var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(
            $"/rates?from={fromCurrency}&to={toCurrency}");

        return response?.Rate
            ?? throw new InvalidOperationException("Exchange rate was missing.");
    }
}

public record ExchangeRateResponse(decimal Rate);
```

### Business Service

```csharp
public class CurrencyConverter
{
    private readonly IExchangeRateProvider _exchangeRateProvider;

    public CurrencyConverter(IExchangeRateProvider exchangeRateProvider)
    {
        _exchangeRateProvider = exchangeRateProvider;
    }

    public async Task<decimal> ConvertAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency)
    {
        var rate = await _exchangeRateProvider.GetRateAsync(fromCurrency, toCurrency);
        return amount * rate;
    }
}
```

### Registration with Typed HttpClient

```csharp
builder.Services.AddHttpClient<IExchangeRateProvider, HttpExchangeRateProvider>(
    client =>
    {
        client.BaseAddress = new Uri("https://api.example.com");
    });

builder.Services.AddScoped<CurrencyConverter>();
```

## 15. Recipe 13 - Factory Abstraction

Sometimes the high-level module needs to create something at runtime. If direct construction leaks details, inject a factory.

```csharp
public interface IInvoiceFormatter
{
    string Format(Invoice invoice);
}

public class PdfInvoiceFormatter : IInvoiceFormatter
{
    public string Format(Invoice invoice)
    {
        return $"PDF invoice for {invoice.CustomerName}";
    }
}

public class HtmlInvoiceFormatter : IInvoiceFormatter
{
    public string Format(Invoice invoice)
    {
        return $"<h1>Invoice for {invoice.CustomerName}</h1>";
    }
}

public interface IInvoiceFormatterFactory
{
    IInvoiceFormatter Create(InvoiceFormat format);
}

public class InvoiceFormatterFactory : IInvoiceFormatterFactory
{
    public IInvoiceFormatter Create(InvoiceFormat format)
    {
        return format switch
        {
            InvoiceFormat.Pdf => new PdfInvoiceFormatter(),
            InvoiceFormat.Html => new HtmlInvoiceFormatter(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }
}

public class InvoiceService
{
    private readonly IInvoiceFormatterFactory _formatterFactory;

    public InvoiceService(IInvoiceFormatterFactory formatterFactory)
    {
        _formatterFactory = formatterFactory;
    }

    public string GenerateInvoice(Invoice invoice, InvoiceFormat format)
    {
        var formatter = _formatterFactory.Create(format);
        return formatter.Format(invoice);
    }
}

public record Invoice(string CustomerName, decimal Total);

public enum InvoiceFormat
{
    Pdf,
    Html
}
```

### Note

The service knows it needs a formatter. It does not know how concrete formatters are created.

## 16. Recipe 14 - Decorator Pattern with DIP

Decorators let you add behavior around an abstraction without changing the core implementation.

```csharp
public interface IProductRepository
{
    Product GetById(int id);
}

public class SqlProductRepository : IProductRepository
{
    public Product GetById(int id)
    {
        // Query database...
        return new Product(id, "Keyboard");
    }
}

public class CachedProductRepository : IProductRepository
{
    private readonly IProductRepository _inner;
    private readonly Dictionary<int, Product> _cache = new();

    public CachedProductRepository(IProductRepository inner)
    {
        _inner = inner;
    }

    public Product GetById(int id)
    {
        if (_cache.TryGetValue(id, out var cachedProduct))
        {
            return cachedProduct;
        }

        var product = _inner.GetById(id);
        _cache[id] = product;

        return product;
    }
}

public record Product(int Id, string Name);
```

### Manual Wiring

```csharp
IProductRepository repository =
    new CachedProductRepository(new SqlProductRepository());
```

### Why This Is Advanced DIP

The cache depends on the same abstraction as the service. You can wrap SQL, API, or in-memory repositories without changing the consumers.

## 17. Recipe 15 - Strategy Pattern with DIP

Use strategy when business behavior varies by rule or policy.

```csharp
public interface IDiscountStrategy
{
    decimal ApplyDiscount(decimal subtotal);
}

public class NoDiscountStrategy : IDiscountStrategy
{
    public decimal ApplyDiscount(decimal subtotal) => subtotal;
}

public class PercentageDiscountStrategy : IDiscountStrategy
{
    private readonly decimal _percentage;

    public PercentageDiscountStrategy(decimal percentage)
    {
        _percentage = percentage;
    }

    public decimal ApplyDiscount(decimal subtotal)
    {
        return subtotal - subtotal * _percentage;
    }
}

public class PricingService
{
    private readonly IDiscountStrategy _discountStrategy;

    public PricingService(IDiscountStrategy discountStrategy)
    {
        _discountStrategy = discountStrategy;
    }

    public decimal CalculateTotal(decimal subtotal)
    {
        return _discountStrategy.ApplyDiscount(subtotal);
    }
}
```

### Usage

```csharp
var service = new PricingService(new PercentageDiscountStrategy(0.10m));
var total = service.CalculateTotal(100m); // 90
```

## 18. Recipe 16 - Multiple Implementations Selected by Key

In real applications, you may need to choose between multiple implementations at runtime.

```csharp
public interface IShippingCalculator
{
    ShippingCarrier Carrier { get; }

    decimal Calculate(decimal weightInKg);
}

public class FedExShippingCalculator : IShippingCalculator
{
    public ShippingCarrier Carrier => ShippingCarrier.FedEx;

    public decimal Calculate(decimal weightInKg) => 10m + weightInKg * 2m;
}

public class UpsShippingCalculator : IShippingCalculator
{
    public ShippingCarrier Carrier => ShippingCarrier.Ups;

    public decimal Calculate(decimal weightInKg) => 8m + weightInKg * 2.5m;
}

public class ShippingService
{
    private readonly IReadOnlyDictionary<ShippingCarrier, IShippingCalculator> _calculators;

    public ShippingService(IEnumerable<IShippingCalculator> calculators)
    {
        _calculators = calculators.ToDictionary(calculator => calculator.Carrier);
    }

    public decimal CalculateShipping(ShippingCarrier carrier, decimal weightInKg)
    {
        if (!_calculators.TryGetValue(carrier, out var calculator))
        {
            throw new NotSupportedException($"Carrier {carrier} is not supported.");
        }

        return calculator.Calculate(weightInKg);
    }
}

public enum ShippingCarrier
{
    FedEx,
    Ups
}
```

### Registration

```csharp
builder.Services.AddScoped<IShippingCalculator, FedExShippingCalculator>();
builder.Services.AddScoped<IShippingCalculator, UpsShippingCalculator>();
builder.Services.AddScoped<ShippingService>();
```

### Use When

- You have multiple algorithms behind one abstraction.
- The chosen algorithm depends on input.

## 19. Recipe 17 - Options Pattern plus DIP

Configuration is also a dependency. Avoid hardcoding settings in services.

```csharp
public class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;

    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
}
```

```csharp
public class RetryPolicy
{
    private readonly RetryOptions _options;

    public RetryPolicy(IOptions<RetryOptions> options)
    {
        _options = options.Value;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < _options.MaxAttempts)
            {
                lastException = ex;
                await Task.Delay(_options.Delay);
            }
        }

        throw lastException ?? new InvalidOperationException("Retry failed.");
    }
}
```

### Registration

```csharp
builder.Services.Configure<RetryOptions>(
    builder.Configuration.GetSection("Retry"));

builder.Services.AddScoped<RetryPolicy>();
```

### Required Namespace

```csharp
using Microsoft.Extensions.Options;
```

## 20. Recipe 18 - Domain Service Independent from Infrastructure

This is a more realistic example.

### Domain Abstractions

```csharp
public interface IOrderRepository
{
    Task SaveAsync(Order order);
}

public interface IPaymentProcessor
{
    Task<PaymentReceipt> ChargeAsync(Money amount, string paymentToken);
}

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent);
}
```

### Domain Service

```csharp
public class PlaceOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IPaymentProcessor _payments;
    private readonly IEventPublisher _events;

    public PlaceOrderService(
        IOrderRepository orders,
        IPaymentProcessor payments,
        IEventPublisher events)
    {
        _orders = orders;
        _payments = payments;
        _events = events;
    }

    public async Task<Order> PlaceAsync(PlaceOrderCommand command)
    {
        var order = Order.Create(command.CustomerId, command.Items);
        var receipt = await _payments.ChargeAsync(order.Total, command.PaymentToken);

        order.MarkPaid(receipt.TransactionId);

        await _orders.SaveAsync(order);
        await _events.PublishAsync(new OrderPlaced(order.Id, order.CustomerId));

        return order;
    }
}
```

### Supporting Types

```csharp
public record PlaceOrderCommand(
    int CustomerId,
    IReadOnlyList<OrderItem> Items,
    string PaymentToken);

public record OrderPlaced(Guid OrderId, int CustomerId);

public record OrderItem(string Sku, int Quantity, Money Price);

public record Money(decimal Amount, string Currency);

public record PaymentReceipt(string TransactionId);

public class Order
{
    private readonly List<OrderItem> _items;

    private Order(int customerId, IReadOnlyList<OrderItem> items)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        _items = items.ToList();
        Total = new Money(_items.Sum(item => item.Price.Amount * item.Quantity), "USD");
    }

    public Guid Id { get; }

    public int CustomerId { get; }

    public Money Total { get; }

    public string? PaymentTransactionId { get; private set; }

    public static Order Create(int customerId, IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Order must contain at least one item.", nameof(items));
        }

        return new Order(customerId, items);
    }

    public void MarkPaid(string transactionId)
    {
        PaymentTransactionId = transactionId;
    }
}
```

### Infrastructure Implementations

```csharp
public class EfOrderRepository : IOrderRepository
{
    private readonly AppDbContext _dbContext;

    public EfOrderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveAsync(Order order)
    {
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
    }
}

public class StripePaymentProcessor : IPaymentProcessor
{
    public Task<PaymentReceipt> ChargeAsync(Money amount, string paymentToken)
    {
        // Call Stripe...
        return Task.FromResult(new PaymentReceipt(Guid.NewGuid().ToString("N")));
    }
}

public class RabbitMqEventPublisher : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent domainEvent)
    {
        // Publish message...
        return Task.CompletedTask;
    }
}
```

### Registration

```csharp
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IPaymentProcessor, StripePaymentProcessor>();
builder.Services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddScoped<PlaceOrderService>();
```

### What DIP Achieves Here

- The domain workflow does not depend on Entity Framework.
- The domain workflow does not depend on Stripe.
- The domain workflow does not depend on RabbitMQ.
- Infrastructure details can be replaced without rewriting order placement rules.

## 21. Recipe 19 - Ports and Adapters Style

DIP is the foundation behind ports and adapters, also called hexagonal architecture.

```text
Application Core
  PlaceOrderService
  IOrderRepository
  IPaymentProcessor
  IEventPublisher

Adapters
  EfOrderRepository
  StripePaymentProcessor
  RabbitMqEventPublisher
  OrdersController
```

The application core owns the interfaces. Infrastructure adapters implement them.

### Folder Example

```text
src/
  MyApp.Application/
    Orders/
      PlaceOrderService.cs
      IOrderRepository.cs
      IPaymentProcessor.cs
      IEventPublisher.cs

  MyApp.Domain/
    Orders/
      Order.cs
      OrderItem.cs
      Money.cs

  MyApp.Infrastructure/
    Persistence/
      EfOrderRepository.cs
    Payments/
      StripePaymentProcessor.cs
    Messaging/
      RabbitMqEventPublisher.cs

  MyApp.Api/
    Controllers/
      OrdersController.cs
    Program.cs
```

### Dependency Direction

```text
Api -> Application -> Domain
Infrastructure -> Application
Infrastructure -> Domain
```

The important rule: `Application` should not depend on `Infrastructure`.

## 22. Recipe 20 - Avoiding Interface Explosion

DIP does not mean every class needs an interface.

### Probably Worth an Interface

- External API clients
- Database repositories
- File system access
- Email, SMS, push, or message bus senders
- Clocks and random number providers
- Payment processors
- Strategies with multiple implementations
- Code you need to fake in tests

### Usually Not Worth an Interface

- Simple data models
- Pure functions with no external effects
- Small internal helper classes with one implementation and no test pain
- Classes that are already stable and deterministic

### Bad Example

```csharp
public interface IStringTrimmer
{
    string Trim(string value);
}

public class StringTrimmer : IStringTrimmer
{
    public string Trim(string value) => value.Trim();
}
```

This adds ceremony without meaningful flexibility.

## 23. Recipe 21 - Anti-Pattern: Service Locator

Service locator hides dependencies.

```csharp
public class BadOrderService
{
    public void PlaceOrder()
    {
        var repository = ServiceLocator.Get<IOrderRepository>();
        var sender = ServiceLocator.Get<INotificationSender>();

        // Business logic...
    }
}
```

### Why It Is a Problem

- Dependencies are invisible from the constructor.
- Tests are harder to reason about.
- Runtime failures happen when registrations are missing.
- The class lies about what it needs.

### Prefer Constructor Injection

```csharp
public class GoodOrderService
{
    private readonly IOrderRepository _repository;
    private readonly INotificationSender _sender;

    public GoodOrderService(
        IOrderRepository repository,
        INotificationSender sender)
    {
        _repository = repository;
        _sender = sender;
    }
}
```

## 24. Recipe 22 - Anti-Pattern: Injecting the Container

Avoid injecting `IServiceProvider` into regular services.

```csharp
public class BadReportService
{
    private readonly IServiceProvider _serviceProvider;

    public BadReportService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Generate()
    {
        var exporter = _serviceProvider.GetRequiredService<IReportExporter>();
        exporter.Export("Report content");
    }
}
```

### Better

```csharp
public class GoodReportService
{
    private readonly IReportExporter _exporter;

    public GoodReportService(IReportExporter exporter)
    {
        _exporter = exporter;
    }

    public void Generate()
    {
        _exporter.Export("Report content");
    }
}
```

### Exception

Using `IServiceProvider` can be acceptable in infrastructure composition code, framework glue, or carefully designed factories. It should not leak into normal business services.

## 25. Recipe 23 - Anti-Pattern: Depending on Concrete Framework Types

### Problem

```csharp
public class InvoiceService
{
    private readonly AppDbContext _dbContext;

    public InvoiceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateInvoiceAsync(Invoice invoice)
    {
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();
    }
}
```

This may be fine in simple CRUD apps. But if `InvoiceService` contains important business rules, it is better to hide persistence behind an abstraction.

### Better for Business Logic

```csharp
public interface IInvoiceRepository
{
    Task SaveAsync(Invoice invoice);
}

public class InvoiceService
{
    private readonly IInvoiceRepository _invoices;

    public InvoiceService(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public async Task CreateInvoiceAsync(Invoice invoice)
    {
        // Important business rules...

        await _invoices.SaveAsync(invoice);
    }
}
```

## 26. Recipe 24 - Advanced: Open Generic Abstractions

Open generics are useful for cross-cutting infrastructure.

```csharp
public interface IRepository<TEntity>
    where TEntity : class
{
    Task<TEntity?> GetByIdAsync(Guid id);

    Task SaveAsync(TEntity entity);
}

public class EfRepository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    private readonly AppDbContext _dbContext;

    public EfRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TEntity?> GetByIdAsync(Guid id)
    {
        return _dbContext.Set<TEntity>().FindAsync(id).AsTask();
    }

    public async Task SaveAsync(TEntity entity)
    {
        _dbContext.Set<TEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();
    }
}
```

### Registration

```csharp
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
```

### Caution

Generic repositories can become too generic. Prefer specific repositories when queries and domain language matter.

## 27. Recipe 25 - Advanced: Pipeline Behaviors

In CQRS-style applications, DIP can power pipelines for validation, logging, authorization, and transactions.

```csharp
public interface ICommandHandler<TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command);
}

public class CreateUserCommand
{
    public required string Email { get; init; }

    public required string Name { get; init; }
}

public record CreateUserResult(Guid UserId);
```

### Handler

```csharp
public class CreateUserHandler
    : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IUserRepository _users;

    public CreateUserHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<CreateUserResult> HandleAsync(CreateUserCommand command)
    {
        var user = new User(Guid.NewGuid(), command.Email, command.Name);
        await _users.SaveAsync(user);

        return new CreateUserResult(user.Id);
    }
}
```

### Decorator

```csharp
public class LoggingCommandHandler<TCommand, TResult>
    : ICommandHandler<TCommand, TResult>
{
    private readonly ICommandHandler<TCommand, TResult> _inner;
    private readonly ILogger<LoggingCommandHandler<TCommand, TResult>> _logger;

    public LoggingCommandHandler(
        ICommandHandler<TCommand, TResult> inner,
        ILogger<LoggingCommandHandler<TCommand, TResult>> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<TResult> HandleAsync(TCommand command)
    {
        _logger.LogInformation("Handling command {CommandType}", typeof(TCommand).Name);

        var result = await _inner.HandleAsync(command);

        _logger.LogInformation("Handled command {CommandType}", typeof(TCommand).Name);

        return result;
    }
}
```

### Benefit

The handler only contains the use case. Logging, validation, transactions, and authorization can be added around it.

## 28. Recipe 26 - Advanced: DIP in a Plugin Architecture

Suppose your app supports export plugins.

```csharp
public interface IExportPlugin
{
    string Format { get; }

    Task ExportAsync(ExportDocument document, Stream destination);
}

public record ExportDocument(string Title, string Body);
```

### Plugins

```csharp
public class PdfExportPlugin : IExportPlugin
{
    public string Format => "pdf";

    public Task ExportAsync(ExportDocument document, Stream destination)
    {
        // Write PDF...
        return Task.CompletedTask;
    }
}

public class MarkdownExportPlugin : IExportPlugin
{
    public string Format => "md";

    public async Task ExportAsync(ExportDocument document, Stream destination)
    {
        using var writer = new StreamWriter(destination, leaveOpen: true);
        await writer.WriteAsync($"# {document.Title}\n\n{document.Body}");
    }
}
```

### Plugin Host

```csharp
public class ExportService
{
    private readonly IReadOnlyDictionary<string, IExportPlugin> _plugins;

    public ExportService(IEnumerable<IExportPlugin> plugins)
    {
        _plugins = plugins.ToDictionary(
            plugin => plugin.Format,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task ExportAsync(
        string format,
        ExportDocument document,
        Stream destination)
    {
        if (!_plugins.TryGetValue(format, out var plugin))
        {
            throw new NotSupportedException($"Export format '{format}' is not supported.");
        }

        return plugin.ExportAsync(document, destination);
    }
}
```

### Registration

```csharp
builder.Services.AddScoped<IExportPlugin, PdfExportPlugin>();
builder.Services.AddScoped<IExportPlugin, MarkdownExportPlugin>();
builder.Services.AddScoped<ExportService>();
```

## 29. Recipe 27 - Advanced: Background Worker with DIP

```csharp
public interface IJobQueue
{
    Task<Job?> DequeueAsync(CancellationToken cancellationToken);
}

public interface IJobProcessor
{
    Task ProcessAsync(Job job, CancellationToken cancellationToken);
}

public record Job(Guid Id, string Type, string Payload);
```

```csharp
public class JobWorker : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IJobProcessor _processor;
    private readonly ILogger<JobWorker> _logger;

    public JobWorker(
        IJobQueue queue,
        IJobProcessor processor,
        ILogger<JobWorker> logger)
    {
        _queue = queue;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);

            if (job is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            try
            {
                await _processor.ProcessAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process job {JobId}", job.Id);
            }
        }
    }
}
```

### Registration

```csharp
builder.Services.AddSingleton<IJobQueue, AzureStorageJobQueue>();
builder.Services.AddScoped<IJobProcessor, JobProcessor>();
builder.Services.AddHostedService<JobWorker>();
```

### Lifetime Warning

`BackgroundService` is singleton-like. Be careful when injecting scoped services directly. If a worker needs scoped dependencies, inject `IServiceScopeFactory` in the worker and create a scope per job.

## 30. Recipe 28 - Advanced: Scoped Dependencies in Background Workers

```csharp
public class ScopedJobWorker : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedJobWorker(
        IJobQueue queue,
        IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);

            if (job is null)
            {
                continue;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();

            await processor.ProcessAsync(job, stoppingToken);
        }
    }
}
```

This is one of the acceptable cases where framework service resolution appears in infrastructure code.

## 31. DIP Checklist

Ask these questions:

- Does this class create concrete dependencies with `new`?
- Does business logic directly call a database, file system, external API, clock, random generator, or message bus?
- Would this class be hard to unit test without real infrastructure?
- Could a dependency realistically have multiple implementations?
- Is the dependency part of policy or merely a detail?
- Are constructor parameters clearly showing what the class needs?
- Are interfaces owned by the high-level layer that consumes them?

## 32. Refactoring Steps

Use this process when converting existing code to DIP:

1. Identify the high-level policy class.
2. Find the concrete low-level dependency it directly uses.
3. Extract an interface that describes only what the high-level class needs.
4. Make the low-level class implement the interface.
5. Inject the interface into the high-level class.
6. Move object creation to the composition root, such as `Program.cs`, a controller factory, or test setup.
7. Add tests using fakes or mocks.

## 33. Composition Root

The composition root is the place where concrete dependencies are assembled.

Good places:

- `Program.cs` in ASP.NET Core
- Worker service startup
- Console app startup
- Test setup

Avoid spreading object creation across business services.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.AddScoped<INotificationSender, EmailNotificationSender>();
builder.Services.AddScoped<OrderService>();

var app = builder.Build();
```

## 34. DIP vs Dependency Injection

These are related but not identical.

| Concept | Meaning |
|---|---|
| DIP | Design principle: high-level code should depend on abstractions |
| Dependency Injection | Technique: dependencies are supplied from the outside |
| DI Container | Tool: creates objects and wires dependencies automatically |

You can follow DIP without a DI container. A DI container helps manage object graphs in larger applications.

## 35. Practical Rules of Thumb

- Inject behavior, not data.
- Keep interfaces small and consumer-focused.
- Put abstractions near the code that owns the policy.
- Do not create interfaces just because a class exists.
- Avoid hiding dependencies behind service locators.
- Prefer constructor injection for required dependencies.
- Use method injection for operation-specific dependencies.
- Use property injection only for optional dependencies with safe defaults.
- Keep object creation near the application boundary.
- Let infrastructure depend on application abstractions, not the other way around.

## 36. Mini Before-and-After Summary

### Before

```csharp
public class ReportService
{
    public void SendReport()
    {
        var pdf = new PdfGenerator();
        var email = new SmtpEmailClient();

        var content = pdf.Generate();
        email.Send("admin@example.com", content);
    }
}
```

### After

```csharp
public interface IReportGenerator
{
    string Generate();
}

public interface IReportSender
{
    void Send(string recipient, string content);
}

public class ReportService
{
    private readonly IReportGenerator _generator;
    private readonly IReportSender _sender;

    public ReportService(IReportGenerator generator, IReportSender sender)
    {
        _generator = generator;
        _sender = sender;
    }

    public void SendReport()
    {
        var content = _generator.Generate();
        _sender.Send("admin@example.com", content);
    }
}
```

## 37. Final Takeaway

DIP is not about adding interfaces everywhere. It is about protecting important business decisions from unstable implementation details.

Use DIP when a concrete dependency makes code harder to test, harder to replace, or harder to understand. Keep it simple for code that is already stable, deterministic, and local.
