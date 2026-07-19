# SOLID Principles in C# — Advanced Examples

Five deliberately harder examples, one per principle, each using a pattern you'd actually run into in a real codebase: middleware pipelines, double dispatch, transitivity bugs, capability interfaces, and open-generic decorator chains through a DI container.

---

## S — Single Responsibility: Async Middleware Pipeline

**The trap:** most SRP examples just show "split a God class into three classes" — the real difficulty is *composing* many single-purpose steps into one coherent operation without a coordinator that becomes a God class itself. The fix is a **middleware/chain-of-responsibility pipeline**, the same shape ASP.NET Core itself uses.

```csharp
public class OrderContext
{
    public required Order Order { get; init; }
    public decimal FinalPrice { get; set; }
    public List<string> Log { get; } = new();
}

// One tiny, composable contract — each implementer has exactly one reason to change.
public interface IPipelineStep<TContext>
{
    Task ExecuteAsync(TContext context, Func<Task> next);
}

public class ValidationStep : IPipelineStep<OrderContext>
{
    public Task ExecuteAsync(OrderContext ctx, Func<Task> next)
    {
        if (ctx.Order.Items.Count == 0)
            throw new InvalidOperationException("Order has no items");
        ctx.Log.Add("Validated");
        return next();
    }
}

public class PricingStep : IPipelineStep<OrderContext>
{
    public Task ExecuteAsync(OrderContext ctx, Func<Task> next)
    {
        ctx.FinalPrice = ctx.Order.Items.Sum(i => i.Price * i.Qty);
        ctx.Log.Add($"Priced at {ctx.FinalPrice:C}");
        return next();
    }
}

public class TaxStep : IPipelineStep<OrderContext>
{
    private readonly decimal _rate;
    public TaxStep(decimal rate) => _rate = rate;

    public Task ExecuteAsync(OrderContext ctx, Func<Task> next)
    {
        ctx.FinalPrice *= 1 + _rate;
        ctx.Log.Add($"Tax applied, total {ctx.FinalPrice:C}");
        return next();
    }
}

public class PersistenceStep : IPipelineStep<OrderContext>
{
    private readonly IOrderRepository _repo;
    public PersistenceStep(IOrderRepository repo) => _repo = repo;

    public Task ExecuteAsync(OrderContext ctx, Func<Task> next)
    {
        _repo.Add(ctx.Order);
        ctx.Log.Add("Persisted");
        return next(); // last step still calls next() -> terminal no-op delegate
    }
}

// The ONLY responsibility of this class is composing steps in order — nothing else.
public class Pipeline<TContext>
{
    private readonly IReadOnlyList<IPipelineStep<TContext>> _steps;
    public Pipeline(IEnumerable<IPipelineStep<TContext>> steps) => _steps = steps.ToList();

    public Task RunAsync(TContext context)
    {
        Func<Task> terminal = () => Task.CompletedTask;
        var chain = _steps.Reverse().Aggregate(terminal,
            (next, step) => () => step.ExecuteAsync(context, next));
        return chain();
    }
}

// Usage — reordering, adding, or removing steps never touches step implementations:
var pipeline = new Pipeline<OrderContext>(new IPipelineStep<OrderContext>[]
{
    new ValidationStep(),
    new PricingStep(),
    new TaxStep(0.08m),
    new PersistenceStep(new SqlOrderRepository())
});

var context = new OrderContext { Order = someOrder };
await pipeline.RunAsync(context);
```

Each step has exactly one reason to change (its own business rule). The `Pipeline<T>` class has exactly one reason to change too: *how steps are chained*, never *what any step does*. That second-order discipline — the coordinator itself staying single-purpose — is the part that's easy to get wrong at scale.

---

## O — Open/Closed: Double Dispatch (Visitor Pattern)

**The trap:** naive OCP examples use `if/switch` on a type enum, which *looks* extensible but requires editing the switch every time. True OCP for a *fixed* set of node types with a *growing* set of operations needs **double dispatch**.

```csharp
public interface IExprVisitor<T>
{
    T VisitNumber(NumberExpr expr);
    T VisitAdd(AddExpr expr);
    T VisitMultiply(MultiplyExpr expr);
}

public abstract class Expr
{
    public abstract T Accept<T>(IExprVisitor<T> visitor);
}

public class NumberExpr : Expr
{
    public double Value { get; }
    public NumberExpr(double value) => Value = value;
    public override T Accept<T>(IExprVisitor<T> visitor) => visitor.VisitNumber(this);
}

public class AddExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public AddExpr(Expr left, Expr right) => (Left, Right) = (left, right);
    public override T Accept<T>(IExprVisitor<T> visitor) => visitor.VisitAdd(this);
}

public class MultiplyExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public MultiplyExpr(Expr left, Expr right) => (Left, Right) = (left, right);
    public override T Accept<T>(IExprVisitor<T> visitor) => visitor.VisitMultiply(this);
}

// New operation #1 — zero changes to Expr/NumberExpr/AddExpr/MultiplyExpr
public class EvaluatorVisitor : IExprVisitor<double>
{
    public double VisitNumber(NumberExpr e) => e.Value;
    public double VisitAdd(AddExpr e) => e.Left.Accept(this) + e.Right.Accept(this);
    public double VisitMultiply(MultiplyExpr e) => e.Left.Accept(this) * e.Right.Accept(this);
}

// New operation #2 — again, zero changes to the node hierarchy
public class PrintVisitor : IExprVisitor<string>
{
    public string VisitNumber(NumberExpr e) => e.Value.ToString();
    public string VisitAdd(AddExpr e) => $"({e.Left.Accept(this)} + {e.Right.Accept(this)})";
    public string VisitMultiply(MultiplyExpr e) => $"({e.Left.Accept(this)} * {e.Right.Accept(this)})";
}

// New operation #3 — symbolic differentiation, added a year later, still zero changes above
public class DerivativeVisitor : IExprVisitor<Expr>
{
    public Expr VisitNumber(NumberExpr e) => new NumberExpr(0);
    public Expr VisitAdd(AddExpr e) => new AddExpr(e.Left.Accept(this), e.Right.Accept(this));
    public Expr VisitMultiply(MultiplyExpr e) =>
        new AddExpr(
            new MultiplyExpr(e.Left.Accept(this), e.Right),
            new MultiplyExpr(e.Left, e.Right.Accept(this))); // product rule
}

// Usage
Expr expr = new AddExpr(new NumberExpr(3), new MultiplyExpr(new NumberExpr(4), new NumberExpr(5)));
Console.WriteLine(expr.Accept(new PrintVisitor()));      // (3 + (4 * 5))
Console.WriteLine(expr.Accept(new EvaluatorVisitor()));   // 23
```

The node hierarchy is **closed** (adding a node type *does* require touching every visitor — that's the honest tradeoff of Visitor), but the set of **operations** is genuinely **open**: `DerivativeVisitor` was added without a single edit to `Expr`, `NumberExpr`, `AddExpr`, or `MultiplyExpr`. This is the real mechanism behind things like Roslyn's syntax tree walkers.

---

## L — Liskov Substitution: A Violation the Type System Can't Catch

**The trap:** Rectangle/Square is a *structural* violation the compiler sort of hints at. The genuinely hard LSP bugs are **mathematical contract violations** — nothing type-checks, nothing throws, code just silently produces wrong answers.

```csharp
public record Product(string Name, decimal Price);

// Base contract for IComparer<T>.Compare (documented, never enforced by the compiler):
// it must define a consistent TOTAL ORDER — critically, transitivity:
// if Compare(a,b) < 0 and Compare(b,c) < 0, then Compare(a,c) must be < 0.

public class PriceComparer : IComparer<Product>
{
    public int Compare(Product? x, Product? y) => x!.Price.CompareTo(y!.Price);
}

// ❌ Compiles fine. Implements the interface correctly. Silently breaks LSP.
public class FuzzyPriceComparer : IComparer<Product>
{
    public int Compare(Product? x, Product? y)
    {
        var diff = x!.Price - y!.Price;
        if (Math.Abs(diff) < 5) return 0; // "close enough" treated as equal
        return diff < 0 ? -1 : 1;
    }
}

// Products priced 10, 13, 17:
//   Compare(10, 13) == 0   (diff 3, "equal")
//   Compare(13, 17) == 0   (diff 4, "equal")
//   Compare(10, 17) != 0   (diff 7, NOT equal)
// Equality is not transitive. Any algorithm written against IComparer<T>'s contract —
// List<T>.Sort, Array.BinarySearch, SortedSet<T>, LINQ's OrderBy — can now produce
// duplicate entries in a SortedSet, inconsistent sort order, or a failed binary search,
// purely because FuzzyPriceComparer was substituted in for PriceComparer.

var set = new SortedSet<Product>(new FuzzyPriceComparer());
set.Add(new Product("A", 10));
set.Add(new Product("B", 13)); // considered "equal" to A -> silently NOT added
set.Add(new Product("C", 17)); // considered "equal" to B, but NOT to A -> inconsistent state
```

**✅ Fix** — never encode tolerance/fuzziness as *equality* in a comparer; that's a modeling error, not an optimization. If "close enough" grouping is genuinely needed, do it explicitly as a separate step (e.g. bucket first, then compare within buckets) rather than inside a general-purpose `IComparer<T>`.

**A second, compiler-enforced LSP mechanism worth knowing:** C#'s generic variance (`in`/`out`) lets the compiler *guarantee* substitutability instead of hoping developers uphold it by convention.

```csharp
public interface IReadOnlyRepository<out T>   // T only ever comes OUT — safe to widen
{
    T GetById(int id);
    IEnumerable<T> GetAll();
}

public class Dog : Animal { }

IReadOnlyRepository<Dog> dogRepo = new DogRepository();
IReadOnlyRepository<Animal> animalRepo = dogRepo; // legal — covariance, compiler-verified LSP

// Without 'out', this assignment would be a compile error, because a plain
// IReadOnlyRepository<Dog> cannot be safely treated as IReadOnlyRepository<Animal>
// once you also allow input parameters (e.g. void Add(T item) would break it).
```

---

## I — Interface Segregation: Capability Interfaces + Runtime Feature Detection

**The trap:** basic ISP examples just split one fat interface into a few smaller ones. The harder, real-world version is a **plugin/device-driver-style system** where implementers support an arbitrary *subset* of many fine-grained capabilities, and callers must safely probe for what's available — without ever forcing an implementer to fake support it doesn't have.

```csharp
public interface IReadableMedia { Stream OpenRead(); }
public interface IWritableMedia { Stream OpenWrite(); }
public interface ISeekable { void Seek(long position); }
public interface ITranscodable { Task TranscodeAsync(string targetFormat); }

// Supports everything — a local file
public class LocalFileMedia : IReadableMedia, IWritableMedia, ISeekable
{
    private readonly string _path;
    public LocalFileMedia(string path) => _path = path;
    public Stream OpenRead() => File.OpenRead(_path);
    public Stream OpenWrite() => File.OpenWrite(_path);
    public void Seek(long position) { /* fseek-equivalent */ }
}

// Supports only reading — a live network stream. No NotSupportedException anywhere,
// because it never claimed to support seeking or writing in the first place.
public class LiveStreamMedia : IReadableMedia
{
    public Stream OpenRead() => throw new NotImplementedException("network stream elided");
}

// Supports reading + transcoding, but is neither seekable nor writable — a cloud blob.
public class CloudBlobMedia : IReadableMedia, ITranscodable
{
    public Stream OpenRead() => throw new NotImplementedException("blob download elided");
    public Task TranscodeAsync(string targetFormat) => Task.CompletedTask;
}

public class MediaProcessor
{
    public async Task Process(IReadableMedia media)
    {
        using var stream = media.OpenRead();
        // ... core logic that only ever needs read access ...

        // Safe capability probing: each optional behavior is its own contract,
        // so "supports X?" is answered by the type system, not a runtime exception.
        if (media is ISeekable seekable)
            seekable.Seek(0);

        if (media is ITranscodable transcodable)
            await transcodable.TranscodeAsync("mp4");
    }
}
```

A second, equally advanced flavor of ISP: composing **minimal generic constraints** instead of one bloated base entity type.

```csharp
public interface IIdentifiable<TId> { TId Id { get; } }
public interface IAuditable { DateTime CreatedAt { get; } DateTime? ModifiedAt { get; } }
public interface ISoftDeletable { bool IsDeleted { get; set; } }

// This repository requires EXACTLY the two capabilities it needs — nothing more —
// instead of forcing every entity in the system to implement a single fat IEntity.
public class AuditedRepository<T, TId> where T : IIdentifiable<TId>, IAuditable
{
    public void LogAccess(T entity) =>
        Console.WriteLine($"Entity {entity.Id} last touched {entity.ModifiedAt ?? entity.CreatedAt}");
}

// An entity that doesn't need soft-delete simply never implements ISoftDeletable —
// and AuditedRepository<T,TId> couldn't care less, because it never asked for it.
public class Invoice : IIdentifiable<int>, IAuditable
{
    public int Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; set; }
}
```

---

## D — Dependency Inversion: Open-Generic Decorator Pipeline via DI

**The trap:** basic DIP demos wire one interface to one implementation. The advanced, genuinely common enterprise pattern is registering **open generic decorators** so cross-cutting concerns (logging, validation, caching) wrap *every* handler automatically — without the mediator ever knowing concrete behavior types exist. This is the mechanism behind libraries like MediatR.

```csharp
public interface IRequest<TResponse> { }

public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken ct);
}

public interface IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken ct);
}

// Cross-cutting concern — depends only on the generic abstraction, never a concrete request
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger _logger;
    public LoggingBehavior(ILogger logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        _logger.Log($"Handling {typeof(TRequest).Name}");
        var response = await next();
        _logger.Log($"Handled {typeof(TRequest).Name}");
        return response;
    }
}

public interface IValidator<TRequest> { void Validate(TRequest request); }

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        foreach (var v in _validators) v.Validate(request);
        return await next();
    }
}

// The mediator composes behaviors purely through abstractions — it never references
// LoggingBehavior or ValidationBehavior by name, so adding concern #3 next year
// touches zero lines here.
public class Mediator
{
    private readonly IServiceProvider _provider;
    public Mediator(IServiceProvider provider) => _provider = provider;

    public async Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : IRequest<TResponse>
    {
        var handler = _provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = _provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse();

        Func<Task<TResponse>> pipeline = () => handler.Handle(request, ct);
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = () => behavior.Handle(request, next, ct);
        }
        return await pipeline();
    }
}

// --- Concrete feature: unrelated to any of the above ---
public record CreateOrderCommand(string CustomerEmail, decimal Total) : IRequest<int>;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, int>
{
    public Task<int> Handle(CreateOrderCommand request, CancellationToken ct) => Task.FromResult(42);
}

public class CreateOrderValidator : IValidator<CreateOrderCommand>
{
    public void Validate(CreateOrderCommand request)
    {
        if (request.Total <= 0) throw new ArgumentException("Total must be positive");
    }
}

// --- Composition root ---
var services = new ServiceCollection();
services.AddSingleton<ILogger, ConsoleLogger>();
services.AddTransient<IValidator<CreateOrderCommand>, CreateOrderValidator>();
services.AddTransient<IRequestHandler<CreateOrderCommand, int>, CreateOrderHandler>();

// Open generic registration — applies to EVERY IRequest<T>/handler pair in the system,
// present or future, without a single line of per-feature wiring.
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddTransient<Mediator>();

using var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<Mediator>();
var orderId = await mediator.Send<CreateOrderCommand, int>(new CreateOrderCommand("a@b.com", 99.99m));
```

`Mediator`, `LoggingBehavior<,>`, and `ValidationBehavior<,>` never reference `CreateOrderCommand` or `CreateOrderHandler`. `CreateOrderHandler` never references logging or validation. Every dependency points at an abstraction — including the *generic shape* of the abstraction — and the container assembles the whole graph at the composition root. This is DIP scaled past "swap one implementation" into "cross-cutting concerns compose themselves."

---

## Why These Are Harder Than the Usual Examples

| Principle | Typical intro example | What makes the above versions harder |
|-----------|------------------------|----------------------------------------|
| SRP | Split a class into three | Composing many single-purpose steps without the composer becoming a God object |
| OCP | `if/switch` on a type | Double dispatch — extending *behavior* while the *node hierarchy* stays closed |
| LSP | Rectangle/Square | A contract violation (transitivity) invisible to the compiler and to code review |
| ISP | Split one interface into two | Runtime capability probing across an open-ended set of optional behaviors |
| DIP | Inject one interface | Open-generic decorators that wrap *every* handler in the system automatically |
