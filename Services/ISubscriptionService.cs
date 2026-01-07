namespace MusicWeb.Services;

public interface ISubscriptionService
{
    Task<bool> IsPremiumUserAsync(string userId);
    Task<Models.Entities.UserSubscription?> GetActiveSubscriptionAsync(string userId);
    Task<List<Models.Entities.SubscriptionPlan>> GetActivePlansAsync();
    Task<bool> CanAccessPremiumSongAsync(string userId, int songId);
    Task<bool> CanDownloadAsync(string userId);
    Task<int> GetRemainingDownloadsAsync(string userId);
    Task<bool> PurchaseSubscriptionAsync(string userId, int planId);
}
