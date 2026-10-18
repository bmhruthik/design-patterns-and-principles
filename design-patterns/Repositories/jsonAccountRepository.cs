using System.Text.Json;
using DesignPatterns.Interfaces;
using DesignPatterns.Models;

namespace DesignPatterns.Repositories;

public class JsonAccountRepository : IAccountRepository
{
    private readonly string _filePath;
    private readonly List<BankAccount> _accounts;

    public JsonAccountRepository(string filePath)
    {
        _filePath = filePath;

        if (File.Exists(_filePath))
        {
            string json = File.ReadAllText(_filePath);

            if (!string.IsNullOrWhiteSpace(json))
            {
                _accounts =
                    JsonSerializer.Deserialize<List<BankAccount>>(json)
                    ?? new List<BankAccount>();
            }
            else
            {
                _accounts = new List<BankAccount>();
            }
        }
        else
        {
            _accounts = new List<BankAccount>();
        }
    }

    public BankAccount? GetById(int id)
    {
        return _accounts.FirstOrDefault(a => a.Id == id);
    }

    public void Save(BankAccount account)
    {
        int index = _accounts.FindIndex(a => a.Id == account.Id);

        if (index >= 0)
        {
            _accounts[index] = account;
        }
        else
        {
            _accounts.Add(account);
        }

        string json = JsonSerializer.Serialize(
            _accounts,
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(_filePath, json);
    }

    public IEnumerable<BankAccount> GetAll()
    {
        return _accounts;
    }
}