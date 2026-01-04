namespace MusicWeb.Models.Entities;

public class Song
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public string? AudioUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? LyricsUrl { get; set; }
    public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;
    public bool IsPublic { get; set; } = true;

    public int ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    public int? AlbumId { get; set; }
    public Album? Album { get; set; }

    public ICollection<SongGenre> SongGenres { get; set; } = new List<SongGenre>();
    public ICollection<LyricLine> Lyrics { get; set; } = new List<LyricLine>();
    public ICollection<PlaylistSong> PlaylistSongs { get; set; } = new List<PlaylistSong>();
    public ICollection<FavoriteSong> Favorites { get; set; } = new List<FavoriteSong>();
    public ICollection<ChartEntry> ChartEntries { get; set; } = new List<ChartEntry>();
    public ICollection<PlayHistory> PlayHistories { get; set; } = new List<PlayHistory>();
    public ICollection<UserSongRating> UserRatings { get; set; } = new List<UserSongRating>();
    public ICollection<UserAlbumSong> UserAlbumSongs { get; set; } = new List<UserAlbumSong>();
    
    public int ViewCount { get; set; } = 0;
}

