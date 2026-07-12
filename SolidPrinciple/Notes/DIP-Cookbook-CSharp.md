# Dependency Inversion Principle (DIP) in C# — A Cookbook

> **DIP, formally:**
> 1. High-level modules should not depend on low-level modules. Both should depend on abstractions.
> 2. Abstractions should not depend on details. Details should depend on abstractions.
>
> In practice: your business logic should talk to *interfaces*, and the concrete classes (database, email, file system, HTTP...) plug into those interfaces from the outside. This is what makes swapping implementations, unit testing, and change-tolerant code possible.

Each recipe below builds on the last. Compile-and-run in a Console app or paste into LINQPad/a test project.

---

## Recipe 1 (Basic) — The Violation and The Fix

**❌ Violation:** `OrderService` (high-level policy) directly `new`s up `EmailSender` (low-level detail). If you want SMS instead, you must edit `OrderService`.

```csharp
public class EmailSender
{
    public void Send(string to, string message) =>
        Console.WriteLine($"Email to {to}: {message}");
}

public class OrderService
{
    private readonly EmailSender _emailSender = new(); // tight coupling to a concrete class

    public void PlaceOrder(string customerEmail)
    {
        // ... order logic ...
        _emailSender.Send(customerEmail, "Your order has been placed.");
    }
}
```

**✅ Fix:** Introduce an abstraction (`INotifier`). `OrderService` depends only on that. `EmailSender` becomes one possible implementation, injected from outside (constructor injection).

```csharp
public interface INotifier
{
    void Send(string to, string message);
}

public class EmailSender : INotifier
{
    public void Send(string to, string message) =>
        Console.WriteLine($"Email to {to}: {message}");
}

public class OrderService
{
    private readonly INotifier _notifier;

    public OrderService(INotifier notifier) // dependency is "injected"
    {
        _notifier = notifier;
    }

    public void PlaceOrder(string customerEmail)
    {
        // ... order logic ...
        _notifier.Send(customerEmail, "Your order has been placed.");
    }
}

// Composition root (e.g. Main)
var service = new OrderService(new EmailSender());
service.PlaceOrder("alice@example.com");
```

Now `OrderService` doesn't know or care whether it's email, SMS, or a push notification. That decision moved to whoever *constructs* the object.

---

## Recipe 2 (Basic) — Swappable Implementations

Once you depend on an interface, swapping behavior is a one-line change at the composition root — no change to the consumer at all.

```csharp
public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine($"[Console] {message}");
}

public class FileLogger : ILogger
{
    private readonly string _path;
    public FileLogger(string path) => _path = path;

    public void Log(string message) => File.AppendAllText(_path, message + Environment.NewLine);
}

public class InventoryService
{
    private readonly ILogger _logger;
    public InventoryService(ILogger logger) => _logger = logger;

    public void ReduceStock(string sku, int qty)
    {
        // ... business logic ...
        _logger.Log($"Reduced stock for {sku} by {qty}");
    }
}

// Swap at will — InventoryService never changes:
var dev  = new InventoryService(new ConsoleLogger());
var prod = new InventoryService(new FileLogger("app.log"));
```

---

## Recipe 3 (Basic→Intermediate) — Don't Let Interfaces Get Fat (ISP supports DIP)

A common mistake: cramming everything into one interface, which forces implementers to support methods they don't need, and forces consumers to depend on things they don't use. Keep interfaces small and role-specific.

```csharp
// ❌ Fat interface — a ReadOnlyOrderRepository would be forced to throw NotImplementedException
public interface IOrderRepository
{
    Order GetById(int id);
    void Save(Order order);
    void Delete(int id);
    void ArchiveOldOrders();
}

// ✅ Segregated — each consumer depends only on what it needs
public interface IOrderReader
{
    Order GetById(int id);
}

public interface IOrderWriter
{
    void Save(Order order);
    void Delete(int id);
}

public interface IOrderArchiver
{
    void ArchiveOldOrders();
}

// A reporting service only needs read access:
public class OrderReportService
{
    private readonly IOrderReader _reader;
    public OrderReportService(IOrderReader reader) => _reader = reader;
}
```

---

## Recipe 4 (Intermediate) — Repository Pattern (Abstracting Data Access)

The classic DIP use case: business logic shouldn't know if data comes from SQL Server, an in-memory list, or a REST API.

```csharp
public record Order(int Id, string CustomerEmail, decimal Total);

public interface IOrderRepository
{
    Order? GetById(int id);
    void Add(Order order);
}

// Low-level detail #1
public class SqlOrderRepository : IOrderRepository
{
    public Order? GetById(int id)
    {
        Console.WriteLine($"SELECT * FROM Orders WHERE Id = {id}");
        return new Order(id, "sql-customer@example.com", 99.99m);
    }

    public void Add(Order order) =>
        Console.WriteLine($"INSERT INTO Orders VALUES ({order.Id}, {order.Total})");
}

// Low-level detail #2 — great for unit tests, no database needed
public class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<int, Order> _store = new();

    public Order? GetById(int id) => _store.GetValueOrDefault(id);

    public void Add(Order order) => _store[order.Id] = order;
}

// High-level module — only knows about the abstraction
public class OrderService
{
    private readonly IOrderRepository _repository;
    public OrderService(IOrderRepository repository) => _repository = repository;

    public void CreateOrder(int id, string email, decimal total)
    {
        var order = new Order(id, email, total);
        _repository.Add(order);
    }
}
```

---

## Recipe 5 (Intermediate) — Strategy Pattern for Runtime-Selected Behavior

DIP pairs naturally with Strategy when you need to pick *which* implementation to use at runtime (not just at startup).

```csharp
public interface IPaymentStrategy
{
    bool CanHandle(string method);
    void Process(decimal amount);
}

public class CreditCardPayment : IPaymentStrategy
{
    public bool CanHandle(string method) => method == "credit_card";
    public void Process(decimal amount) => Console.WriteLine($"Charged ${amount} to credit card.");
}

public class PayPalPayment : IPaymentStrategy
{
    public bool CanHandle(string method) => method == "paypal";
    public void Process(decimal amount) => Console.WriteLine($"Charged ${amount} via PayPal.");
}

public class PaymentProcessor
{
    private readonly IEnumerable<IPaymentStrategy> _strategies;

    // The high-level module depends on an abstraction (IEnumerable<IPaymentStrategy>),
    // not on CreditCardPayment/PayPalPayment directly.
    public PaymentProcessor(IEnumerable<IPaymentStrategy> strategies) => _strategies = strategies;

    public void Pay(string method, decimal amount)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(method))
            ?? throw new NotSupportedException($"No handler for '{method}'");
        strategy.Process(amount);
    }
}

var processor = new PaymentProcessor(new IPaymentStrategy[]
{
    new CreditCardPayment(),
    new PayPalPayment()
});
processor.Pay("paypal", 49.99m);
```

Adding a new payment method (e.g. `ApplePayPayment`) means writing a new class and registering it — `PaymentProcessor` never changes. That's the **Open/Closed Principle falling out naturally from DIP**.

---

## Recipe 6 (Intermediate→Advanced) — Using a Real DI Container

Manually `new`-ing everything at a composition root works, but for larger apps you use a DI container to wire the graph and manage object lifetimes (`Microsoft.Extensions.DependencyInjection`).

```csharp
// dotnet add package Microsoft.Extensions.DependencyInjection

using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton<ILogger, ConsoleLogger>();          // one instance for the app's lifetime
services.AddScoped<IOrderRepository, SqlOrderRepository>();// one instance per scope (e.g. per web request)
services.AddTransient<INotifier, EmailSender>();           // new instance every time it's requested

services.AddTransient<OrderService>(); // container resolves OrderService's constructor deps automatically

using var provider = services.BuildServiceProvider();
var orderService = provider.GetRequiredService<OrderService>();
orderService.CreateOrder(1, "bob@example.com", 25.00m);
```

Lifetime cheat sheet:

| Lifetime    | Meaning                                            | Typical use                     |
|-------------|-----------------------------------------------------|----------------------------------|
| `Transient` | New instance every resolution                       | Stateless, lightweight services  |
| `Scoped`    | One instance per scope (per HTTP request in ASP.NET)| DbContext, unit-of-work          |
| `Singleton` | One instance for the app's lifetime                 | Config, caches, loggers          |

---

## Recipe 7 (Advanced) — Decorators for Cross-Cutting Concerns

Because everything depends on the abstraction (`IOrderRepository`), you can wrap an implementation with another implementation of the *same interface* to add logging, caching, retries, etc., without touching the original class or its consumers.

```csharp
// Adds caching around any IOrderRepository
public class CachingOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly Dictionary<int, Order> _cache = new();

    public CachingOrderRepository(IOrderRepository inner) => _inner = inner;

    public Order? GetById(int id)
    {
        if (_cache.TryGetValue(id, out var cached))
        {
            Console.WriteLine($"Cache hit for order {id}");
            return cached;
        }

        var order = _inner.GetById(id);
        if (order is not null) _cache[id] = order;
        return order;
    }

    public void Add(Order order)
    {
        _inner.Add(order);
        _cache[order.Id] = order;
    }
}

// Adds logging around any IOrderRepository
public class LoggingOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly ILogger _logger;

    public LoggingOrderRepository(IOrderRepository inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public Order? GetById(int id)
    {
        _logger.Log($"Fetching order {id}");
        return _inner.GetById(id);
    }

    public void Add(Order order)
    {
        _logger.Log($"Adding order {order.Id}");
        _inner.Add(order);
    }
}

// Stack decorators like Russian dolls — OrderService is unaware and unchanged
IOrderRepository repo = new LoggingOrderRepository(
    new CachingOrderRepository(
        new SqlOrderRepository()),
    new ConsoleLogger());

var service = new OrderService(repo);
```

With a DI container, this composition is usually done via `Scrutor`'s `Decorate<T>()` extension rather than by hand.

---

## Recipe 8 (Advanced) — Generic Repository + Unit of Work

A more architecture-scale application of DIP: abstract persistence entirely behind generic contracts so the domain/application layer has zero reference to EF Core, Dapper, or any ORM.

```csharp
public interface IRepository<T> where T : class
{
    T? GetById(int id);
    IEnumerable<T> GetAll();
    void Add(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork : IDisposable
{
    IRepository<Order> Orders { get; }
    IRepository<Customer> Customers { get; }
    int SaveChanges();
}

public record Customer(int Id, string Name);

// EF-Core-backed implementation lives in the Infrastructure layer,
// referencing the Domain/Application layer — never the reverse.
public class EfUnitOfWork : IUnitOfWork
{
    // In real code these wrap a DbContext + generic EfRepository<T>
    public IRepository<Order> Orders { get; }
    public IRepository<Customer> Customers { get; }

    public EfUnitOfWork(IRepository<Order> orders, IRepository<Customer> customers)
    {
        Orders = orders;
        Customers = customers;
    }

    public int SaveChanges()
    {
        Console.WriteLine("Committing transaction...");
        return 1;
    }

    public void Dispose() { /* dispose DbContext */ }
}

// Application layer — depends only on IUnitOfWork, knows nothing about EF Core
public class CheckoutHandler
{
    private readonly IUnitOfWork _uow;
    public CheckoutHandler(IUnitOfWork uow) => _uow = uow;

    public void Checkout(int customerId, decimal total)
    {
        var order = new Order(new Random().Next(1000, 9999), $"customer-{customerId}@x.com", total);
        _uow.Orders.Add(order);
        _uow.SaveChanges();
    }
}
```

This is the layering that makes **Clean Architecture / Hexagonal / Onion Architecture** work: dependencies always point *inward*, toward abstractions owned by the domain, never outward toward infrastructure.

---

## Recipe 9 (Advanced) — Why This All Matters: Unit Testing with Mocks

The payoff of DIP shows up hardest in tests. Because `OrderService` depends on interfaces, you can substitute fakes/mocks with zero production code changes.

```csharp
// dotnet add package Moq
// dotnet add package xunit

using Moq;
using Xunit;

public class OrderServiceTests
{
    [Fact]
    public void CreateOrder_AddsOrderToRepository()
    {
        // Arrange — fake the low-level dependency
        var mockRepo = new Mock<IOrderRepository>();
        var service = new OrderService(mockRepo.Object);

        // Act
        service.CreateOrder(1, "test@example.com", 10.0m);

        // Assert — verify the interaction happened, without touching a real database
        mockRepo.Verify(r => r.Add(It.Is<Order>(o => o.Id == 1 && o.Total == 10.0m)), Times.Once);
    }
}
```

No SQL Server, no test database, no flakiness — because `OrderService` never knew a concrete `SqlOrderRepository` existed in the first place.

---

## Recipe 10 (Expert) — Event-Driven Systems: Inverting Even the *Notification* of Change

At scale, you often want modules to react to things happening elsewhere without a direct reference between them at all. Abstract the *publishing mechanism* itself.

```csharp
public interface IDomainEvent { }

public record OrderPlacedEvent(int OrderId, string CustomerEmail) : IDomainEvent;

public interface IEventHandler<TEvent> where TEvent : IDomainEvent
{
    void Handle(TEvent domainEvent);
}

public interface IEventDispatcher
{
    void Dispatch<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
}

// Infrastructure detail: a simple in-process dispatcher resolving handlers via DI
public class EventDispatcher : IEventDispatcher
{
    private readonly IServiceProvider _provider;
    public EventDispatcher(IServiceProvider provider) => _provider = provider;

    public void Dispatch<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
    {
        var handlers = _provider.GetServices<IEventHandler<TEvent>>();
        foreach (var handler in handlers)
            handler.Handle(domainEvent);
    }
}

// Independent handlers — none of them know about each other, or about OrderService
public class SendConfirmationEmailHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly INotifier _notifier;
    public SendConfirmationEmailHandler(INotifier notifier) => _notifier = notifier;

    public void Handle(OrderPlacedEvent domainEvent) =>
        _notifier.Send(domainEvent.CustomerEmail, $"Order {domainEvent.OrderId} confirmed!");
}

public class UpdateInventoryHandler : IEventHandler<OrderPlacedEvent>
{
    public void Handle(OrderPlacedEvent domainEvent) =>
        Console.WriteLine($"Reserving inventory for order {domainEvent.OrderId}");
}

// High-level policy — depends only on IEventDispatcher
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IEventDispatcher _dispatcher;

    public OrderService(IOrderRepository repository, IEventDispatcher dispatcher)
    {
        _repository = repository;
        _dispatcher = dispatcher;
    }

    public void CreateOrder(int id, string email, decimal total)
    {
        var order = new Order(id, email, total);
        _repository.Add(order);
        _dispatcher.Dispatch(new OrderPlacedEvent(id, email));
    }
}
```

This is essentially what libraries like **MediatR** give you out of the box — `OrderService` never references `SendConfirmationEmailHandler` or `UpdateInventoryHandler` directly, so you can add/remove reactions to "order placed" indefinitely without modifying `OrderService`.

---

## Common Pitfalls

- **Service Locator disguised as DI** — calling `provider.GetService<T>()` deep inside a class instead of injecting through the constructor. This hides the dependency and breaks testability; it looks like DIP but isn't.
- **Leaky abstractions** — an interface like `IOrderRepository` that exposes `IQueryable<Order>` or EF-specific types still leaks the ORM into your domain layer. Keep abstractions in terms the domain understands.
- **Interfaces owned by the wrong layer** — DIP says the *high-level* module should own the abstraction, and the low-level module should implement it. If your domain project references your infrastructure project's interfaces, you've inverted nothing.
- **Over-abstracting trivial things** — wrapping `DateTime.Now` or `Math.Sqrt` behind an interface "for testability" usually adds ceremony without real benefit. Reserve interfaces for things that genuinely vary (I/O, external services, volatile business rules).
- **One interface, one implementation, forever** — if a seam never changes and never needs mocking, a plain class may be simpler than an interface + DI registration.

---

## Quick Reference

| Level        | Pattern                          | Solves                                   |
|--------------|-----------------------------------|-------------------------------------------|
| Basic        | Constructor injection             | Tight coupling to concrete classes        |
| Basic        | Interface segregation             | Fat interfaces forcing unneeded methods   |
| Intermediate | Repository pattern                | Coupling business logic to a data source  |
| Intermediate | Strategy pattern                  | Runtime selection between implementations |
| Advanced     | DI container + lifetimes          | Manual wiring at scale                    |
| Advanced     | Decorators                        | Cross-cutting concerns (cache/log/retry)  |
| Advanced     | Generic repo + Unit of Work        | Whole persistence layer abstraction       |
| Advanced     | Mocking in unit tests              | Fast, isolated tests                      |
| Expert       | Event dispatcher / mediator        | Decoupling reactions from the trigger     |
