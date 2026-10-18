using DesignPatterns.Interfaces;
using DesignPatterns.Models;

namespace DesignPatterns.Services;

public class BankService
{
    private readonly IAccountRepository _repository;

    private readonly ILoggerService _logger;

    public BankService(
        IAccountRepository repository,
        ILoggerService logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void Deposit(int accountId, decimal amount)
    {
        BankAccount? account = _repository.GetById(accountId);

        if (account is null)
        {
            _logger.LogError(
                $"Account {accountId} was not found.");

            return;
        }

        try
        {
            account.Deposit(amount);

            _repository.Save(account);

            _logger.LogInformation(
                $"Deposited {amount} into {account.AccountHolder}'s account.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex.Message);
        }
    }

    public void Withdraw(int accountId, decimal amount)
    {
        BankAccount? account = _repository.GetById(accountId);

        if (account is null)
        {
            _logger.LogError(
                $"Account {accountId} was not found.");

            return;
        }

        try
        {
            account.Withdraw(amount);

            _repository.Save(account);

            _logger.LogInformation(
                $"Withdrew {amount} from {account.AccountHolder}'s account.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex.Message);
        }
    }

    public void DisplayBalance(int accountId)
    {
        BankAccount? account = _repository.GetById(accountId);

        if (account is null)
        {
            _logger.LogError(
                $"Account {accountId} was not found.");

            return;
        }

        _logger.LogInformation(
            $"Account holder: {account.AccountHolder}");

        _logger.LogInformation(
            $"Current balance: {account.Balance}");
    }
}