namespace DesignPatterns.Models;

public class BankAccount
{
    public int Id { get; }

    public string AccountHolder { get; }

    public decimal Balance { get; private set; }

    public BankAccount(int id, string accountHolder, decimal balance)
    {
        Id = id;
        AccountHolder = accountHolder;
        Balance = balance;
    }

    public void Deposit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException(
                "Deposit amount must be positive.");
        }

        Balance += amount;
    }

    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException(
                "Withdrawal amount must be positive.");
        }

        if (amount > Balance)
        {
            throw new InvalidOperationException(
                "Insufficient balance.");
        }

        Balance -= amount;
    }
}