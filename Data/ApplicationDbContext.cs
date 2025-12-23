using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MusicWeb.Models.Entities;

namespace MusicWeb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<SongGenre> SongGenres => Set<SongGenre>();
    public DbSet<LyricLine> LyricLines => Set<LyricLine>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistSong> PlaylistSongs => Set<PlaylistSong>();
    public DbSet<FavoriteSong> FavoriteSongs => Set<FavoriteSong>();
    public DbSet<ChartEntry> ChartEntries => Set<ChartEntry>();
    public DbSet<PlayHistory> PlayHistories => Set<PlayHistory>();
    public DbSet<UserSongRating> UserSongRatings => Set<UserSongRating>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SongGenre>()
            .HasKey(x => new { x.SongId, x.GenreId });

        builder.Entity<SongGenre>()
            .HasOne(x => x.Song)
            .WithMany(x => x.SongGenres)
            .HasForeignKey(x => x.SongId);

        builder.Entity<SongGenre>()
            .HasOne(x => x.Genre)
            .WithMany(x => x.SongGenres)
            .HasForeignKey(x => x.GenreId);

        builder.Entity<PlaylistSong>()
            .HasKey(x => new { x.PlaylistId, x.SongId });

        builder.Entity<PlaylistSong>()
            .HasOne(x => x.Playlist)
            .WithMany(x => x.Songs)
            .HasForeignKey(x => x.PlaylistId);

        builder.Entity<PlaylistSong>()
            .HasOne(x => x.Song)
            .WithMany(x => x.PlaylistSongs)
            .HasForeignKey(x => x.SongId);

        builder.Entity<FavoriteSong>()
            .HasKey(x => new { x.UserId, x.SongId });

        builder.Entity<FavoriteSong>()
            .HasOne(x => x.User)
            .WithMany(x => x.FavoriteSongs)
            .HasForeignKey(x => x.UserId);

        builder.Entity<FavoriteSong>()
            .HasOne(x => x.Song)
            .WithMany(x => x.Favorites)
            .HasForeignKey(x => x.SongId);

        builder.Entity<UserSongRating>()
            .HasKey(x => new { x.UserId, x.SongId });

        builder.Entity<UserSongRating>()
            .HasOne(x => x.User)
            .WithMany(x => x.SongRatings)
            .HasForeignKey(x => x.UserId);

        builder.Entity<UserSongRating>()
            .HasOne(x => x.Song)
            .WithMany(x => x.UserRatings)
            .HasForeignKey(x => x.SongId);

        builder.Entity<UserFollow>()
            .HasKey(x => new { x.FollowerId, x.FolloweeId });

        builder.Entity<UserFollow>()
            .HasOne(x => x.Follower)
            .WithMany(x => x.Following)
            .HasForeignKey(x => x.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserFollow>()
            .HasOne(x => x.Followee)
            .WithMany(x => x.Followers)
            .HasForeignKey(x => x.FolloweeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}


