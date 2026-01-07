using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;

namespace MusicWeb.Services;

public class RevenueService : IRevenueService
{
    private readonly ApplicationDbContext _context;
    private readonly IWalletService _walletService;
    private readonly ISubscriptionService _subscriptionService;

    // Revenue sharing constants
    private const decimal UploaderSharePercent = 0.70m; // 70% for uploader
    private const decimal MinEarningPerPlay = 10m;      // Minimum 10 VND per play
    private const decimal MaxEarningPerPlay = 500m;     // Maximum 500 VND per play

    public RevenueService(
        ApplicationDbContext context, 
        IWalletService walletService,
        ISubscriptionService subscriptionService)
    {
        _context = context;
        _walletService = walletService;
        _subscriptionService = subscriptionService;
    }

    public async Task RecordPremiumPlayAsync(string listenerUserId, int songId)
    {
        Console.WriteLine($"[RevenueShare] RecordPremiumPlayAsync called: listener={listenerUserId}, songId={songId}");
        
        // Get song with Artist and check if it's premium
        var song = await _context.Songs
            .Include(s => s.Artist)
            .FirstOrDefaultAsync(s => s.Id == songId);
            
        if (song == null || !song.IsPremium)
        {
            Console.WriteLine($"[RevenueShare] Skipped: song null or not premium");
            return;
        }
        
        // Get uploader UserId from Artist
        var uploaderUserId = song.Artist?.UserId;
        Console.WriteLine($"[RevenueShare] Song '{song.Title}' uploader from Artist.UserId: {uploaderUserId}");
        
        if (string.IsNullOrEmpty(uploaderUserId))
        {
            Console.WriteLine($"[RevenueShare] Skipped: no uploader UserId");
            return;
        }

        // Check if listener is premium
        if (!await _subscriptionService.IsPremiumUserAsync(listenerUserId))
        {
            Console.WriteLine($"[RevenueShare] Skipped: listener is not premium");
            return;
        }

        // Don't pay if listener is the uploader
        if (uploaderUserId == listenerUserId)
        {
            Console.WriteLine($"[RevenueShare] Skipped: listener is uploader");
            return;
        }

        // Calculate earning
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(listenerUserId);
        if (subscription == null)
        {
            Console.WriteLine($"[RevenueShare] Skipped: no active subscription");
            return;
        }

        var earningAmount = await CalculateEarningPerPlayAsync(subscription.PlanId);
        Console.WriteLine($"[RevenueShare] Calculated earning: {earningAmount} VND");

        try
        {
            // Record earnings history
            var earnings = new EarningsHistory
            {
                UploaderUserId = uploaderUserId,
                ListenerUserId = listenerUserId,
                SongId = songId,
                Amount = earningAmount,
                CreatedAt = DateTime.UtcNow
            };
            _context.EarningsHistories.Add(earnings);
            await _context.SaveChangesAsync();
            Console.WriteLine($"[RevenueShare] EarningsHistory saved successfully");

            // Add earnings to uploader's wallet
            await _walletService.AddEarningsAsync(
                uploaderUserId,
                earningAmount,
                $"Doanh thu từ bài hát: {song.Title}",
                songId.ToString()
            );
            Console.WriteLine($"[RevenueShare] Wallet updated for uploader {uploaderUserId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RevenueShare] ERROR: {ex.Message}");
            Console.WriteLine($"[RevenueShare] Inner: {ex.InnerException?.Message}");
            throw; // Re-throw to see error in API response
        }
    }

    public async Task<decimal> CalculateEarningPerPlayAsync(int planId)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null || plan.Price == 0) return 0;

        // Formula: (Plan price / 30 days) * 70% / estimated plays per day
        // Simplified: assume ~20 plays per day per user
        var dailyValue = plan.Price / 30m;
        var uploaderShare = dailyValue * UploaderSharePercent;
        var earningPerPlay = uploaderShare / 20m;

        // Clamp to min/max
        return Math.Clamp(earningPerPlay, MinEarningPerPlay, MaxEarningPerPlay);
    }

    public async Task<List<EarningsHistory>> GetUserEarningsAsync(string userId, int limit = 50)
    {
        return await _context.EarningsHistories
            .Include(e => e.Song)
            .Include(e => e.Listener)
            .Where(e => e.UploaderUserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalEarningsAsync(string userId)
    {
        return await _context.EarningsHistories
            .Where(e => e.UploaderUserId == userId)
            .SumAsync(e => e.Amount);
    }
}
