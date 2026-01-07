using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MusicWeb.Models.Entities;
using MusicWeb.Services;

namespace MusicWeb.Controllers;

[Route("api/premium")]
[ApiController]
public class PremiumController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IWalletService _walletService;
    private readonly IRevenueService _revenueService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PremiumController(
        ISubscriptionService subscriptionService,
        IWalletService walletService,
        IRevenueService revenueService,
        UserManager<ApplicationUser> userManager)
    {
        _subscriptionService = subscriptionService;
        _walletService = walletService;
        _revenueService = revenueService;
        _userManager = userManager;
    }

    /// <summary>
    /// Get current user's premium status (for JS to check if user is premium)
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetPremiumStatus()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Ok(new { isPremium = false, hasSubscription = false });
        }

        var isPremium = await _subscriptionService.IsPremiumUserAsync(userId);
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId);
        var wallet = await _walletService.GetOrCreateWalletAsync(userId);

        return Ok(new
        {
            isPremium,
            hasSubscription = subscription != null,
            planName = subscription?.Plan?.Name,
            planId = subscription?.PlanId,
            endDate = subscription?.EndDate,
            balance = wallet.Balance,
            totalEarnings = wallet.TotalEarnings,
            remainingDownloads = await _subscriptionService.GetRemainingDownloadsAsync(userId),
            noAds = subscription?.Plan?.NoAds ?? false
        });
    }

    /// <summary>
    /// Get all available subscription plans
    /// </summary>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _subscriptionService.GetActivePlansAsync();
        return Ok(plans.Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.DurationDays,
            p.DownloadLimit,
            p.NoAds,
            p.HighQualityAudio,
            p.CanAccessPremiumSongs
        }));
    }

    /// <summary>
    /// Purchase a subscription plan using wallet balance
    /// </summary>
    [HttpPost("purchase/{planId}")]
    [Authorize]
    public async Task<IActionResult> PurchaseSubscription(int planId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _subscriptionService.PurchaseSubscriptionAsync(userId, planId);
        if (!success)
            return BadRequest(new { success = false, message = "Không đủ số dư hoặc gói không khả dụng" });

        return Ok(new { success = true, message = "Mua gói thành công!" });
    }

    /// <summary>
    /// Get wallet info and transaction history
    /// </summary>
    [HttpGet("wallet")]
    [Authorize]
    public async Task<IActionResult> GetWallet()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var wallet = await _walletService.GetOrCreateWalletAsync(userId);
        var transactions = await _walletService.GetTransactionsAsync(userId, 20);

        return Ok(new
        {
            balance = wallet.Balance,
            totalEarnings = wallet.TotalEarnings,
            transactions = transactions.Select(t => new
            {
                t.Id,
                t.Amount,
                t.Type,
                t.Description,
                t.BalanceAfter,
                t.CreatedAt
            })
        });
    }

    /// <summary>
    /// Get earnings history for song uploaders
    /// </summary>
    [HttpGet("earnings")]
    [Authorize]
    public async Task<IActionResult> GetEarnings()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var earnings = await _revenueService.GetUserEarningsAsync(userId, 50);
        var totalEarnings = await _revenueService.GetTotalEarningsAsync(userId);

        return Ok(new
        {
            totalEarnings,
            history = earnings.Select(e => new
            {
                e.Id,
                e.Amount,
                songTitle = e.Song?.Title,
                listenerName = e.Listener?.DisplayName,
                e.CreatedAt
            })
        });
    }

    /// <summary>
    /// Record a premium song play (called from JS when premium user plays premium song)
    /// </summary>
    [HttpPost("record-play/{songId}")]
    [Authorize]
    public async Task<IActionResult> RecordPremiumPlay(int songId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _revenueService.RecordPremiumPlayAsync(userId, songId);
        return Ok(new { success = true });
    }
}
