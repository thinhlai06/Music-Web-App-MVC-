namespace MusicWeb.Services;

public interface IRevenueService
{
    Task RecordPremiumPlayAsync(string listenerUserId, int songId);
    Task<decimal> CalculateEarningPerPlayAsync(int planId);
    Task<List<Models.Entities.EarningsHistory>> GetUserEarningsAsync(string userId, int limit = 50);
    Task<decimal> GetTotalEarningsAsync(string userId);
}
