using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.ViewModels;

namespace MusicWeb.Services;

public class ListeningStatsService : IListeningStatsService
{
    private readonly ApplicationDbContext _context;

    public ListeningStatsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ListeningStatsViewModel> GetUserStatsAsync(string userId, string period = "all")
    {
        var result = new ListeningStatsViewModel { Period = period };

        // Determine date range based on period
        DateTime? startDate = period switch
        {
            "7d" => DateTime.UtcNow.AddDays(-7),
            "30d" => DateTime.UtcNow.AddDays(-30),
            _ => null
        };

        result.StartDate = startDate;
        result.EndDate = DateTime.UtcNow;

        // Base query with period filter
        var historyQuery = _context.PlayHistories
            .AsNoTracking()
            .Where(h => h.UserId == userId);

        if (startDate.HasValue)
        {
            historyQuery = historyQuery.Where(h => h.PlayedAt >= startDate.Value);
        }

        // Get all play history with song details
        var playHistory = await historyQuery
            .Include(h => h.Song)
                .ThenInclude(s => s.Artist)
            .Include(h => h.Song)
                .ThenInclude(s => s.SongGenres)
                    .ThenInclude(sg => sg.Genre)
            .ToListAsync();

        if (!playHistory.Any())
        {
            return result;
        }

        // Summary stats
        result.TotalPlays = playHistory.Count;
        result.TotalListeningTime = TimeSpan.FromTicks(
            playHistory.Sum(h => h.Song.Duration.Ticks));
        result.UniqueArtists = playHistory.Select(h => h.Song.ArtistId).Distinct().Count();
        result.UniqueSongs = playHistory.Select(h => h.SongId).Distinct().Count();
        result.UniqueGenres = playHistory
            .SelectMany(h => h.Song.SongGenres.Select(sg => sg.GenreId))
            .Distinct()
            .Count();

        // Top Artists (Top 5)
        var artistGroups = playHistory
            .GroupBy(h => new { h.Song.ArtistId, h.Song.Artist.Name })
            .Select(g => new
            {
                ArtistId = g.Key.ArtistId,
                Name = g.Key.Name,
                PlayCount = g.Count()
            })
            .OrderByDescending(a => a.PlayCount)
            .Take(5)
            .ToList();

        var maxArtistPlays = artistGroups.FirstOrDefault()?.PlayCount ?? 1;
        result.TopArtists = artistGroups.Select(a => new TopArtistStat(
            a.ArtistId,
            a.Name,
            null, // ImageUrl - not stored in Artist entity
            a.PlayCount,
            Math.Round((double)a.PlayCount / result.TotalPlays * 100, 1)
        )).ToList();

        // Top Genres (Top 5)
        var genreGroups = playHistory
            .SelectMany(h => h.Song.SongGenres.Select(sg => new { sg.GenreId, sg.Genre.Name, PlayedAt = h.PlayedAt }))
            .GroupBy(g => new { g.GenreId, g.Name })
            .Select(g => new
            {
                GenreId = g.Key.GenreId,
                Name = g.Key.Name,
                PlayCount = g.Count()
            })
            .OrderByDescending(g => g.PlayCount)
            .Take(5)
            .ToList();

        var totalGenrePlays = genreGroups.Sum(g => g.PlayCount);
        result.TopGenres = genreGroups.Select(g => new TopGenreStat(
            g.GenreId,
            g.Name,
            g.PlayCount,
            totalGenrePlays > 0 ? Math.Round((double)g.PlayCount / totalGenrePlays * 100, 1) : 0
        )).ToList();

        // Top Songs (Top 10)
        var songGroups = playHistory
            .GroupBy(h => new { h.SongId, h.Song.Title, ArtistName = h.Song.Artist.Name, h.Song.CoverUrl, h.Song.Duration })
            .Select(g => new
            {
                SongId = g.Key.SongId,
                Title = g.Key.Title,
                Artist = g.Key.ArtistName,
                CoverUrl = g.Key.CoverUrl,
                Duration = g.Key.Duration,
                PlayCount = g.Count()
            })
            .OrderByDescending(s => s.PlayCount)
            .Take(10)
            .ToList();

        result.TopSongs = songGroups.Select(s => new TopSongStat(
            s.SongId,
            s.Title,
            s.Artist,
            s.CoverUrl,
            s.PlayCount,
            TimeSpan.FromTicks(s.Duration.Ticks * s.PlayCount)
        )).ToList();

        // Plays by Day of Week (convert UTC to local time)
        var dayNames = new[] { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };
        var playsByDay = playHistory
            .GroupBy(h => (int)h.PlayedAt.ToLocalTime().DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Count());

        for (int i = 0; i < 7; i++)
        {
            result.PlaysByDayOfWeek[dayNames[i]] = playsByDay.GetValueOrDefault(i, 0);
        }

        // Plays by Hour (0-23) - convert UTC to local time
        var playsByHour = playHistory
            .GroupBy(h => h.PlayedAt.ToLocalTime().Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        for (int i = 0; i < 24; i++)
        {
            result.PlaysByHour[i] = playsByHour.GetValueOrDefault(i, 0);
        }

        return result;
    }
}
