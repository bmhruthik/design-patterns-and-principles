using DesignPatterns.Interfaces;
using DesignPatterns.Logging;
using DesignPatterns.Repositories;
using DesignPatterns.Services;
using Microsoft.Extensions.DependencyInjection;
using DesignPatterns.Models;



// 1. Create the dependency injection container.
var services = new ServiceCollection();


// 2. Register interface-to-implementation mappings.
// services.AddSingleton<IAccountRepository, AccountRepository>();

services.AddSingleton<IAccountRepository>(
    _ => new JsonAccountRepository("accounts.json")
);

services.AddSingleton<ILoggerService>(
    _ => new FileLogger("application.log")
);

services.AddTransient<BankService>();

// 3. Build the service provider.
using ServiceProvider serviceProvider = services.BuildServiceProvider();

var repository = serviceProvider.GetRequiredService<IAccountRepository>();

repository.Save(new BankAccount(1, "Hruthik", 1000m));
repository.Save(new BankAccount(2, "Rahul", 2000m));

// 4. Ask the container for the banking service.
BankService bankService =
    serviceProvider.GetRequiredService<BankService>();

// 5. Use the service.
bankService.DisplayBalance(1);

Console.WriteLine();

bankService.Deposit(1, 500m);

bankService.DisplayBalance(1);

Console.WriteLine();

bankService.Withdraw(1, 200m);

bankService.DisplayBalance(1);

Console.WriteLine();

bankService.Withdraw(1, 5000m);

Console.WriteLine();

bankService.Deposit(99, 100m);