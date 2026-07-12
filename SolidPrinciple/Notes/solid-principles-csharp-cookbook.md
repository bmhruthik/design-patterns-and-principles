# SOLID Principles in C# - Cookbook

> Practical examples for SRP, OCP, LSP, and ISP. This companion guide pairs with the DIP cookbook.

## 1. SOLID Quick Map

| Principle | Name | Short Meaning |
|---|---|---|
| S | Single Responsibility Principle | A class should have one reason to change |
| O | Open/Closed Principle | Open for extension, closed for modification |
| L | Liskov Substitution Principle | Subtypes must be safely usable as their base type |
| I | Interface Segregation Principle | Prefer small, focused interfaces |
| D | Dependency Inversion Principle | Depend on abstractions, not concrete details |

This file covers SRP, OCP, LSP, and ISP. DIP is covered in the separate `dip-csharp-cookbook.md`.

## 2. Single Responsibility Principle (SRP)

SRP says a class should have one reason to change.

That does not mean a class can have only one method. It means the class should represent one cohesive responsibility.

### Common SRP Smells

- A class mixes business rules, persistence, validation, logging, formatting, and notifications.
- A class changes whenever different teams request unrelated features.
- A class name becomes vague, such as `Manager`, `Processor`, `Helper`, or `Utility`.
- Unit tests for one class need large setup for unrelated behavior.

## 3. SRP Recipe 1 - Split Business Logic from Persistence

### Problem

`InvoiceService` calculates totals and saves directly to a database.

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
        invoice.Total = invoice.Lines.Sum(line => line.Quantity * line.UnitPrice);
        invoice.Tax = invoice.Total * 0.18m;
        invoice.GrandTotal = invoice.Total + invoice.Tax;

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();
    }
}
```

This class has at least two reasons to change:

- Invoice calculation rules change.
- Persistence rules change.

### SRP Version

```csharp
public class InvoiceCalculator
{
    public InvoiceTotals Calculate(IReadOnlyList<InvoiceLine> lines)
    {
        var subtotal = lines.Sum(line => line.Quantity * line.UnitPrice);
        var tax = subtotal * 0.18m;

        return new InvoiceTotals(subtotal, tax, subtotal + tax);
    }
}

public interface IInvoiceRepository
{
    Task SaveAsync(Invoice invoice);
}

public class InvoiceService
{
    private readonly InvoiceCalculator _calculator;
    private readonly IInvoiceRepository _repository;

    public InvoiceService(
        InvoiceCalculator calculator,
        IInvoiceRepository repository)
    {
        _calculator = calculator;
        _repository = repository;
    }

    public async Task CreateInvoiceAsync(Invoice invoice)
    {
        var totals = _calculator.Calculate(invoice.Lines);

        invoice.ApplyTotals(totals);

        await _repository.SaveAsync(invoice);
    }
}

public record InvoiceTotals(decimal Subtotal, decimal Tax, decimal GrandTotal);
```

### Why This Is Better

- Calculation can be tested without a database.
- Persistence can change without rewriting calculation.
- The class names reveal the responsibilities.

## 4. SRP Recipe 2 - Split Validation from Processing

### Problem

```csharp
public class UserRegistrationService
{
    public void Register(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.");
        }

        if (!email.Contains('@'))
        {
            throw new ArgumentException("Email is invalid.");
        }

        if (password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.");
        }

        // Create user...
        // Save user...
        // Send welcome email...
    }
}
```

### SRP Version

```csharp
public class UserRegistrationValidator
{
    public ValidationResult Validate(RegisterUserRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add("Email is required.");
        }
        else if (!request.Email.Contains('@'))
        {
            errors.Add("Email is invalid.");
        }

        if (request.Password.Length < 8)
        {
            errors.Add("Password must be at least 8 characters.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}

public class UserRegistrationService
{
    private readonly UserRegistrationValidator _validator;
    private readonly IUserRepository _users;
    private readonly IWelcomeEmailSender _welcomeEmailSender;

    public UserRegistrationService(
        UserRegistrationValidator validator,
        IUserRepository users,
        IWelcomeEmailSender welcomeEmailSender)
    {
        _validator = validator;
        _users = users;
        _welcomeEmailSender = welcomeEmailSender;
    }

    public async Task<RegistrationResult> RegisterAsync(RegisterUserRequest request)
    {
        var validation = _validator.Validate(request);

        if (!validation.IsValid)
        {
            return RegistrationResult.Failed(validation.Errors);
        }

        var user = new User(request.Email);

        await _users.SaveAsync(user);
        await _welcomeEmailSender.SendAsync(user.Email);

        return RegistrationResult.Success(user.Id);
    }
}
```

### Supporting Types

```csharp
public record RegisterUserRequest(string Email, string Password);

public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success() => new(true, Array.Empty<string>());

    public static ValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}

public record RegistrationResult(bool Succeeded, Guid? UserId, IReadOnlyList<string> Errors)
{
    public static RegistrationResult Success(Guid userId) =>
        new(true, userId, Array.Empty<string>());

    public static RegistrationResult Failed(IReadOnlyList<string> errors) =>
        new(false, null, errors);
}
```

## 5. SRP Recipe 3 - Split Formatting from Data Retrieval

### Problem

```csharp
public class SalesReportService
{
    public string GetMonthlyReport()
    {
        var sales = LoadSalesFromDatabase();
        var total = sales.Sum(sale => sale.Amount);

        return $"""
               Monthly Sales Report
               Total Sales: {total:C}
               Orders: {sales.Count}
               """;
    }

    private List<Sale> LoadSalesFromDatabase()
    {
        // Query database...
        return new List<Sale>();
    }
}
```

### SRP Version

```csharp
public interface ISalesRepository
{
    Task<IReadOnlyList<Sale>> GetMonthlySalesAsync(int year, int month);
}

public class SalesReportBuilder
{
    public SalesReport Build(IReadOnlyList<Sale> sales)
    {
        return new SalesReport(
            TotalSales: sales.Sum(sale => sale.Amount),
            OrderCount: sales.Count);
    }
}

public class PlainTextSalesReportFormatter
{
    public string Format(SalesReport report)
    {
        return $"""
               Monthly Sales Report
               Total Sales: {report.TotalSales:C}
               Orders: {report.OrderCount}
               """;
    }
}

public class SalesReportService
{
    private readonly ISalesRepository _sales;
    private readonly SalesReportBuilder _builder;
    private readonly PlainTextSalesReportFormatter _formatter;

    public SalesReportService(
        ISalesRepository sales,
        SalesReportBuilder builder,
        PlainTextSalesReportFormatter formatter)
    {
        _sales = sales;
        _builder = builder;
        _formatter = formatter;
    }

    public async Task<string> GetMonthlyReportAsync(int year, int month)
    {
        var sales = await _sales.GetMonthlySalesAsync(year, month);
        var report = _builder.Build(sales);

        return _formatter.Format(report);
    }
}

public record Sale(decimal Amount);

public record SalesReport(decimal TotalSales, int OrderCount);
```

## 6. SRP Recipe 4 - When Not to Split

Do not split just because a class has multiple methods.

```csharp
public class Money
{
    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException("Currencies must match.");
        }

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(decimal multiplier)
    {
        return new Money(Amount * multiplier, Currency);
    }
}
```

This class has multiple methods, but one cohesive responsibility: representing money behavior.

## 7. SRP Checklist

Ask:

- What reason would make this class change?
- Are there multiple unrelated reasons?
- Can I describe the class responsibility without using "and"?
- Can this class be tested without unrelated setup?
- Is this class doing orchestration, calculation, persistence, formatting, and communication all at once?

## 8. Open/Closed Principle (OCP)

OCP says software entities should be open for extension but closed for modification.

In practical C#, it means you should be able to add new behavior by adding new code, not constantly editing stable existing code.

## 9. OCP Recipe 1 - Replace Conditionals with Strategy

### Problem

Every new discount type requires editing this method.

```csharp
public class DiscountService
{
    public decimal ApplyDiscount(decimal amount, string customerType)
    {
        if (customerType == "Regular")
        {
            return amount;
        }

        if (customerType == "Premium")
        {
            return amount * 0.90m;
        }

        if (customerType == "Vip")
        {
            return amount * 0.80m;
        }

        throw new NotSupportedException($"Customer type '{customerType}' is not supported.");
    }
}
```

### OCP Version

```csharp
public interface IDiscountPolicy
{
    string CustomerType { get; }

    decimal Apply(decimal amount);
}

public class RegularDiscountPolicy : IDiscountPolicy
{
    public string CustomerType => "Regular";

    public decimal Apply(decimal amount) => amount;
}

public class PremiumDiscountPolicy : IDiscountPolicy
{
    public string CustomerType => "Premium";

    public decimal Apply(decimal amount) => amount * 0.90m;
}

public class VipDiscountPolicy : IDiscountPolicy
{
    public string CustomerType => "Vip";

    public decimal Apply(decimal amount) => amount * 0.80m;
}

public class DiscountService
{
    private readonly IReadOnlyDictionary<string, IDiscountPolicy> _policies;

    public DiscountService(IEnumerable<IDiscountPolicy> policies)
    {
        _policies = policies.ToDictionary(
            policy => policy.CustomerType,
            StringComparer.OrdinalIgnoreCase);
    }

    public decimal ApplyDiscount(decimal amount, string customerType)
    {
        if (!_policies.TryGetValue(customerType, out var policy))
        {
            throw new NotSupportedException($"Customer type '{customerType}' is not supported.");
        }

        return policy.Apply(amount);
    }
}
```

### Adding a New Type

```csharp
public class EmployeeDiscountPolicy : IDiscountPolicy
{
    public string CustomerType => "Employee";

    public decimal Apply(decimal amount) => amount * 0.70m;
}
```

You add a class and register it. You do not modify the existing discount service.

## 10. OCP Recipe 2 - Add New Report Export Formats

### Problem

```csharp
public class ReportExporter
{
    public string Export(Report report, string format)
    {
        return format switch
        {
            "csv" => $"{report.Title},{report.Total}",
            "json" => JsonSerializer.Serialize(report),
            _ => throw new NotSupportedException()
        };
    }
}
```

Every new format requires editing the switch.

### OCP Version

```csharp
public interface IReportFormatExporter
{
    string Format { get; }

    string Export(Report report);
}

public class CsvReportExporter : IReportFormatExporter
{
    public string Format => "csv";

    public string Export(Report report)
    {
        return $"{report.Title},{report.Total}";
    }
}

public class JsonReportExporter : IReportFormatExporter
{
    public string Format => "json";

    public string Export(Report report)
    {
        return JsonSerializer.Serialize(report);
    }
}

public class ReportExportService
{
    private readonly IReadOnlyDictionary<string, IReportFormatExporter> _exporters;

    public ReportExportService(IEnumerable<IReportFormatExporter> exporters)
    {
        _exporters = exporters.ToDictionary(
            exporter => exporter.Format,
            StringComparer.OrdinalIgnoreCase);
    }

    public string Export(Report report, string format)
    {
        if (!_exporters.TryGetValue(format, out var exporter))
        {
            throw new NotSupportedException($"Format '{format}' is not supported.");
        }

        return exporter.Export(report);
    }
}

public record Report(string Title, decimal Total);
```

### Adding XML Later

```csharp
public class XmlReportExporter : IReportFormatExporter
{
    public string Format => "xml";

    public string Export(Report report)
    {
        return $"<report><title>{report.Title}</title><total>{report.Total}</total></report>";
    }
}
```

## 11. OCP Recipe 3 - Specification Pattern

Specification objects let you add new filtering rules without editing the search service.

```csharp
public interface ISpecification<T>
{
    bool IsSatisfiedBy(T item);
}

public class ActiveCustomerSpecification : ISpecification<Customer>
{
    public bool IsSatisfiedBy(Customer customer) => customer.IsActive;
}

public class PremiumCustomerSpecification : ISpecification<Customer>
{
    public bool IsSatisfiedBy(Customer customer) => customer.Tier == "Premium";
}

public class CustomerSearchService
{
    public IReadOnlyList<Customer> Search(
        IEnumerable<Customer> customers,
        ISpecification<Customer> specification)
    {
        return customers
            .Where(specification.IsSatisfiedBy)
            .ToList();
    }
}

public record Customer(string Name, bool IsActive, string Tier);
```

### Usage

```csharp
var service = new CustomerSearchService();
var premiumCustomers = service.Search(customers, new PremiumCustomerSpecification());
```

## 12. OCP Recipe 4 - Decorators for Extension

Decorators add behavior without modifying the original class.

```csharp
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}

public class SmtpEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body)
    {
        // Send using SMTP...
        return Task.CompletedTask;
    }
}

public class LoggingEmailSender : IEmailSender
{
    private readonly IEmailSender _inner;
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(IEmailSender inner, ILogger<LoggingEmailSender> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        _logger.LogInformation("Sending email to {Recipient}", to);

        await _inner.SendAsync(to, subject, body);

        _logger.LogInformation("Email sent to {Recipient}", to);
    }
}
```

### Why This Follows OCP

`SmtpEmailSender` stays closed for modification. Logging is added by wrapping it.

## 13. OCP Recipe 5 - When a Switch Is Fine

Not every switch violates OCP.

```csharp
public string GetStatusLabel(OrderStatus status)
{
    return status switch
    {
        OrderStatus.Pending => "Pending",
        OrderStatus.Paid => "Paid",
        OrderStatus.Shipped => "Shipped",
        OrderStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };
}
```

This is fine when:

- The set of cases is small and stable.
- The behavior is simple.
- There is no plugin-like extension need.

OCP matters most where change is frequent and editing existing stable code creates risk.

## 14. OCP Checklist

Ask:

- Do I edit the same method every time a new type or rule is added?
- Is a growing `if` or `switch` hiding multiple behaviors?
- Can a new behavior be added as a new class?
- Would a strategy, specification, factory, or decorator make extension safer?
- Is the current conditional simple and stable enough to leave alone?

## 15. Liskov Substitution Principle (LSP)

LSP says objects of a subtype should be usable anywhere the base type is expected without breaking correctness.

In simple words: if code works with a base class or interface, it should not be surprised by a derived class.

## 16. LSP Recipe 1 - Classic Rectangle/Square Problem

### Problem

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
        set
        {
            base.Width = value;
            base.Height = value;
        }
    }

    public override int Height
    {
        get => base.Height;
        set
        {
            base.Width = value;
            base.Height = value;
        }
    }
}
```

This breaks callers that expect width and height to be independently settable.

```csharp
public static void Resize(Rectangle rectangle)
{
    rectangle.Width = 5;
    rectangle.Height = 10;

    // Expected 50, but Square gives 100.
    Console.WriteLine(rectangle.Area);
}
```

### LSP Version

```csharp
public interface IShape
{
    int Area { get; }
}

public class Rectangle : IShape
{
    public Rectangle(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public int Area => Width * Height;
}

public class Square : IShape
{
    public Square(int side)
    {
        Side = side;
    }

    public int Side { get; }

    public int Area => Side * Side;
}
```

Now both shapes share only the behavior that is truly substitutable: area.

## 17. LSP Recipe 2 - Do Not Throw for Unsupported Base Behavior

### Problem

```csharp
public abstract class Bird
{
    public abstract void Fly();
}

public class Sparrow : Bird
{
    public override void Fly()
    {
        Console.WriteLine("Sparrow is flying.");
    }
}

public class Penguin : Bird
{
    public override void Fly()
    {
        throw new NotSupportedException("Penguins cannot fly.");
    }
}
```

`Penguin` cannot safely substitute `Bird` if `Bird` promises flying.

### LSP Version

```csharp
public abstract class Bird
{
    public string Name { get; }

    protected Bird(string name)
    {
        Name = name;
    }
}

public interface IFlyingBird
{
    void Fly();
}

public class Sparrow : Bird, IFlyingBird
{
    public Sparrow() : base("Sparrow")
    {
    }

    public void Fly()
    {
        Console.WriteLine("Sparrow is flying.");
    }
}

public class Penguin : Bird
{
    public Penguin() : base("Penguin")
    {
    }
}
```

Only birds that can fly implement `IFlyingBird`.

## 18. LSP Recipe 3 - Preserve Contracts

If a base class accepts any positive amount, the subtype should not secretly reject valid base inputs.

### Problem

```csharp
public class PaymentProcessor
{
    public virtual PaymentResult Charge(decimal amount)
    {
        if (amount <= 0)
        {
            return PaymentResult.Failed("Amount must be positive.");
        }

        return PaymentResult.Success();
    }
}

public class MinimumTenDollarPaymentProcessor : PaymentProcessor
{
    public override PaymentResult Charge(decimal amount)
    {
        if (amount < 10)
        {
            return PaymentResult.Failed("Minimum amount is $10.");
        }

        return base.Charge(amount);
    }
}
```

The subtype strengthens the precondition. Code that validly charges `$5` through `PaymentProcessor` can fail unexpectedly.

### Better Design

```csharp
public interface IPaymentProcessor
{
    PaymentResult Charge(decimal amount);
}

public interface IPaymentRule
{
    ValidationResult Validate(decimal amount);
}

public class PositiveAmountRule : IPaymentRule
{
    public ValidationResult Validate(decimal amount)
    {
        return amount > 0
            ? ValidationResult.Success()
            : ValidationResult.Failure("Amount must be positive.");
    }
}

public class MinimumAmountRule : IPaymentRule
{
    private readonly decimal _minimumAmount;

    public MinimumAmountRule(decimal minimumAmount)
    {
        _minimumAmount = minimumAmount;
    }

    public ValidationResult Validate(decimal amount)
    {
        return amount >= _minimumAmount
            ? ValidationResult.Success()
            : ValidationResult.Failure($"Minimum amount is {_minimumAmount:C}.");
    }
}
```

Keep validation rules explicit instead of hiding stricter behavior inside a subtype.

## 19. LSP Recipe 4 - Avoid Overriding with Weaker Results

### Problem

```csharp
public class ReportGenerator
{
    public virtual string Generate()
    {
        return "Report content";
    }
}

public class EmptyReportGenerator : ReportGenerator
{
    public override string Generate()
    {
        return string.Empty;
    }
}
```

If callers rely on a non-empty report, the subtype weakens the postcondition.

### Better

```csharp
public interface IReportGenerator
{
    Report Generate();
}

public class StandardReportGenerator : IReportGenerator
{
    public Report Generate()
    {
        return new Report("Standard Report", "Report content");
    }
}

public class NoDataReportGenerator : IReportGenerator
{
    public Report Generate()
    {
        return Report.Empty("No data available.");
    }
}

public record Report(string Title, string Content)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Content);

    public static Report Empty(string reason) => new("Empty Report", reason);
}
```

Make special states explicit in the return type.

## 20. LSP Recipe 5 - Prefer Composition over Risky Inheritance

### Problem

```csharp
public class ReadOnlyList<T> : List<T>
{
    public new void Add(T item)
    {
        throw new NotSupportedException("List is read-only.");
    }
}
```

This violates expectations of `List<T>`.

### Better

```csharp
public class ReadOnlyItems<T>
{
    private readonly IReadOnlyList<T> _items;

    public ReadOnlyItems(IEnumerable<T> items)
    {
        _items = items.ToList();
    }

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    public IReadOnlyList<T> AsReadOnly() => _items;
}
```

Or use built-in interfaces:

```csharp
IReadOnlyList<string> names = new List<string> { "Asha", "Noah" };
```

## 21. LSP Checklist

Ask:

- Can this subtype be used anywhere the base type is expected?
- Does the subtype throw `NotSupportedException` for base behavior?
- Does the subtype require stricter inputs than the base type?
- Does the subtype return weaker or surprising outputs?
- Am I using inheritance only for code reuse?
- Would composition or a smaller interface be safer?

## 22. Interface Segregation Principle (ISP)

ISP says clients should not be forced to depend on methods they do not use.

In practical C#, prefer focused interfaces over large "do everything" interfaces.

## 23. ISP Recipe 1 - Split a Fat Worker Interface

### Problem

```csharp
public interface IWorker
{
    void Work();

    void Eat();

    void Sleep();
}

public class HumanWorker : IWorker
{
    public void Work()
    {
        Console.WriteLine("Human working.");
    }

    public void Eat()
    {
        Console.WriteLine("Human eating.");
    }

    public void Sleep()
    {
        Console.WriteLine("Human sleeping.");
    }
}

public class RobotWorker : IWorker
{
    public void Work()
    {
        Console.WriteLine("Robot working.");
    }

    public void Eat()
    {
        throw new NotSupportedException();
    }

    public void Sleep()
    {
        throw new NotSupportedException();
    }
}
```

### ISP Version

```csharp
public interface IWorkable
{
    void Work();
}

public interface IEatable
{
    void Eat();
}

public interface ISleepable
{
    void Sleep();
}

public class HumanWorker : IWorkable, IEatable, ISleepable
{
    public void Work()
    {
        Console.WriteLine("Human working.");
    }

    public void Eat()
    {
        Console.WriteLine("Human eating.");
    }

    public void Sleep()
    {
        Console.WriteLine("Human sleeping.");
    }
}

public class RobotWorker : IWorkable
{
    public void Work()
    {
        Console.WriteLine("Robot working.");
    }
}
```

Now consumers ask only for what they need.

```csharp
public class ShiftManager
{
    public void StartShift(IWorkable worker)
    {
        worker.Work();
    }
}
```

## 24. ISP Recipe 2 - Split Repository Read and Write Interfaces

### Problem

```csharp
public interface IRepository<T>
{
    Task<T?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<T>> GetAllAsync();

    Task AddAsync(T item);

    Task UpdateAsync(T item);

    Task DeleteAsync(Guid id);
}
```

Some services only read, but they depend on write methods too.

### ISP Version

```csharp
public interface IReadRepository<T>
{
    Task<T?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<T>> GetAllAsync();
}

public interface IWriteRepository<T>
{
    Task AddAsync(T item);

    Task UpdateAsync(T item);

    Task DeleteAsync(Guid id);
}

public class ProductQueryService
{
    private readonly IReadRepository<Product> _products;

    public ProductQueryService(IReadRepository<Product> products)
    {
        _products = products;
    }

    public Task<Product?> GetProductAsync(Guid id)
    {
        return _products.GetByIdAsync(id);
    }
}

public class ProductCommandService
{
    private readonly IWriteRepository<Product> _products;

    public ProductCommandService(IWriteRepository<Product> products)
    {
        _products = products;
    }

    public Task CreateProductAsync(Product product)
    {
        return _products.AddAsync(product);
    }
}
```

### Implementation Can Still Implement Both

```csharp
public class EfProductRepository :
    IReadRepository<Product>,
    IWriteRepository<Product>
{
    public Task<Product?> GetByIdAsync(Guid id)
    {
        // Query database...
        return Task.FromResult<Product?>(null);
    }

    public Task<IReadOnlyList<Product>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());
    }

    public Task AddAsync(Product item)
    {
        // Add product...
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Product item)
    {
        // Update product...
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        // Delete product...
        return Task.CompletedTask;
    }
}
```

## 25. ISP Recipe 3 - Split Printer Capabilities

### Problem

```csharp
public interface IMultiFunctionPrinter
{
    void Print(Document document);

    void Scan(Document document);

    void Fax(Document document);
}

public class BasicPrinter : IMultiFunctionPrinter
{
    public void Print(Document document)
    {
        Console.WriteLine("Printing.");
    }

    public void Scan(Document document)
    {
        throw new NotSupportedException();
    }

    public void Fax(Document document)
    {
        throw new NotSupportedException();
    }
}
```

### ISP Version

```csharp
public interface IPrinter
{
    void Print(Document document);
}

public interface IScanner
{
    void Scan(Document document);
}

public interface IFaxMachine
{
    void Fax(Document document);
}

public class BasicPrinter : IPrinter
{
    public void Print(Document document)
    {
        Console.WriteLine("Printing.");
    }
}

public class OfficeMachine : IPrinter, IScanner, IFaxMachine
{
    public void Print(Document document)
    {
        Console.WriteLine("Printing.");
    }

    public void Scan(Document document)
    {
        Console.WriteLine("Scanning.");
    }

    public void Fax(Document document)
    {
        Console.WriteLine("Faxing.");
    }
}

public record Document(string Name, byte[] Content);
```

## 26. ISP Recipe 4 - Role Interfaces for Users

### Problem

```csharp
public interface IUserActions
{
    void ViewDashboard();

    void ManageUsers();

    void ApprovePayments();

    void ExportReports();
}
```

Many users cannot perform all actions.

### ISP Version

```csharp
public interface IDashboardViewer
{
    void ViewDashboard();
}

public interface IUserManager
{
    void ManageUsers();
}

public interface IPaymentApprover
{
    void ApprovePayments();
}

public interface IReportExporter
{
    void ExportReports();
}

public class AdminUser :
    IDashboardViewer,
    IUserManager,
    IPaymentApprover,
    IReportExporter
{
    public void ViewDashboard() { }

    public void ManageUsers() { }

    public void ApprovePayments() { }

    public void ExportReports() { }
}

public class FinanceUser : IDashboardViewer, IPaymentApprover, IReportExporter
{
    public void ViewDashboard() { }

    public void ApprovePayments() { }

    public void ExportReports() { }
}
```

## 27. ISP Recipe 5 - Interface Composition

Small interfaces can be composed when a caller truly needs the larger capability.

```csharp
public interface IReadableFile
{
    string ReadAllText(string path);
}

public interface IWritableFile
{
    void WriteAllText(string path, string content);
}

public interface IFileStorage : IReadableFile, IWritableFile
{
}

public class LocalFileStorage : IFileStorage
{
    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public void WriteAllText(string path, string content)
    {
        File.WriteAllText(path, content);
    }
}
```

### Consumers Stay Focused

```csharp
public class ConfigLoader
{
    private readonly IReadableFile _files;

    public ConfigLoader(IReadableFile files)
    {
        _files = files;
    }
}

public class BackupWriter
{
    private readonly IWritableFile _files;

    public BackupWriter(IWritableFile files)
    {
        _files = files;
    }
}
```

## 28. ISP Recipe 6 - When Not to Split

Avoid creating tiny interfaces that always travel together and have no independent clients.

```csharp
public interface IFirstNameProvider
{
    string FirstName { get; }
}

public interface ILastNameProvider
{
    string LastName { get; }
}

public interface IEmailProvider
{
    string Email { get; }
}
```

This is usually too granular if every consumer needs the full user profile.

```csharp
public interface IUserProfile
{
    string FirstName { get; }

    string LastName { get; }

    string Email { get; }
}
```

ISP is about client needs, not making every property its own interface.

## 29. ISP Checklist

Ask:

- Does this interface force implementers to throw `NotSupportedException`?
- Do clients use only one or two methods from a large interface?
- Are read and write concerns mixed?
- Are optional capabilities modeled as required methods?
- Can role-based capabilities be split into focused interfaces?
- Have I split so much that every interface is meaningless alone?

## 30. How the Principles Work Together

SOLID principles often reinforce each other:

- SRP makes responsibilities easier to isolate.
- OCP uses abstractions and composition to extend behavior.
- LSP keeps inheritance and polymorphism honest.
- ISP prevents abstractions from becoming too large.
- DIP keeps high-level policy independent from low-level details.

## 31. Refactoring Decision Guide

| Symptom | Likely Principle | Refactoring Move |
|---|---|---|
| One class has unrelated reasons to change | SRP | Split responsibilities |
| You edit a growing `switch` for every new rule | OCP | Strategy, specification, or polymorphism |
| Subclass throws for inherited behavior | LSP | Smaller interface or composition |
| Interface has methods most clients ignore | ISP | Split into role interfaces |
| Business logic creates infrastructure classes | DIP | Inject abstractions |

## 32. Practical SOLID Rules

- Start simple; do not design for imaginary variation.
- Refactor toward SOLID when change pressure appears.
- Prefer composition when inheritance creates surprising behavior.
- Keep interfaces small, but not microscopic.
- Put business rules where they can be tested without infrastructure.
- Use abstractions to protect stable policy from unstable details.
- Do not confuse more classes with better design.

## 33. Final Takeaway

SOLID is not a checklist for making code complicated. It is a set of pressure-release valves for change.

Use SRP when responsibilities blur. Use OCP when new cases keep modifying old code. Use LSP when inheritance surprises callers. Use ISP when interfaces become too heavy. Use DIP when important logic depends on concrete infrastructure.
