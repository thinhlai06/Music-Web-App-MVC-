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
        // Get song and check if it's premium
        var song = await _context.Songs.FindAsync(songId);
        if (song == null || !song.IsPremium || string.IsNullOrEmpty(song.UploadedByUserId))
            return;

        // Check if listener is premium
        if (!await _subscriptionService.IsPremiumUserAsync(listenerUserId))
            return;

        // Don't pay if listener is the uploader
        if (song.UploadedByUserId == listenerUserId)
            return;

        // Calculate earning
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(listenerUserId);
        if (subscription == null) return;

        var earningAmount = await CalculateEarningPerPlayAsync(subscription.PlanId);

        // Record earnings history
        var earnings = new EarningsHistory
        {
            UploaderUserId = song.UploadedByUserId,
            ListenerUserId = listenerUserId,
            SongId = songId,
            Amount = earningAmount,
            CreatedAt = DateTime.UtcNow
        };
        _context.EarningsHistories.Add(earnings);
        await _context.SaveChangesAsync();

        // Add earnings to uploader's wallet
        await _walletService.AddEarningsAsync(
            song.UploadedByUserId,
            earningAmount,
            $"Doanh thu từ bài hát: {song.Title}",
            songId.ToString()
        );
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
