using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;
using System.Net.Http;

namespace MusicWeb.Services;

public class MusicService : IMusicService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public MusicService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HomeViewModel> BuildHomeAsync(string? userId)
    {
        var favoriteSongIds = new HashSet<int>();
        var userRatings = new Dictionary<int, decimal>();
        if (userId is not null)
        {
            var favIds = await _context.FavoriteSongs
                .Where(f => f.UserId == userId)
                .Select(f => f.SongId)
                .ToListAsync();
            favoriteSongIds = favIds.ToHashSet();
            
            var ratings = await _context.UserSongRatings
                .Where(r => r.UserId == userId)
                .ToDictionaryAsync(r => r.SongId, r => r.Rating);
            userRatings = ratings;
        }

        // NEW RELEASES: Latest 8 songs (For the carousel/scroll section)
        var newReleasesRaw = await _context.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .Where(s => s.IsPublic)
            .OrderByDescending(s => s.ReleaseDate)
            .Take(8)
            .ToListAsync();

        // RECOMMENDATIONS: Random 3 songs from ALL available songs
        // Guid.NewGuid() ensures a random sort order every time this is called,
        // so it will always pick 3 random songs from the current total pool of songs (6, 10, or 100+).
        var randomRecommendations = await _context.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .Where(s => s.IsPublic)
            .OrderBy(s => Guid.NewGuid()) 
            .Take(3)
            .ToListAsync();

        // DYNAMIC ZINGCHART
        // Top 7 songs by ViewCount
        var chartSongs = await _context.Songs
            .Include(s => s.Artist)
            .Where(s => s.IsPublic)
            .OrderByDescending(s => s.ViewCount)
            .Take(7)
            .ToListAsync();

        var playlists = await _context.Playlists
            .Include(p => p.Owner)
            .Where(p => p.IsPublic)
            .Take(6)
            .ToListAsync();

        var genres = await _context.Genres.ToListAsync();

        var albums = await _context.Albums
            .Include(a => a.Artist)
            .OrderByDescending(a => a.ReleaseDate)
            .Take(8)
            .ToListAsync();

        UserProfileViewModel? profile = null;
        List<Playlist> personalPlaylists = new();
        List<Song> favoriteSongs = new();
        List<PlayHistory> recentlyPlayed = new();
        List<Song> uploadedSongs = new();

        if (userId is not null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is not null)
            {
                profile = new UserProfileViewModel(
                    user.DisplayName ?? user.UserName ?? "User",
                    user.Email ?? string.Empty,
                    user.AvatarUrl,
                    user.Id);
            }

            personalPlaylists = await _context.Playlists
                .Include(p => p.Songs)
                .Where(p => p.OwnerId == userId)
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            favoriteSongs = await _context.FavoriteSongs
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Take(12)
                .Include(f => f.Song)
                .ThenInclude(s => s.Artist)
                .Select(f => f.Song)
                .ToListAsync();

            recentlyPlayed = await _context.PlayHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.PlayedAt)
                .Include(h => h.Song)
                .ThenInclude(s => s.Artist)
                .Take(12)
                .ToListAsync();

            // Fetch songs uploaded by this user (via Artist relationship)
            uploadedSongs = await _context.Songs
                .Include(s => s.Artist)
                .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
                .Where(s => s.Artist.UserId == userId)
                .OrderByDescending(s => s.ReleaseDate)
                .ToListAsync();

            // Stats Logic
            int followersCount = 0;
            int followingCount = 0;
            int publicSongCount = 0;

            if (profile != null) 
            {
                 followersCount = await _context.UserFollows.CountAsync(f => f.FolloweeId == profile.UserId);
                 followingCount = await _context.UserFollows.CountAsync(f => f.FollowerId == profile.UserId);
                 publicSongCount = await _context.Songs.CountAsync(s => s.Artist.UserId == profile.UserId && s.IsPublic);
                 
                 profile = profile with { 
                    FollowersCount = followersCount, 
                    FollowingCount = followingCount, 
                    PublicSongCount = publicSongCount 
                 };
            }
        }

        return new HomeViewModel
        {
            CurrentUser = profile,
            Recommendations = randomRecommendations
                .Select(song => ToSongCard(song, favoriteSongIds, userRatings))
                .ToList(),
            NewReleases = newReleasesRaw
                .Select(song => ToSongCard(song, favoriteSongIds, userRatings))
                .ToList(),
            Chart = chartSongs.Select((s, index) => {
                 var totalTop10Views = chartSongs.Sum(x => x.ViewCount);
                 var percent = totalTop10Views > 0 ? (double)s.ViewCount / totalTop10Views : 0;
                 return new ChartItemViewModel(
                    s.Id,
                    s.Title,
                    s.Artist.Name,
                    s.CoverUrl ?? string.Empty,
                    index + 1,
                    percent,
                    s.ViewCount
                 );
            }).ToList(),
            TrendingPlaylists = playlists.Select(p => new PlaylistCardViewModel(
                p.Id,
                p.Name,
                $"Tạo bởi {p.Owner.DisplayName ?? p.Owner.Email}",
                p.CoverUrl ?? "https://picsum.photos/id/129/300/300")).ToList(),
            Albums = albums.Select(a => new AlbumCardViewModel(
                a.Id,
                a.Title,
                a.Artist.Name,
                a.CoverUrl ?? "https://picsum.photos/seed/album-" + a.Id + "/300/300")).ToList(),
            Genres = genres.Select(g => new GenreTileViewModel(g.Id, g.Name, g.TileImageUrl ?? string.Empty)).ToList(),
            PersonalPlaylists = personalPlaylists.Select(p => new PlaylistCardViewModel(
                p.Id,
                p.Name,
                $"{p.Songs.Count} bài hát",
                p.CoverUrl ?? "https://picsum.photos/id/180/300/300")).ToList(),
            FavoriteSongs = favoriteSongs.Select(song => ToSongCard(song, favoriteSongIds, userRatings)).ToList(),
            RecentlyPlayed = recentlyPlayed.Select(h => ToSongCard(h.Song, favoriteSongIds, userRatings)).ToList(),
            UploadedSongs = uploadedSongs.Select(song => ToSongCard(song, favoriteSongIds, userRatings)).ToList()
        };
    }

    public async Task<List<SongCardViewModel>> GetSongsByGenreAsync(int genreId, string userId)
    {
        // ... (keep legacy if needed, or redirect)
        var detail = await GetGenreDetailAsync(genreId, userId);
        return detail?.Songs ?? new List<SongCardViewModel>();
    }

    public async Task<GenreDetailViewModel?> GetGenreDetailAsync(int id, string? userId)
    {
        var genre = await _context.Genres.FindAsync(id);
        if (genre == null) return null;

        var songs = await _context.SongGenres
            .Where(sg => sg.GenreId == id)
            .Include(sg => sg.Song)
                .ThenInclude(s => s.Artist)
            .Select(sg => sg.Song)
            .Where(s => s.IsPublic)
            .ToListAsync();

        var favorites = new HashSet<int>();
        var userRatings = new Dictionary<int, decimal>();
        if (userId != null)
        {
            favorites = (await _context.FavoriteSongs.Where(f => f.UserId == userId).Select(f => f.SongId).ToListAsync()).ToHashSet();
            userRatings = await _context.UserSongRatings.Where(r => r.UserId == userId).ToDictionaryAsync(r => r.SongId, r => r.Rating);
        }

        return new GenreDetailViewModel
        {
            Id = genre.Id,
            Title = genre.Name,
            ImageUrl = genre.TileImageUrl ?? $"https://picsum.photos/seed/genre-{genre.Id}/400/200",
            Songs = songs.Select(s => ToSongCard(s, favorites, userRatings)).ToList()
        };
    }

    public async Task<SearchResultsViewModel> SearchAsync(string term, string? userId)
    {
        term = term.Trim();
        var favorites = new HashSet<int>();
        var userRatings = new Dictionary<int, decimal>();
        if (userId is not null)
        {
            favorites = (await _context.FavoriteSongs
                .Where(f => f.UserId == userId)
                .Select(f => f.SongId)
                .ToListAsync()).ToHashSet();
            userRatings = await _context.UserSongRatings
                .Where(r => r.UserId == userId)
                .ToDictionaryAsync(r => r.SongId, r => r.Rating);
        }

        var songsQuery = _context.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .Where(s => s.Title.Contains(term) && s.IsPublic);

        var artistsQuery = _context.Artists
            .Where(a => a.Name.Contains(term));

        var playlistsQuery = _context.Playlists
            .Where(p => p.Name.Contains(term) && p.IsPublic);

        var albumsQuery = _context.Albums
            .Include(a => a.Artist)
            .Where(a => a.Title.Contains(term));

        var usersQuery = _context.Users
            .Where(u => u.Id != userId && (u.DisplayName != null && u.DisplayName.Contains(term) || 
                        u.Email != null && u.Email.Contains(term)));

        var songResults = await songsQuery.Take(10).ToListAsync();

        var model = new SearchResultsViewModel
        {
            Songs = songResults.Select(song => ToSongCard(song, favorites, userRatings)).ToList(),
            Artists = await artistsQuery.Take(10).Select(a =>
                new ArtistResultViewModel(a.Id, a.Name, a.AvatarUrl, a.Bio)).ToListAsync(),
            Playlists = await playlistsQuery.Take(5).Select(p =>
                new PlaylistCardViewModel(p.Id, p.Name, p.Description ?? string.Empty, p.CoverUrl ?? string.Empty))
                .ToListAsync(),
            Albums = await albumsQuery.Take(5).Select(a =>
                new AlbumResultViewModel(a.Id, a.Title, a.Artist.Name, a.CoverUrl))
                .ToListAsync(),
            Users = await usersQuery.Take(10).Select(u =>
                new UserResultViewModel(u.Id, u.DisplayName ?? u.UserName ?? "Unknown", u.Email, u.AvatarUrl, 
                    userId != null && _context.UserFollows.Any(f => f.FollowerId == userId && f.FolloweeId == u.Id)))
                .ToListAsync()
        };

        return model;
    }

    public async Task<(IEnumerable<string> Lyrics, string SongTitle, string Artist)> GetLyricsAsync(int songId)
    {
        var song = await _context.Songs
            .Include(s => s.Artist)
            .Include(s => s.Lyrics)
            .FirstOrDefaultAsync(s => s.Id == songId);

        if (song is null)
        {
            return (Array.Empty<string>(), "Không tìm thấy", string.Empty);
        }

        IEnumerable<string> lines = Array.Empty<string>();

        if (!string.IsNullOrWhiteSpace(song.LyricsUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var content = await client.GetStringAsync(song.LyricsUrl);
                lines = content
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch
            {
                // fallback bên dưới nếu đọc URL thất bại
            }
        }

        if (!lines.Any())
        {
            lines = song.Lyrics
                .OrderBy(l => l.TimestampSeconds)
                .Select(l => l.Content);
        }

        return (lines, song.Title, song.Artist.Name);
    }

    public async Task<bool> ToggleFavoriteAsync(int songId, string userId)
    {
        var favorite = await _context.FavoriteSongs
            .FirstOrDefaultAsync(f => f.SongId == songId && f.UserId == userId);

        if (favorite is null)
        {
            _context.FavoriteSongs.Add(new FavoriteSong { SongId = songId, UserId = userId });
            await _context.SaveChangesAsync();
            return true;
        }

        _context.FavoriteSongs.Remove(favorite);
        await _context.SaveChangesAsync();
        return false;
    }

    public async Task<Playlist> CreatePlaylistAsync(string name, string userId)
    {
        var playlist = new Playlist
        {
            Name = name,
            OwnerId = userId,
            CoverUrl = "https://picsum.photos/seed/" + Guid.NewGuid() + "/300/300"
        };

        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();
        return playlist;
    }

    public async Task<PlaylistDetailViewModel?> GetPlaylistDetailAsync(int id, string? userId)
    {
        var playlist = await _context.Playlists
            .Include(p => p.Owner)
            .Include(p => p.Songs)
                .ThenInclude(ps => ps.Song)
                    .ThenInclude(s => s.Artist)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (playlist == null) return null;
        if (!playlist.IsPublic && playlist.OwnerId != userId) return null;

        var favoriteIds = new HashSet<int>();
        var userRatings = new Dictionary<int, decimal>();
        if (userId != null)
        {
            favoriteIds = (await _context.FavoriteSongs
                .Where(f => f.UserId == userId)
                .Select(f => f.SongId)
                .ToListAsync()).ToHashSet();
            userRatings = await _context.UserSongRatings
                .Where(r => r.UserId == userId)
                .ToDictionaryAsync(r => r.SongId, r => r.Rating);
        }

        return new PlaylistDetailViewModel
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Description = playlist.Description,
            CoverUrl = playlist.CoverUrl ?? $"https://picsum.photos/seed/playlist-{playlist.Id}/300/300",
            IsPublic = playlist.IsPublic,
            OwnerName = playlist.Owner.DisplayName ?? playlist.Owner.UserName ?? "Unknown",
            CanEdit = userId == playlist.OwnerId,
            Songs = playlist.Songs.OrderBy(ps => ps.Order)
                .Select(ps => ToSongCard(ps.Song, favoriteIds, userRatings))
                .ToList()
        };
    }

    public async Task<bool> UpdatePlaylistAsync(int id, string name, string? coverUrl, string userId)
    {
        var playlist = await _context.Playlists.FindAsync(id);
        if (playlist == null || playlist.OwnerId != userId) return false;

        playlist.Name = name;
        if (coverUrl != null) playlist.CoverUrl = coverUrl;
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddSongToPlaylistAsync(int playlistId, int songId, string userId)
    {
        var playlist = await _context.Playlists.Include(p => p.Songs)
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == userId);

        if (playlist is null) return false;
        if (playlist.Songs.Any(ps => ps.SongId == songId)) return false;

        var order = playlist.Songs.Count == 0 ? 1 : playlist.Songs.Max(ps => ps.Order) + 1;
        playlist.Songs.Add(new PlaylistSong { SongId = songId, Order = order });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveSongFromPlaylistAsync(int playlistId, int songId, string userId)
    {
        var playlist = await _context.Playlists
            .Include(p => p.Songs)
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == userId);

        if (playlist is null) return false;

        var song = playlist.Songs.FirstOrDefault(ps => ps.SongId == songId);
        if (song is null) return false;

        playlist.Songs.Remove(song);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePlaylistAsync(int playlistId, string userId)
    {
        var playlist = await _context.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == userId);

        if (playlist is null) return false;

        _context.Playlists.Remove(playlist);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task RecordPlayAsync(int songId, string? userId)
    {
        // 1. Increment View Count (Always)
        var song = await _context.Songs.FindAsync(songId);
        if (song != null)
        {
            song.ViewCount++;
        }
        else
        {
            return; // Song doesn't exist
        }

        // 2. Record History (Only if logged in)
        if (!string.IsNullOrEmpty(userId))
        {
            var existingHistory = await _context.PlayHistories
                .FirstOrDefaultAsync(h => h.SongId == songId && h.UserId == userId);

            if (existingHistory is not null)
            {
                _context.PlayHistories.Remove(existingHistory);
            }

            _context.PlayHistories.Add(new PlayHistory
            {
                SongId = songId,
                UserId = userId,
                PlayedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<AlbumDetailViewModel?> GetAlbumDetailAsync(int id, string? userId)
    {
        var album = await _context.Albums
            .Include(a => a.Artist)
            .Include(a => a.Songs)
            .ThenInclude(s => s.Artist)
            .Include(a => a.Songs)
            .ThenInclude(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (album == null) return null;

        var favoriteIds = new HashSet<int>();
        var userRatings = new Dictionary<int, decimal>();
        if (userId != null)
        {
            favoriteIds = (await _context.FavoriteSongs
                .Where(f => f.UserId == userId)
                .Select(f => f.SongId)
                .ToListAsync()).ToHashSet();
            userRatings = await _context.UserSongRatings
                .Where(r => r.UserId == userId)
                .ToDictionaryAsync(r => r.SongId, r => r.Rating);
        }

        return new AlbumDetailViewModel
        {
            Id = album.Id,
            Title = album.Title,
            ArtistName = album.Artist.Name,
            ArtistAvatar = album.Artist.AvatarUrl, // Map Avatar
            CoverUrl = album.CoverUrl ?? "https://picsum.photos/300/300",
            ReleaseYear = album.ReleaseDate.Year,
            ReleaseDate = album.ReleaseDate,
            Songs = album.Songs.Select(s => ToSongCard(s, favoriteIds, userRatings)).ToList()
        };
    }

    public async Task<bool> SetUserSongRatingAsync(int songId, decimal rating, string userId)
    {
        // Validate rating range
        if (rating < 0 || rating > 5)
        {
            return false;
        }

        var existingRating = await _context.UserSongRatings
            .FirstOrDefaultAsync(r => r.UserId == userId && r.SongId == songId);

        if (existingRating != null)
        {
            // Update existing rating
            existingRating.Rating = rating;
            existingRating.RatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new rating
            _context.UserSongRatings.Add(new UserSongRating
            {
                UserId = userId,
                SongId = songId,
                Rating = rating,
                RatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    private static SongCardViewModel ToSongCard(Song song, HashSet<int> favoriteIds, Dictionary<int, decimal> userRatings) =>
        new(
            song.Id,
            song.Title,
            song.Artist.Name,
            song.CoverUrl ?? string.Empty,
            $"{(int)song.Duration.TotalMinutes}:{song.Duration.Seconds:00}",
            favoriteIds.Contains(song.Id),
            song.AudioUrl ?? string.Empty,
            userRatings.TryGetValue(song.Id, out var rating) ? rating : null,
            song.SongGenres.FirstOrDefault()?.Genre.Name ?? "Unknown",
            song.ReleaseDate,
            song.ViewCount,
            song.UserRatings.Any() ? (decimal?)song.UserRatings.Average(r => (double)r.Rating) : null,
            song.IsPublic);

    public async Task<bool> ToggleSongVisibilityAsync(int songId, string userId)
    {
        var song = await _context.Songs
            .Include(s => s.Artist)
            .FirstOrDefaultAsync(s => s.Id == songId);

        if (song == null || song.Artist.UserId != userId) return false;

        song.IsPublic = !song.IsPublic;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> FollowUserAsync(string followerId, string followeeId)
    {
        if (followerId == followeeId) return false;

        var exists = await _context.UserFollows
            .AnyAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId);
        
        if (exists) return true;

        _context.UserFollows.Add(new UserFollow { FollowerId = followerId, FolloweeId = followeeId });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnfollowUserAsync(string followerId, string followeeId)
    {
        var follow = await _context.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId);
        
        if (follow == null) return true;

        _context.UserFollows.Remove(follow);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<UserResultViewModel>> GetFollowersAsync(string userId, string? currentUserId)
    {
        return await _context.UserFollows
            .Where(f => f.FolloweeId == userId)
            .Include(f => f.Follower)
            .Select(f => f.Follower)
            .Select(u => new UserResultViewModel(u.Id, u.DisplayName ?? u.UserName ?? "Unknown", u.Email, u.AvatarUrl,
                currentUserId != null && _context.UserFollows.Any(x => x.FollowerId == currentUserId && x.FolloweeId == u.Id)))
            .ToListAsync();
    }

    public async Task<List<UserResultViewModel>> GetFollowingAsync(string userId, string? currentUserId)
    {
        return await _context.UserFollows
            .Where(f => f.FollowerId == userId)
            .Include(f => f.Followee)
            .Select(f => f.Followee)
            .Select(u => new UserResultViewModel(u.Id, u.DisplayName ?? u.UserName ?? "Unknown", u.Email, u.AvatarUrl,
                currentUserId != null && _context.UserFollows.Any(x => x.FollowerId == currentUserId && x.FolloweeId == u.Id)))
            .ToListAsync();
    }

    public async Task<bool> RemoveFollowerAsync(string userId, string followerId)
    {
        var follow = await _context.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == userId);

        if (follow == null) return true;

        _context.UserFollows.Remove(follow);
        await _context.SaveChangesAsync();
        return true;
    }
}

