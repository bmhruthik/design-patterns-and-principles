# Liskov Substitution Principle (LSP) in C# — A Cookbook

> **LSP, formally (Barbara Liskov):**
> If `S` is a subtype of `T`, then objects of type `T` in a program may be replaced with objects of type `S` without altering any of the desirable properties of that program.
>
> **In practice, a subtype must not:**
> - **Strengthen preconditions** (demand more from callers than the base type does)
> - **Weaken postconditions** (promise less than the base type does)
> - **Break invariants** the base type guarantees
> - **Introduce new exceptions** callers of the base type aren't prepared for
>
> If a client that only knows about the base type/interface can be handed *any* subtype and still behave correctly, LSP holds. If the client needs to check `if (obj is Square)` to work around a subtype's quirks, LSP is already broken.

---

## Recipe 1 (Basic) — The Classic Violation: Rectangle/Square

Mathematically a square *is* a rectangle. Behaviorally, in code, it usually isn't.

```csharp
public class Rectangle
{
    public virtual int Width { get; set; }
    public virtual int Height { get; set; }
    public int Area => Width * Height;
}

public class Square : Rectangle
{
    public override int Width
    {
        get => base.Width;
        set { base.Width = value; base.Height = value; } // keeps it "square"
    }
    public override int Height
    {
        get => base.Height;
        set { base.Height = value; base.Width = value; }
    }
}

void Resize(Rectangle r)
{
    r.Width = 5;
    r.Height = 10;
    // Any client of Rectangle reasonably expects Area == 50 here.
    Console.WriteLine(r.Area); // 50 for Rectangle, 100 for Square — silently wrong!
}
```

`Square` technically overrides everything correctly, but it violates the *behavioral* contract clients assume about `Rectangle`: setting `Width` and `Height` independently. Substituting a `Square` breaks correctness.

**✅ Fix — don't force an inheritance relationship that doesn't hold behaviorally.** Use a shared abstraction only for what's *actually* common (area), and keep the shapes independent.

```csharp
public interface IShape
{
    int Area { get; }
}

public class Rectangle : IShape
{
    public int Width { get; }
    public int Height { get; }
    public Rectangle(int width, int height) => (Width, Height) = (width, height);
    public int Area => Width * Height;
}

public class Square : IShape
{
    public int Side { get; }
    public Square(int side) => Side = side;
    public int Area => Side * Side;
}
```

Neither shape makes promises it can't keep. Both are freely substitutable wherever `IShape` is expected.

---

## Recipe 2 (Basic) — Overriding to Throw: The "Not All Birds Fly" Problem

```csharp
public class Bird
{
    public virtual void Fly() => Console.WriteLine("Flying");
}

public class Ostrich : Bird
{
    // ❌ Silently breaks any code written against Bird
    public override void Fly() => throw new NotSupportedException("Ostriches can't fly");
}

void ReleaseBirds(List<Bird> birds)
{
    foreach (var b in birds) b.Fly(); // blows up the moment an Ostrich is in the list
}
```

Whenever an override's *first line* is throwing `NotImplementedException`/`NotSupportedException`, that's almost always an LSP violation in disguise — it means the subtype doesn't actually fulfill the base type's contract.

**✅ Fix — segregate the capability into its own interface** rather than putting it on the shared base.

```csharp
public abstract class Bird
{
    public abstract void Eat();
}

public interface IFlyingBird
{
    void Fly();
}

public class Sparrow : Bird, IFlyingBird
{
    public override void Eat() => Console.WriteLine("Eating seeds");
    public void Fly() => Console.WriteLine("Flying");
}

public class Ostrich : Bird
{
    public override void Eat() => Console.WriteLine("Eating plants");
    // no Fly — and that's fine, it was never promised
}

void ReleaseFlyingBirds(IEnumerable<IFlyingBird> flyers)
{
    foreach (var f in flyers) f.Fly(); // only ever called on things that truly can
}
```

---

## Recipe 3 (Basic→Intermediate) — Strengthening Preconditions

A subtype is allowed to accept **more** than the base type, but never **less** — narrowing what's valid breaks callers who only know the base contract.

```csharp
public class Discount
{
    // Contract: works for any non-negative price
    public virtual decimal Apply(decimal price)
    {
        if (price < 0) throw new ArgumentException("Price cannot be negative");
        return price * 0.9m;
    }
}

public class VipDiscount : Discount
{
    // ❌ Adds a STRICTER requirement (price >= 100) that Discount never had.
    // Any client trusting the Discount contract can now throw unexpectedly.
    public override decimal Apply(decimal price)
    {
        if (price < 100) throw new ArgumentException("VIP discount requires price >= 100");
        return price * 0.8m;
    }
}
```

**✅ Fix** — handle the edge case gracefully instead of rejecting it; never make an override pickier than its base:

```csharp
public class VipDiscount : Discount
{
    public override decimal Apply(decimal price)
    {
        if (price < 0) throw new ArgumentException("Price cannot be negative");
        return price >= 100 ? price * 0.8m : price * 0.9m; // falls back to standard discount
    }
}
```

---

## Recipe 4 (Intermediate) — Weakening Postconditions

Just as preconditions can't be strengthened, postconditions (what a method *guarantees* on return) can't be weakened.

```csharp
public class Repository
{
    // Contract: never returns null — always a list, possibly empty
    public virtual List<string> GetItems() => new() { "a", "b" };
}

public class BuggyRepository : Repository
{
    // ❌ Weakens the guarantee: base promises non-null, this can return null
    public override List<string> GetItems() => null!;
}

void PrintCount(Repository repo)
{
    Console.WriteLine(repo.GetItems().Count); // NullReferenceException with BuggyRepository
}
```

**✅ Fix** — always uphold the same guarantee, even in edge cases:

```csharp
public class BuggyRepository : Repository
{
    public override List<string> GetItems() => new(); // empty, never null
}
```

---

## Recipe 5 (Intermediate) — Read vs. Write Contracts (Mutable Collections)

A very common real-world LSP trap: a "read-only" variant of something that inherits from (or implements the same interface as) a mutable version, and throws on the mutating members.

```csharp
public interface ICollection<T>
{
    void Add(T item);
    IEnumerable<T> Items { get; }
}

public class MutableCollection<T> : ICollection<T>
{
    private readonly List<T> _items = new();
    public void Add(T item) => _items.Add(item);
    public IEnumerable<T> Items => _items;
}

public class ReadOnlyCollectionWrapper<T> : ICollection<T>
{
    private readonly List<T> _items;
    public ReadOnlyCollectionWrapper(List<T> items) => _items = items;

    // ❌ LSP violation — the interface implies Add works; this one lies about it.
    public void Add(T item) => throw new NotSupportedException("Collection is read-only");
    public IEnumerable<T> Items => _items;
}

void FillCollection(ICollection<int> collection)
{
    collection.Add(1); // fine for MutableCollection, throws for ReadOnlyCollectionWrapper
}
```

**✅ Fix — this is exactly why .NET splits `IReadOnlyList<T>` from `IList<T>`.** Model read and write as separate contracts so a read-only type simply doesn't implement the write side at all — nothing to violate.

```csharp
public interface IReadOnlyBasket
{
    IReadOnlyList<string> Items { get; }
}

public interface IBasket : IReadOnlyBasket
{
    void Add(string item);
}

public class ShoppingBasket : IBasket
{
    private readonly List<string> _items = new();
    public IReadOnlyList<string> Items => _items;
    public void Add(string item) => _items.Add(item);
}

// Any code depending on IReadOnlyBasket can safely receive any IBasket —
// there's no mutating method it could be tricked into calling.
void PrintBasket(IReadOnlyBasket basket)
{
    foreach (var item in basket.Items) Console.WriteLine(item);
}
```

---

## Recipe 6 (Intermediate→Advanced) — When Overriding *Is* Safe: Weakening a Precondition

Not every override is a violation — it's worth seeing a correct one for contrast. A subtype **may** accept a *wider* range of inputs than its base; that's compatible with LSP.

```csharp
public class BankAccount
{
    public decimal Balance { get; protected set; }

    public virtual void Withdraw(decimal amount)
    {
        if (amount > Balance) throw new InvalidOperationException("Insufficient funds");
        Balance -= amount;
    }
}

public class CreditAccount : BankAccount
{
    public decimal CreditLimit { get; }
    public CreditAccount(decimal limit) => CreditLimit = limit;

    // ✅ This WEAKENS the precondition (accepts withdrawals the base would reject,
    // up to the credit limit) — clients that only expect "may throw if amount > Balance"
    // are still safe; this override only ever succeeds in *more* cases, never fewer.
    public override void Withdraw(decimal amount)
    {
        if (amount > Balance + CreditLimit) throw new InvalidOperationException("Exceeds credit limit");
        Balance -= amount;
    }
}
```

Any code written against `BankAccount.Withdraw` continues to work correctly with a `CreditAccount` substituted in — it just succeeds more often than expected, never less.

---

## Recipe 7 (Advanced) — History Constraint Violations

A subtype must not add invariants that the base type never promised, if doing so can corrupt state that base-type code assumes is unconstrained.

```csharp
public class NumberCollection
{
    protected readonly List<int> _numbers = new();
    public virtual void Add(int number) => _numbers.Add(number);
    public IReadOnlyList<int> Numbers => _numbers;
}

public class EvenNumberCollection : NumberCollection
{
    // ❌ Introduces a NEW invariant ("only even numbers") that NumberCollection
    // never guaranteed or required. Any pre-existing code that freely adds
    // odd numbers to a NumberCollection will now throw when an
    // EvenNumberCollection is substituted in — a "history constraint" violation.
    public override void Add(int number)
    {
        if (number % 2 != 0) throw new ArgumentException("Only even numbers allowed");
        base.Add(number);
    }
}
```

**✅ Fix** — if "only even numbers" is a genuinely different contract, model it as its own type with its own name, not as a subtype of the unconstrained one:

```csharp
public class EvenNumberCollection
{
    private readonly List<int> _numbers = new();
    public void Add(int number)
    {
        if (number % 2 != 0) throw new ArgumentException("Only even numbers allowed");
        _numbers.Add(number);
    }
    public IReadOnlyList<int> Numbers => _numbers;
}
```
It no longer claims to *be* a `NumberCollection`, so nobody is misled into substituting it where unconstrained behavior is assumed.

---

## Recipe 8 (Advanced) — Enforcing Contracts Structurally with Template Method

Rather than trusting every subclass to honor pre/postconditions by convention, you can lock the contract check into the base class itself using the Template Method pattern — subclasses physically cannot weaken it.

```csharp
public abstract class ShapeBase
{
    // Sealed entry point — no subclass can skip or alter this check.
    public decimal ComputeArea()
    {
        var area = ComputeAreaCore();
        if (area < 0)
            throw new InvalidOperationException(
                $"{GetType().Name} produced a negative area — LSP contract violated");
        return area;
    }

    protected abstract decimal ComputeAreaCore();
}

public class Circle : ShapeBase
{
    private readonly decimal _radius;
    public Circle(decimal radius) => _radius = radius;
    protected override decimal ComputeAreaCore() => 3.14159m * _radius * _radius;
}

public class Triangle : ShapeBase
{
    private readonly decimal _base, _height;
    public Triangle(decimal @base, decimal height) => (_base, _height) = (@base, height);
    protected override decimal ComputeAreaCore() => 0.5m * _base * _height;
}
```

Every current and future `ShapeBase` subtype is guaranteed to honor the "area is never negative" postcondition — the base class enforces it once, structurally, instead of hoping each override remembers to.

---

## Recipe 9 (Expert) — Contract Tests: Verifying LSP Automatically

The most reliable way to *keep* LSP intact as a codebase grows is to write one shared test suite against the abstraction, and run it against every concrete subtype. If any implementation violates the shared contract, its test class fails immediately — no manual code review required.

```csharp
// dotnet add package xunit

using Xunit;

public abstract class ShapeContractTests
{
    // Each concrete test class supplies its own subtype under test
    protected abstract IShape CreateShape(int size);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(100)]
    public void Area_IsNeverNegative(int size)
    {
        var shape = CreateShape(size);
        Assert.True(shape.Area >= 0);
    }

    [Fact]
    public void Area_OfZeroSize_IsZero()
    {
        var shape = CreateShape(0);
        Assert.Equal(0, shape.Area);
    }
}

public class RectangleContractTests : ShapeContractTests
{
    protected override IShape CreateShape(int size) => new Rectangle(size, size);
}

public class SquareContractTests : ShapeContractTests
{
    protected override IShape CreateShape(int size) => new Square(size);
}

// Add a new IShape next year? Add one more test class here —
// it automatically inherits every existing contract check.
```

This turns LSP from "a principle we try to remember" into "a suite that fails CI the moment someone breaks it."

---

## Common Pitfalls

- **Overriding a method just to throw `NotImplementedException`/`NotSupportedException`** — almost always a sign the subtype shouldn't inherit that member (or that type) at all. Segregate instead (Recipe 2).
- **Stricter validation in an override than the base promises** — narrows what callers can rely on (Recipe 3).
- **Returning `null`, throwing new exception types, or otherwise promising less than the base guarantees** — weakens the postcondition (Recipe 4).
- **Modeling "read-only" as inheriting from "mutable"** (or vice-versa) — separate the contracts instead (Recipe 5).
- **Adding invariants a base type never had** — silently corrupts assumptions existing code relies on (Recipe 7).
- **Client code doing `if (obj is ConcreteSubtype)` or `obj.GetType() == typeof(X)` to special-case a subtype's behavior** — this is the clearest tell that LSP is already broken; a well-formed subtype should never need special-casing by its consumers.
- **Confusing "is-a" in the real world with "is-a" in code.** A square *is* mathematically a rectangle, but that doesn't mean `Square : Rectangle` is safe — inheritance must model *behavioral* substitutability, not domain taxonomy.

---

## Quick Reference

| Level        | Technique                                  | Solves                                              |
|--------------|----------------------------------------------|------------------------------------------------------|
| Basic        | Prefer composition/shared interface over inheritance | "Is-a" in name only (Rectangle/Square)         |
| Basic        | Interface segregation for optional capabilities | Overrides that throw `NotSupportedException`     |
| Intermediate | Never narrow accepted inputs in an override   | Strengthened preconditions                            |
| Intermediate | Always uphold base return guarantees          | Weakened postconditions                                |
| Intermediate | Separate read/write interfaces                 | Mutable-vs-immutable substitution traps               |
| Advanced     | Widening (not narrowing) preconditions is safe | Legitimate, LSP-safe specialization                    |
| Advanced     | Avoid adding new invariants in subtypes        | History constraint violations                          |
| Advanced     | Template Method with a sealed contract check   | Structurally enforcing pre/postconditions              |
| Expert       | Shared contract test suites per abstraction    | Catching LSP violations automatically, at every new subtype |
