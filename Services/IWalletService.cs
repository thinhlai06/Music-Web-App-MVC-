namespace MusicWeb.Services;

public interface IWalletService
{
    Task<Models.Entities.UserWallet> GetOrCreateWalletAsync(string userId);
    Task<decimal> GetBalanceAsync(string userId);
    Task<bool> DeductBalanceAsync(string userId, decimal amount, string description, string? referenceId = null);
    Task<bool> AddEarningsAsync(string userId, decimal amount, string description, string? referenceId = null);
    Task<List<Models.Entities.WalletTransaction>> GetTransactionsAsync(string userId, int limit = 20);
}
