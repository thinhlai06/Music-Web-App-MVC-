using MusicWeb.Models.ViewModels;

namespace MusicWeb.Services;

public interface IListeningStatsService
{
    Task<ListeningStatsViewModel> GetUserStatsAsync(string userId, string period = "all");
}
