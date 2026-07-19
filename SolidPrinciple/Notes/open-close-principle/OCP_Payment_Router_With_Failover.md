# OCP Example 2 - Payment Provider Routing with Failover

## Folder Structure

``` text
PaymentRoutingDemo/
 ├─ Models/
 │   ├─ PaymentRequest.cs
 │   └─ PaymentResult.cs
 ├─ Clients/
 │   ├─ AdyenClient.cs
 │   └─ RazorpayClient.cs
 ├─ Providers/
 │   ├─ IPaymentProvider.cs
 │   ├─ AdyenEuropeProvider.cs
 │   ├─ RazorpayIndiaProvider.cs
 │   └─ BackupEuropeProvider.cs
 ├─ Services/
 │   └─ PaymentRouter.cs
 └─ Program.cs
```

## Models

``` csharp
public sealed record PaymentRequest(Guid PaymentId,string MerchantId,string CountryCode,
string Currency,decimal Amount,string PaymentMethodToken);

public sealed record PaymentResult(bool IsApproved,string? ProviderTransactionId,string? FailureReason);
```

## Contract

``` csharp
public interface IPaymentProvider
{
    string ProviderName { get; }
    int Priority { get; }
    bool CanProcess(PaymentRequest request);
    Task<PaymentResult> ChargeAsync(PaymentRequest request,CancellationToken ct);
}
```

## Stable Router

``` csharp
public sealed class PaymentRouter
{
    private readonly IReadOnlyCollection<IPaymentProvider> _providers;
    public PaymentRouter(IEnumerable<IPaymentProvider> providers)=>_providers=providers.ToArray();

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request,CancellationToken ct=default)
    {
        var candidates=_providers.Where(p=>p.CanProcess(request))
            .OrderBy(p=>p.Priority).ToArray();

        if(!candidates.Any())
            throw new InvalidOperationException("No payment route is configured.");

        foreach(var provider in candidates)
        {
            Console.WriteLine($"Trying {provider.ProviderName}");
            var result=await provider.ChargeAsync(request,ct);

            if(result.IsApproved)
                return result;

            if(result.FailureReason=="DECLINED")
                return result;
        }

        return new(false,null,"All eligible providers unavailable");
    }
}
```

## Mock Clients

``` csharp
public sealed class AdyenClient
{
    public async Task<(bool Approved,string Ref,string Reason)> AuthorizeAsync(PaymentRequest r,CancellationToken ct)
    {
        await Task.Delay(200,ct);
        return (false,"","SERVICE_UNAVAILABLE");
    }
}

public sealed class RazorpayClient
{
    public async Task<(bool Captured,string Id,string Error)> CreatePaymentAsync(PaymentRequest r,CancellationToken ct)
    {
        await Task.Delay(200,ct);
        return (true,"RZP-10001","");
    }
}
```

## Providers

``` csharp
public sealed class AdyenEuropeProvider:IPaymentProvider
{
    private readonly AdyenClient _client;
    public AdyenEuropeProvider(AdyenClient c)=>_client=c;
    public string ProviderName=>"AdyenEU";
    public int Priority=>10;

    public bool CanProcess(PaymentRequest r)=>
        (r.Currency=="EUR"||r.Currency=="GBP")&&r.CountryCode!="IN";

    public async Task<PaymentResult> ChargeAsync(PaymentRequest r,CancellationToken ct)
    {
        var x=await _client.AuthorizeAsync(r,ct);
        return new(x.Approved,x.Ref,x.Reason);
    }
}

public sealed class RazorpayIndiaProvider:IPaymentProvider
{
    private readonly RazorpayClient _client;
    public RazorpayIndiaProvider(RazorpayClient c)=>_client=c;
    public string ProviderName=>"RazorpayIN";
    public int Priority=>10;

    public bool CanProcess(PaymentRequest r)=>
        r.CountryCode=="IN"&&r.Currency=="INR";

    public async Task<PaymentResult> ChargeAsync(PaymentRequest r,CancellationToken ct)
    {
        var x=await _client.CreatePaymentAsync(r,ct);
        return new(x.Captured,x.Id,x.Error);
    }
}

public sealed class BackupEuropeProvider:IPaymentProvider
{
    public string ProviderName=>"StripeBackup";
    public int Priority=>20;
    public bool CanProcess(PaymentRequest r)=>r.Currency=="EUR";

    public Task<PaymentResult> ChargeAsync(PaymentRequest r,CancellationToken ct)=>
        Task.FromResult(new PaymentResult(true,"STRIPE-90001",null));
}
```

## Program.cs

``` csharp
services.AddSingleton<AdyenClient>();
services.AddSingleton<RazorpayClient>();

services.AddSingleton<IPaymentProvider,AdyenEuropeProvider>();
services.AddSingleton<IPaymentProvider,RazorpayIndiaProvider>();
services.AddSingleton<IPaymentProvider,BackupEuropeProvider>();

services.AddSingleton<PaymentRouter>();

// Europe request -> Adyen fails -> Backup succeeds
// India request -> Razorpay succeeds
```

## Expected Output

``` text
EU Payment
Trying AdyenEU
Trying StripeBackup
Approved=True
Transaction=STRIPE-90001

India Payment
Trying RazorpayIN
Approved=True
Transaction=RZP-10001
```

## OCP

Adding PayPal, Stripe, Braintree, WorldPay or another PSP only requires
creating another `IPaymentProvider` implementation and registering it
with DI. The `PaymentRouter` never changes, making it **closed for
modification** and **open for extension**.
