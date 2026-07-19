# Hard Enterprise-Level Open/Closed Principle (OCP) Examples in C#

The **Open/Closed Principle (OCP)** means a stable module should be **open for extension** but **closed for modification**. In enterprise applications, this prevents central `switch` statements from changing whenever a country, partner, workflow, policy, or integration is added.

This guide has four realistic examples. Each uses a stable orchestrator and a narrow extension contract. New behavior is introduced through a new implementation, not by editing the orchestrator.

---

## Example 1 — Multi-country tax rules

### The OCP violation

```csharp
public decimal CalculateTax(Invoice invoice) => invoice.CountryCode switch
{
    "DE" => invoice.NetAmount * 0.19m,
    "IN" => invoice.NetAmount * 0.18m,
    "US" => CalculateUsSalesTax(invoice),
    _ => throw new NotSupportedException(invoice.CountryCode)
};
```

Every new jurisdiction modifies a high-risk billing class.

### Open extension contract

```csharp
public sealed record Invoice(
    Guid Id, string TenantId, string CountryCode,
    decimal NetAmount, string Currency, bool CustomerIsExempt);

public sealed record TaxLine(string RuleCode, decimal Amount, string Currency);

public interface ITaxRule
{
    int Order { get; }
    string RuleCode { get; }
    bool AppliesTo(Invoice invoice);
    ValueTask<IReadOnlyCollection<TaxLine>> CalculateAsync(Invoice invoice, CancellationToken ct);
}
```

### Stable engine — do not add country checks here

```csharp
public sealed class TaxEngine
{
    private readonly IEnumerable<ITaxRule> _rules;
    private readonly ILogger<TaxEngine> _logger;

    public TaxEngine(IEnumerable<ITaxRule> rules, ILogger<TaxEngine> logger)
        => (_rules, _logger) = (rules, logger);

    public async Task<decimal> CalculateAsync(Invoice invoice, CancellationToken ct)
    {
        decimal total = 0;

        foreach (var rule in _rules.Where(x => x.AppliesTo(invoice))
                     .OrderBy(x => x.Order).ThenBy(x => x.RuleCode, StringComparer.Ordinal))
        {
            var taxLines = await rule.CalculateAsync(invoice, ct);
            total += taxLines.Sum(x => x.Amount);
            _logger.LogInformation("Rule {RuleCode} calculated invoice {InvoiceId}", rule.RuleCode, invoice.Id);
        }
        return total;
    }
}
```

### Extensions: German VAT and an ACME-specific eco levy

```csharp
public sealed class GermanVatRule : ITaxRule
{
    public int Order => 100;
    public string RuleCode => "DE.VAT.STANDARD";

    public bool AppliesTo(Invoice invoice) =>
        invoice.CountryCode.Equals("DE", StringComparison.OrdinalIgnoreCase) && !invoice.CustomerIsExempt;

    public ValueTask<IReadOnlyCollection<TaxLine>> CalculateAsync(Invoice invoice, CancellationToken ct)
    {
        var vat = decimal.Round(invoice.NetAmount * 0.19m, 2, MidpointRounding.AwayFromZero);
        return ValueTask.FromResult<IReadOnlyCollection<TaxLine>>([new(RuleCode, vat, invoice.Currency)]);
    }
}

public sealed class AcmeEcoLevyRule : ITaxRule
{
    public int Order => 200;
    public string RuleCode => "ACME.DE.ECO_LEVY";
    public bool AppliesTo(Invoice invoice) => invoice.TenantId == "acme" && invoice.CountryCode == "DE";

    public ValueTask<IReadOnlyCollection<TaxLine>> CalculateAsync(Invoice invoice, CancellationToken ct)
    {
        var levy = decimal.Round(invoice.NetAmount * 0.005m, 2);
        return ValueTask.FromResult<IReadOnlyCollection<TaxLine>>([new(RuleCode, levy, invoice.Currency)]);
    }
}
```

To add French VAT, create `FrenchVatRule : ITaxRule` and register it. `TaxEngine` remains unchanged.

---

## Example 2 — Payment-provider routing with failover

An enterprise checkout supports multiple payment service providers and routes by currency, country, merchant, card type, or transaction size.

### Stable contract and router

```csharp
public sealed record PaymentRequest(
    Guid PaymentId, string MerchantId, string CountryCode,
    string Currency, decimal Amount, string PaymentMethodToken);

public sealed record PaymentResult(bool IsApproved, string? ProviderTransactionId, string? FailureReason);

public interface IPaymentProvider
{
    string ProviderName { get; }
    int Priority { get; }
    bool CanProcess(PaymentRequest request);
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct);
}

public sealed class PaymentRouter
{
    private readonly IReadOnlyCollection<IPaymentProvider> _providers;
    public PaymentRouter(IEnumerable<IPaymentProvider> providers) => _providers = providers.ToArray();

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct)
    {
        var candidates = _providers.Where(p => p.CanProcess(request)).OrderBy(p => p.Priority).ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException("No payment route is configured.");

        foreach (var provider in candidates)
        {
            var result = await provider.ChargeAsync(request, ct);
            // A business decline must not fail over: a retry at another PSP could double-charge.
            if (result.IsApproved || result.FailureReason == "DECLINED") return result;
        }
        return new(false, null, "All eligible providers unavailable");
    }
}
```

### Extensions: EU and India payment providers

```csharp
public sealed class AdyenEuropeProvider : IPaymentProvider
{
    private readonly AdyenClient _client;
    public AdyenEuropeProvider(AdyenClient client) => _client = client;
    public string ProviderName => "AdyenEU";
    public int Priority => 10;
    public bool CanProcess(PaymentRequest r) => r.Currency is "EUR" or "GBP" && r.CountryCode != "IN";

    public async Task<PaymentResult> ChargeAsync(PaymentRequest r, CancellationToken ct)
    {
        var response = await _client.AuthorizeAsync(r, ct);
        return new(response.Approved, response.PspReference, response.RefusalReason);
    }
}

public sealed class RazorpayIndiaProvider : IPaymentProvider
{
    private readonly RazorpayClient _client;
    public RazorpayIndiaProvider(RazorpayClient client) => _client = client;
    public string ProviderName => "RazorpayIN";
    public int Priority => 10;
    public bool CanProcess(PaymentRequest r) => r.CountryCode == "IN" && r.Currency == "INR";

    public async Task<PaymentResult> ChargeAsync(PaymentRequest r, CancellationToken ct)
    {
        var response = await _client.CreatePaymentAsync(r, ct);
        return new(response.Captured, response.Id, response.ErrorCode);
    }
}
```

Adding a new gateway requires an `IPaymentProvider` plus DI registration. `PaymentRouter` does not change.

---

## Example 3 — Fine-grained authorization policies

Avoid encoding tenant exceptions, ownership checks, and compliance roles in every API endpoint. Express them as independently testable rules.

### Stable policy evaluator

```csharp
public sealed record AccessRequest(
    ClaimsPrincipal User, string Action, string ResourceType, string ResourceId, string TenantId);

public sealed record AuthorizationDecision(bool Allowed, string Reason)
{
    public static AuthorizationDecision Allow(string reason) => new(true, reason);
    public static AuthorizationDecision Deny(string reason) => new(false, reason);
}

public interface IAuthorizationRule
{
    int Order { get; }
    bool Handles(AccessRequest request);
    Task<AuthorizationDecision?> EvaluateAsync(AccessRequest request, CancellationToken ct);
}

public sealed class PolicyEvaluator
{
    private readonly IEnumerable<IAuthorizationRule> _rules;
    public PolicyEvaluator(IEnumerable<IAuthorizationRule> rules) => _rules = rules;

    public async Task<AuthorizationDecision> AuthorizeAsync(AccessRequest request, CancellationToken ct)
    {
        foreach (var rule in _rules.Where(x => x.Handles(request)).OrderBy(x => x.Order))
        {
            var decision = await rule.EvaluateAsync(request, ct);
            if (decision is not null) return decision; // null = no opinion
        }
        return AuthorizationDecision.Deny("No authorization policy granted access.");
    }
}
```

### Extensions: tenant boundary and document-owner read rule

```csharp
public sealed class TenantBoundaryRule : IAuthorizationRule
{
    public int Order => 0; // Security guard: always first.
    public bool Handles(AccessRequest request) => true;

    public Task<AuthorizationDecision?> EvaluateAsync(AccessRequest request, CancellationToken ct)
    {
        var userTenant = request.User.FindFirst("tenant_id")?.Value;
        return Task.FromResult<AuthorizationDecision?>(userTenant == request.TenantId
            ? null : AuthorizationDecision.Deny("Cross-tenant access is prohibited."));
    }
}

public interface IDocumentOwnershipReader
{
    Task<string?> GetOwnerIdAsync(string documentId, CancellationToken ct);
}

public sealed class DocumentOwnerReadRule : IAuthorizationRule
{
    private readonly IDocumentOwnershipReader _ownership;
    public DocumentOwnerReadRule(IDocumentOwnershipReader ownership) => _ownership = ownership;
    public int Order => 100;
    public bool Handles(AccessRequest r) => r.ResourceType == "document" && r.Action == "read";

    public async Task<AuthorizationDecision?> EvaluateAsync(AccessRequest r, CancellationToken ct)
    {
        var ownerId = await _ownership.GetOwnerIdAsync(r.ResourceId, ct);
        var userId = r.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return ownerId == userId ? AuthorizationDecision.Allow("The user owns the document.") : null;
    }
}
```

Adding a “legal-hold reviewer can read documents” policy is a new `IAuthorizationRule`; controllers and the evaluator stay closed.

---

## Example 4 — Report export pipeline with partner-specific renderers

A reporting service can export a domain report as CSV, PDF, XBRL, or a partner-specific fixed-width file. The pipeline should not acquire a new `if (format == ...)` branch each time.

### Stable export service

```csharp
public sealed record ReportData(string TenantId, DateOnly From, DateOnly To, IReadOnlyCollection<ReportRow> Rows);
public sealed record ReportRow(string AccountCode, decimal Debit, decimal Credit);
public sealed record ExportArtifact(string FileName, string ContentType, Stream Content);

public interface IReportRenderer
{
    string Format { get; }
    bool Supports(string format, string tenantId);
    Task<ExportArtifact> RenderAsync(ReportData data, CancellationToken ct);
}

public sealed class ReportExportService
{
    private readonly IEnumerable<IReportRenderer> _renderers;
    public ReportExportService(IEnumerable<IReportRenderer> renderers) => _renderers = renderers;

    public Task<ExportArtifact> ExportAsync(string format, ReportData report, CancellationToken ct)
    {
        var renderer = _renderers.SingleOrDefault(x => x.Supports(format, report.TenantId));
        if (renderer is null) throw new NotSupportedException($"Export format '{format}' is unavailable.");
        return renderer.RenderAsync(report, ct);
    }
}
```

### Extensions: CSV and ACME regulatory XBRL

```csharp
public sealed class CsvReportRenderer : IReportRenderer
{
    public string Format => "csv";
    public bool Supports(string format, string tenantId) => format.Equals(Format, StringComparison.OrdinalIgnoreCase);

    public Task<ExportArtifact> RenderAsync(ReportData data, CancellationToken ct)
    {
        var builder = new StringBuilder("AccountCode,Debit,Credit\n");
        foreach (var row in data.Rows) builder.AppendLine($"{row.AccountCode},{row.Debit},{row.Credit}");
        return Task.FromResult(new ExportArtifact($"trial-balance-{data.From:yyyyMMdd}.csv", "text/csv",
            new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()))));
    }
}

public sealed class AcmeXbrlRenderer : IReportRenderer
{
    public string Format => "xbrl";
    public bool Supports(string format, string tenantId) => tenantId == "acme" && format.Equals(Format, StringComparison.OrdinalIgnoreCase);

    public Task<ExportArtifact> RenderAsync(ReportData data, CancellationToken ct)
    {
        var xml = $"<xbrl><entity>{data.TenantId}</entity><period>{data.From:yyyy-MM-dd}</period></xbrl>";
        return Task.FromResult(new ExportArtifact($"acme-regulatory-{data.To:yyyyMMdd}.xbrl", "application/xml",
            new MemoryStream(Encoding.UTF8.GetBytes(xml))));
    }
}
```

Adding a PDF or fixed-width renderer changes only the renderer set.

---

## Dependency-injection composition

```csharp
services.AddScoped<ITaxRule, GermanVatRule>();
services.AddScoped<ITaxRule, AcmeEcoLevyRule>();
services.AddScoped<TaxEngine>();

services.AddScoped<IPaymentProvider, AdyenEuropeProvider>();
services.AddScoped<IPaymentProvider, RazorpayIndiaProvider>();
services.AddScoped<PaymentRouter>();

services.AddScoped<IAuthorizationRule, TenantBoundaryRule>();
services.AddScoped<IAuthorizationRule, DocumentOwnerReadRule>();
services.AddScoped<PolicyEvaluator>();

services.AddScoped<IReportRenderer, CsvReportRenderer>();
services.AddScoped<IReportRenderer, AcmeXbrlRenderer>();
services.AddScoped<ReportExportService>();
```

For independently deployed plug-ins, load only reviewed, versioned, and allow-listed assemblies. Never dynamically load an arbitrary customer DLL into the web process.

## Enterprise OCP checklist

| Design concern | Recommended approach |
|---|---|
| New behavior | Add an implementation of a narrow interface |
| Execution order | Make it explicit with `Order` / `Priority` |
| Selection | Use `CanProcess`, `AppliesTo`, or `Supports` |
| Observability | Log extension identity, correlation ID, outcome, and latency |
| Failure behavior | Decide intentionally whether to fail, skip, retry, or fail over |
| Contracts | Keep abstractions small; use contract tests for extensions |
| Security | Trust, version, review, and allow-list extension packages |

The shared structure is deliberate: the orchestrator owns the invariant workflow, while extension implementations own changing business policy. That is OCP in a form that scales across teams and releases.
