using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;

namespace MusicWeb.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;

    public SubscriptionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsPremiumUserAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        
        var subscription = await GetActiveSubscriptionAsync(userId);
        return subscription != null && subscription.Plan.CanAccessPremiumSongs;
    }

    public async Task<UserSubscription?> GetActiveSubscriptionAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        return await _context.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId 
                && s.Status == "Active"
                && (s.EndDate == null || s.EndDate > DateTime.UtcNow))
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();
    }

    public async Task<List<SubscriptionPlan>> GetActivePlansAsync()
    {
        return await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();
    }

    public async Task<bool> CanAccessPremiumSongAsync(string userId, int songId)
    {
        // Check if user is premium
        if (!await IsPremiumUserAsync(userId)) return false;

        // Check if song is premium
        var song = await _context.Songs.FindAsync(songId);
        if (song == null || !song.IsPremium) return true; // Non-premium songs are accessible

        return true; // Premium user can access premium songs
    }

    public async Task<bool> CanDownloadAsync(string userId)
    {
        var subscription = await GetActiveSubscriptionAsync(userId);
        if (subscription == null) return false;

        // Check download limit
        if (subscription.Plan.DownloadLimit == -1) return true; // Unlimited
        if (subscription.Plan.DownloadLimit == 0) return false; // No downloads

        var remaining = await GetRemainingDownloadsAsync(userId);
        return remaining > 0;
    }

    public async Task<int> GetRemainingDownloadsAsync(string userId)
    {
        var subscription = await GetActiveSubscriptionAsync(userId);
        if (subscription == null) return 0;

        if (subscription.Plan.DownloadLimit == -1) return int.MaxValue; // Unlimited
        if (subscription.Plan.DownloadLimit == 0) return 0;

        // Count downloads this month
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var downloadsThisMonth = await _context.WalletTransactions
            .Where(t => t.UserId == userId 
                && t.Type == "Download" 
                && t.CreatedAt >= startOfMonth)
            .CountAsync();

        return Math.Max(0, subscription.Plan.DownloadLimit - downloadsThisMonth);
    }

    public async Task<bool> PurchaseSubscriptionAsync(string userId, int planId)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null || !plan.IsActive) return false;

        // Check wallet balance
        var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null || wallet.Balance < plan.Price) return false;

        // Deduct balance
        wallet.Balance -= plan.Price;
        wallet.UpdatedAt = DateTime.UtcNow;

        // Record transaction
        var transaction = new WalletTransaction
        {
            UserId = userId,
            Amount = -plan.Price,
            Type = "Purchase",
            Description = $"Mua gói {plan.Name}",
            ReferenceId = planId.ToString(),
            BalanceAfter = wallet.Balance,
            CreatedAt = DateTime.UtcNow
        };
        _context.WalletTransactions.Add(transaction);

        // Cancel existing subscription if any
        var existingSub = await GetActiveSubscriptionAsync(userId);
        if (existingSub != null)
        {
            existingSub.Status = "Cancelled";
        }

        // Create new subscription
        var endDate = plan.DurationDays > 0 
            ? DateTime.UtcNow.AddDays(plan.DurationDays) 
            : (DateTime?)null;

        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanId = planId,
            StartDate = DateTime.UtcNow,
            EndDate = endDate,
            Status = "Active",
            PaymentMethod = "Wallet",
            TransactionId = Guid.NewGuid().ToString()
        };
        _context.UserSubscriptions.Add(subscription);

        await _context.SaveChangesAsync();
        return true;
    }
}
