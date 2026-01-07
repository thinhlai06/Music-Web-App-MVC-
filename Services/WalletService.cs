using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;

namespace MusicWeb.Services;

public class WalletService : IWalletService
{
    private readonly ApplicationDbContext _context;

    public WalletService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserWallet> GetOrCreateWalletAsync(string userId)
    {
        var wallet = await _context.UserWallets
            .FirstOrDefaultAsync(w => w.UserId == userId);

        if (wallet == null)
        {
            wallet = new UserWallet
            {
                UserId = userId,
                Balance = 100000, // 100,000 VND test balance (enough for Premium plans)
                TotalEarnings = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserWallets.Add(wallet);
            
            // Record initial balance transaction
            var transaction = new WalletTransaction
            {
                UserId = userId,
                Amount = 100000,
                Type = "Deposit",
                Description = "Số dư test ban đầu",
                BalanceAfter = 100000,
                CreatedAt = DateTime.UtcNow
            };
            _context.WalletTransactions.Add(transaction);
            
            await _context.SaveChangesAsync();
        }

        return wallet;
    }

    public async Task<decimal> GetBalanceAsync(string userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        return wallet.Balance;
    }

    public async Task<bool> DeductBalanceAsync(string userId, decimal amount, string description, string? referenceId = null)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        
        if (wallet.Balance < amount) return false;

        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new WalletTransaction
        {
            UserId = userId,
            Amount = -amount,
            Type = "Purchase",
            Description = description,
            ReferenceId = referenceId,
            BalanceAfter = wallet.Balance,
            CreatedAt = DateTime.UtcNow
        };
        _context.WalletTransactions.Add(transaction);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddEarningsAsync(string userId, decimal amount, string description, string? referenceId = null)
    {
        var wallet = await GetOrCreateWalletAsync(userId);

        wallet.Balance += amount;
        wallet.TotalEarnings += amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new WalletTransaction
        {
            UserId = userId,
            Amount = amount,
            Type = "Earning",
            Description = description,
            ReferenceId = referenceId,
            BalanceAfter = wallet.Balance,
            CreatedAt = DateTime.UtcNow
        };
        _context.WalletTransactions.Add(transaction);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<WalletTransaction>> GetTransactionsAsync(string userId, int limit = 20)
    {
        return await _context.WalletTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }
}
