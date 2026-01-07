using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;

namespace MusicWeb.Services;

public class UserAlbumService : IUserAlbumService
{
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;

    public UserAlbumService(ApplicationDbContext context, IStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<UserAlbum?> CreateAlbumAsync(string userId, string name, bool isPublic, string? description, IFormFile? coverFile, List<int>? songIds)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        string? coverUrl = null;
        if (coverFile != null)
        {
            string safeUserName = (user.UserName ?? "user").Replace("@", "-").Replace(".", "-");
            foreach (char c in Path.GetInvalidFileNameChars()) { safeUserName = safeUserName.Replace(c, '-'); }
            string userFolder = $"{safeUserName}-{userId}";

            coverUrl = await _storageService.UploadFileAsync(
                coverFile.OpenReadStream(),
                coverFile.FileName,
                "useralbums",
                $"{userFolder}/covers"
            );
        }

        var album = new UserAlbum
        {
            UserId = userId,
            Name = name,
            Description = description,
            CoverUrl = coverUrl,
            IsPublic = isPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserAlbums.Add(album);
        await _context.SaveChangesAsync();

        // Add songs if provided
        if (songIds != null && songIds.Any())
        {
            var orderIndex = 0;
            foreach (var songId in songIds)
            {
                // Verify song exists and user has access
                var song = await _context.Songs
                    .Include(s => s.Artist)
                    .FirstOrDefaultAsync(s => s.Id == songId);
                
                if (song != null && (song.IsPublic || song.Artist.UserId == userId))
                {
                    _context.UserAlbumSongs.Add(new UserAlbumSong
                    {
                        UserAlbumId = album.Id,
                        SongId = songId,
                        OrderIndex = orderIndex++,
                        AddedAt = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        return album;
    }

    public async Task<UserAlbumDetailViewModel?> GetAlbumByIdAsync(int albumId, string? userId)
    {
        var album = await _context.UserAlbums
            .Include(a => a.Owner)
            .Include(a => a.UserAlbumSongs)
                .ThenInclude(uas => uas.Song)
                    .ThenInclude(s => s.Artist)
            .Include(a => a.UserAlbumSongs)
                .ThenInclude(uas => uas.Song)
                    .ThenInclude(s => s.SongGenres)
                        .ThenInclude(sg => sg.Genre)
            .FirstOrDefaultAsync(a => a.Id == albumId);

        if (album == null) return null;

        // Check visibility
        if (!album.IsPublic && album.UserId != userId)
            return null;

        var favoriteSongIds = new HashSet<int>();
        var userRatings = new Dictionary<int, decimal>();

        if (userId != null)
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

        var songs = album.UserAlbumSongs
            .OrderBy(uas => uas.OrderIndex)
            .Select(uas => ToSongCard(uas.Song, favoriteSongIds, userRatings))
            .ToList();

        return new UserAlbumDetailViewModel
        {
            Id = album.Id,
            Name = album.Name,
            Description = album.Description,
            CoverUrl = album.CoverUrl ?? "https://picsum.photos/300/300",
            OwnerName = album.Owner.DisplayName ?? album.Owner.UserName ?? "Unknown",
            OwnerId = album.UserId,
            IsPublic = album.IsPublic,
            IsOwner = album.UserId == userId,
            Songs = songs,
            CreatedAt = album.CreatedAt
        };
    }

    public async Task<List<UserAlbumCardViewModel>> GetUserAlbumsAsync(string userId)
    {
        var albums = await _context.UserAlbums
            .Include(a => a.Owner)
            .Include(a => a.UserAlbumSongs)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return albums.Select(a => new UserAlbumCardViewModel
        {
            Id = a.Id,
            Name = a.Name,
            OwnerName = a.Owner.DisplayName ?? a.Owner.UserName ?? "Unknown",
            CoverUrl = a.CoverUrl ?? "https://picsum.photos/300/300",
            SongCount = a.UserAlbumSongs.Count,
            IsPublic = a.IsPublic
        }).ToList();
    }

    public async Task<bool> UpdateAlbumAsync(int albumId, string userId, string name, bool isPublic, string? description, IFormFile? coverFile)
    {
        var album = await _context.UserAlbums
            .FirstOrDefaultAsync(a => a.Id == albumId && a.UserId == userId);

        if (album == null) return false;

        album.Name = name;
        album.IsPublic = isPublic;
        album.Description = description;
        album.UpdatedAt = DateTime.UtcNow;

        if (coverFile != null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                string safeUserName = (user.UserName ?? "user").Replace("@", "-").Replace(".", "-");
                foreach (char c in Path.GetInvalidFileNameChars()) { safeUserName = safeUserName.Replace(c, '-'); }
                string userFolder = $"{safeUserName}-{userId}";

                // Delete old cover if exists
                if (!string.IsNullOrEmpty(album.CoverUrl))
                {
                    await _storageService.DeleteFileAsync(album.CoverUrl);
                }

                album.CoverUrl = await _storageService.UploadFileAsync(
                    coverFile.OpenReadStream(),
                    coverFile.FileName,
                    "useralbums",
                    $"{userFolder}/covers"
                );
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAlbumAsync(int albumId, string userId)
    {
        var album = await _context.UserAlbums
            .Include(a => a.UserAlbumSongs)
            .FirstOrDefaultAsync(a => a.Id == albumId && a.UserId == userId);

        if (album == null) return false;

        // Delete cover image from Cloudflare if exists
        if (!string.IsNullOrEmpty(album.CoverUrl))
        {
            try
            {
                await _storageService.DeleteFileAsync(album.CoverUrl);
            }
            catch (Exception ex)
            {
                // Log but don't fail - continue with album deletion
                Console.WriteLine($"Warning: Failed to delete album cover: {ex.Message}");
            }
        }

        _context.UserAlbums.Remove(album);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddSongToAlbumAsync(int albumId, string userId, int songId)
    {
        var album = await _context.UserAlbums
            .FirstOrDefaultAsync(a => a.Id == albumId && a.UserId == userId);

        if (album == null) return false;

        // Check if song exists and user has access
        var song = await _context.Songs
            .Include(s => s.Artist)
            .FirstOrDefaultAsync(s => s.Id == songId);

        if (song == null || (!song.IsPublic && song.Artist.UserId != userId))
            return false;

        // Check if already in album
        var existing = await _context.UserAlbumSongs
            .AnyAsync(uas => uas.UserAlbumId == albumId && uas.SongId == songId);

        if (existing) return false;

        // Get max order index
        var maxOrder = await _context.UserAlbumSongs
            .Where(uas => uas.UserAlbumId == albumId)
            .MaxAsync(uas => (int?)uas.OrderIndex) ?? -1;

        _context.UserAlbumSongs.Add(new UserAlbumSong
        {
            UserAlbumId = albumId,
            SongId = songId,
            OrderIndex = maxOrder + 1,
            AddedAt = DateTime.UtcNow
        });

        album.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddNewSongToAlbumAsync(int albumId, string userId, IFormFile audioFile, IFormFile? coverFile, string title, string? description)
    {
        var album = await _context.UserAlbums
            .FirstOrDefaultAsync(a => a.Id == albumId && a.UserId == userId);

        if (album == null) return false;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;

        // Get or create artist for user
        var artist = await _context.Artists.FirstOrDefaultAsync(a => a.UserId == userId);
        if (artist == null)
        {
            artist = new Artist
            {
                Name = user.DisplayName ?? user.UserName ?? "Unknown Artist",
                UserId = userId,
                AvatarUrl = user.AvatarUrl
            };
            _context.Artists.Add(artist);
            await _context.SaveChangesAsync();
        }

        string safeUserName = (user.UserName ?? "user").Replace("@", "-").Replace(".", "-");
        foreach (char c in Path.GetInvalidFileNameChars()) { safeUserName = safeUserName.Replace(c, '-'); }
        string userFolder = $"{safeUserName}-{userId}";

        // Upload audio file
        string audioUrl = await _storageService.UploadFileAsync(
            audioFile.OpenReadStream(),
            audioFile.FileName,
            "useralbums",
            $"{userFolder}/music"
        );

        // Upload cover if provided
        string? coverUrl = null;
        if (coverFile != null)
        {
            coverUrl = await _storageService.UploadFileAsync(
                coverFile.OpenReadStream(),
                coverFile.FileName,
                "useralbums",
                $"{userFolder}/covers"
            );
        }

        // Create song
        var song = new Song
        {
            Title = title,
            Description = description,
            ArtistId = artist.Id,
            AudioUrl = audioUrl,
            CoverUrl = coverUrl,
            Duration = TimeSpan.FromSeconds(180), // Default duration
            ReleaseDate = DateTime.UtcNow,
            IsPublic = album.IsPublic // Match album visibility
        };

        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Add to album
        var maxOrder = await _context.UserAlbumSongs
            .Where(uas => uas.UserAlbumId == albumId)
            .MaxAsync(uas => (int?)uas.OrderIndex) ?? -1;

        _context.UserAlbumSongs.Add(new UserAlbumSong
        {
            UserAlbumId = albumId,
            SongId = song.Id,
            OrderIndex = maxOrder + 1,
            AddedAt = DateTime.UtcNow
        });

        album.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveSongFromAlbumAsync(int albumId, string userId, int songId, bool deleteSongFile)
    {
        var album = await _context.UserAlbums
            .FirstOrDefaultAsync(a => a.Id == albumId && a.UserId == userId);

        if (album == null) return false;

        var albumSong = await _context.UserAlbumSongs
            .FirstOrDefaultAsync(uas => uas.UserAlbumId == albumId && uas.SongId == songId);

        if (albumSong == null) return false;

        _context.UserAlbumSongs.Remove(albumSong);

        if (deleteSongFile)
        {
            var song = await _context.Songs
                .Include(s => s.Artist)
                .FirstOrDefaultAsync(s => s.Id == songId);

            // Only delete if user owns the song
            if (song != null && song.Artist.UserId == userId)
            {
                // Delete from Cloudflare
                if (!string.IsNullOrEmpty(song.AudioUrl))
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(song.AudioUrl);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to delete audio file: {ex.Message}");
                    }
                }

                if (!string.IsNullOrEmpty(song.CoverUrl))
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(song.CoverUrl);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to delete cover file: {ex.Message}");
                    }
                }

                // Remove song from database
                _context.Songs.Remove(song);
            }
        }

        album.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    private SongCardViewModel ToSongCard(Song song, HashSet<int> favoriteSongIds, Dictionary<int, decimal> userRatings)
    {
        var genre = song.SongGenres.FirstOrDefault()?.Genre;
        return new SongCardViewModel(
            song.Id,
            song.Title,
            song.Artist.Name,
            song.CoverUrl ?? "https://picsum.photos/300/300",
            song.Duration.ToString(@"mm\:ss"),
            favoriteSongIds.Contains(song.Id),
            song.AudioUrl ?? "",
            userRatings.GetValueOrDefault(song.Id),
            genre?.Name ?? "",
            song.ReleaseDate,
            song.ViewCount,
            null, // AverageRating - can be calculated if needed
            song.IsPublic,
            genre?.Id,
            song.IsPremium,
            song.PremiumStatus
        );
    }
}
