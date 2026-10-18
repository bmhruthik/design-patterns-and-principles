using DesignPatterns.Models;

namespace DesignPatterns.Interfaces;

public interface IAccountRepository
{
    BankAccount? GetById(int id);

    void Save(BankAccount account);
}