using DesignPatterns.Interfaces;
using DesignPatterns.Models;

namespace DesignPatterns.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly Dictionary<int, BankAccount> _accounts = new();

    public AccountRepository()
    {
        // Sample data for this demonstration.
        _accounts[1] = new BankAccount(
            1,
            "Hruthik",
            1000m);

        _accounts[2] = new BankAccount(
            2,
            "Rahul",
            2000m);
    }

    public BankAccount? GetById(int id)
    {
        _accounts.TryGetValue(id, out BankAccount? account);

        return account;
    }

    public void Save(BankAccount account)
    {
        _accounts[account.Id] = account;
    }
}