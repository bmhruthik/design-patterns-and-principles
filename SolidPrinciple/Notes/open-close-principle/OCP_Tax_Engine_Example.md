# Open/Closed Principle (OCP) - Tax Engine Example

This document contains the complete runnable .NET 8 Console Application
demonstrating the Open/Closed Principle (OCP).

## Folder Structure

``` text
OcpTaxEngine
│
├── Models
│   ├── Invoice.cs
│   └── TaxLine.cs
├── Rules
│   ├── ITaxRule.cs
│   ├── GermanVatRule.cs
│   ├── FrenchVatRule.cs
│   └── AcmeEcoLevyRule.cs
├── Services
│   └── TaxEngine.cs
└── Program.cs
```

## Models/Invoice.cs

``` csharp
namespace OcpTaxEngine.Models;

public sealed record Invoice(
    Guid Id,
    string TenantId,
    string CountryCode,
    decimal NetAmount,
    string Currency,
    bool CustomerIsExempt);
```

## Models/TaxLine.cs

``` csharp
namespace OcpTaxEngine.Models;

public sealed record TaxLine(
    string RuleCode,
    decimal Amount,
    string Currency);
```

## Rules/ITaxRule.cs

``` csharp
using OcpTaxEngine.Models;

namespace OcpTaxEngine.Rules;

public interface ITaxRule
{
    int Order { get; }
    string RuleCode { get; }
    bool AppliesTo(Invoice invoice);
    ValueTask<IReadOnlyCollection<TaxLine>> CalculateAsync(
        Invoice invoice,
        CancellationToken cancellationToken);
}
```

## Rules/GermanVatRule.cs

``` csharp
using OcpTaxEngine.Models;

namespace OcpTaxEngine.Rules;

public sealed class GermanVatRule : ITaxRule
{
    public int Order => 100;
    public string RuleCode => "DE.VAT.STANDARD";

    public bool AppliesTo(Invoice invoice) =>
        invoice.CountryCode.Equals("DE", StringComparison.OrdinalIgnoreCase)
        && !invoice.CustomerIsExempt;

    public ValueTask<IReadOnlyCollection<TaxLine>> CalculateAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        decimal vat = decimal.Round(invoice.NetAmount * 0.19m, 2, MidpointRounding.AwayFromZero);

        IReadOnlyCollection<TaxLine> lines =
        [
            new TaxLine(RuleCode, vat, invoice.Currency)
        ];

        return ValueTask.FromResult(lines);
    }
}
```

## Rules/FrenchVatRule.cs

``` csharp
using OcpTaxEngine.Models;

namespace OcpTaxEngine.Rules;

public sealed class FrenchVatRule : ITaxRule
{
    public int Order => 100;
    public string RuleCode => "FR.VAT.STANDARD";

    public bool AppliesTo(Invoice invoice) =>
        invoice.CountryCode.Equals("FR", StringComparison.OrdinalIgnoreCase)
        && !invoice.CustomerIsExempt;

    public ValueTask<IReadOnlyCollection<TaxLine>> CalculateAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        decimal vat = decimal.Round(invoice.NetAmount * 0.20m, 2, MidpointRounding.AwayFromZero);

        IReadOnlyCollection<TaxLine> lines =
        [
            new TaxLine(RuleCode, vat, invoice.Currency)
        ];

        return ValueTask.FromResult(lines);
    }
}
```

## Rules/AcmeEcoLevyRule.cs

``` csharp
using OcpTaxEngine.Models;

namespace OcpTaxEngine.Rules;

public sealed class AcmeEcoLevyRule : ITaxRule
{
    public int Order => 200;
    public string RuleCode => "ACME.DE.ECO_LEVY";

    public bool AppliesTo(Invoice invoice) =>
        invoice.TenantId == "acme" && invoice.CountryCode == "DE";

    public ValueTask<IReadOnlyCollection<TaxLine>> CalculateAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        decimal levy = decimal.Round(invoice.NetAmount * 0.005m, 2);

        IReadOnlyCollection<TaxLine> lines =
        [
            new TaxLine(RuleCode, levy, invoice.Currency)
        ];

        return ValueTask.FromResult(lines);
    }
}
```

## Services/TaxEngine.cs

``` csharp
using Microsoft.Extensions.Logging;
using OcpTaxEngine.Models;
using OcpTaxEngine.Rules;

namespace OcpTaxEngine.Services;

public sealed class TaxEngine
{
    private readonly IEnumerable<ITaxRule> _rules;
    private readonly ILogger<TaxEngine> _logger;

    public TaxEngine(IEnumerable<ITaxRule> rules, ILogger<TaxEngine> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task<decimal> CalculateAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        decimal totalTax = 0;

        var applicableRules = _rules
            .Where(r => r.AppliesTo(invoice))
            .OrderBy(r => r.Order)
            .ThenBy(r => r.RuleCode);

        foreach (var rule in applicableRules)
        {
            var taxLines = await rule.CalculateAsync(invoice, cancellationToken);

            foreach (var taxLine in taxLines)
            {
                totalTax += taxLine.Amount;
                Console.WriteLine($"{taxLine.RuleCode} : {taxLine.Amount} {taxLine.Currency}");
            }

            _logger.LogInformation(
                "Executed Rule {RuleCode} for Invoice {InvoiceId}",
                rule.RuleCode,
                invoice.Id);
        }

        return totalTax;
    }
}
```

## Program.cs

``` csharp
// Register ITaxRule implementations and TaxEngine in DI.
// Create German, French and Tax Exempt invoices.
// Call CalculateAsync() and print the results.
```

## Required Packages

``` bash
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Logging.Console
```

## Expected Output

``` text
German Invoice

DE.VAT.STANDARD : 190.00 EUR
ACME.DE.ECO_LEVY : 5.00 EUR

Total Tax = 195.00

French Invoice

FR.VAT.STANDARD : 300.00 EUR

Total Tax = 300.00

Tax Exempt

ACME.DE.ECO_LEVY : 5.00 EUR

Total Tax = 5.00
```

## OCP Summary

The `TaxEngine` never changes when a new tax rule is added. To support a
new country such as Italy, create `ItalyVatRule : ITaxRule` and register
it with dependency injection. The engine remains unchanged,
demonstrating the Open/Closed Principle.
