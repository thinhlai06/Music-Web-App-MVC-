using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MusicWeb.Models.Entities;

namespace MusicWeb.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await context.Database.MigrateAsync();

        if (!context.Artists.Any())
        {
            var artists = new[]
            {
                new Artist { Name = "Phương Ly", AvatarUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/phuongly.jpg" },
                new Artist { Name = "Sobin Hoàng Sơn", AvatarUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/soobinhoangson.jpg" },
                new Artist { Name = "Dương Domic", AvatarUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/duongdomic.jpg" },
                new Artist { Name = "Low G", AvatarUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/lowg.jpg" },
                //new Artist { Name = "Double2T", AvatarUrl = "https://picsum.photos/id/68/200/200" }
            };
            context.Artists.AddRange(artists);
            await context.SaveChangesAsync();
        }

        if (!context.Genres.Any())
        {
            context.Genres.AddRange(
                new Genre { Name = "EDM Sôi Động", TileImageUrl = "https://picsum.photos/id/310/400/200" },
                new Genre { Name = "Acoustic Chill", TileImageUrl = "https://picsum.photos/id/320/400/200" },
                new Genre { Name = "Lofi Học Bài", TileImageUrl = "https://picsum.photos/id/330/400/200" },
                new Genre { Name = "Bolero Trữ Tình", TileImageUrl = "https://picsum.photos/id/340/400/200" },
                new Genre { Name = "Nhạc Âu Mỹ", TileImageUrl = "https://picsum.photos/id/350/400/200" },
                new Genre { Name = "Nhạc Hàn Quốc", TileImageUrl = "https://picsum.photos/id/360/400/200" },
                new Genre { Name = "Nhạc Hoa Ngữ", TileImageUrl = "https://picsum.photos/id/370/400/200" },
                new Genre { Name = "Nhạc Việt", TileImageUrl = "https://picsum.photos/id/380/400/200" },
                new Genre { Name = "Nhạc Thiếu Nhi", TileImageUrl = "https://picsum.photos/id/390/400/200" },
                new Genre { Name = "Nhạc Hip Hop", TileImageUrl = "https://picsum.photos/id/400/400/200" },
                new Genre { Name = "Nhạc Rock", TileImageUrl = "https://picsum.photos/id/410/400/200" },
                new Genre { Name = "Nhạc R&B", TileImageUrl = "https://picsum.photos/id/420/400/200" },
                new Genre { Name = "Nhạc Jazz", TileImageUrl = "https://picsum.photos/id/430/400/200" }
            );
            await context.SaveChangesAsync();
        }

        if (!context.Songs.Any())
        {
            var artistLookup = await context.Artists.ToDictionaryAsync(a => a.Name, a => a.Id);

            var songs = new[]
            {
                new Song
                {
                    Title = "Anh Là Ai",
                    ArtistId = artistLookup["Phương Ly"],
                    Duration = TimeSpan.FromSeconds(257),
                    CoverUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/0806ea1c463042079f3fc0316d2032b0.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-20),
                    AudioUrl = "https://pub-58b2e24834214ddb922477279bdf5ff7.r2.dev/Anh%20L%C3%A0%20Ai.mp3",
                    LyricsUrl="https://pub-87373e218bc4407c8df5c79268c30d48.r2.dev/anh_la_ai.txt"
                },
                new Song
                {
                    Title = "Dancing In The Dark",
                    ArtistId = artistLookup["Sobin Hoàng Sơn"],
                    Duration = TimeSpan.FromSeconds(227),
                    CoverUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/c898ca8096ca588034541539ee1d9016.1000x1000x1.png",
                    ReleaseDate = DateTime.UtcNow.AddDays(-10),
                    AudioUrl = "https://pub-58b2e24834214ddb922477279bdf5ff7.r2.dev/Dancing%20In%20The%20Dark.mp3",
                    LyricsUrl="https://pub-87373e218bc4407c8df5c79268c30d48.r2.dev/dancing_in_the_dark.txt"
                },
                new Song
                {
                    Title = "Giá Như",
                    ArtistId = artistLookup["Sobin Hoàng Sơn"],
                    Duration = TimeSpan.FromSeconds(223),
                    CoverUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/120240420052059.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-5),
                    AudioUrl = "https://pub-58b2e24834214ddb922477279bdf5ff7.r2.dev/Gi%C3%A1%20Nh%C6%B0.mp3",
                    LyricsUrl="https://pub-87373e218bc4407c8df5c79268c30d48.r2.dev/gia_nhu.txt"
                },
                new Song
                {
                    Title = "Mất Kết Nối",
                    ArtistId = artistLookup["Dương Domic"],
                    Duration = TimeSpan.FromSeconds(207),
                    CoverUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/maxresdefault.jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-15),
                    AudioUrl = "https://pub-58b2e24834214ddb922477279bdf5ff7.r2.dev/M%E1%BA%A5t%20K%E1%BA%BFt%20N%E1%BB%91i.mp3",
                    LyricsUrl="https://pub-87373e218bc4407c8df5c79268c30d48.r2.dev/mat_ket_noi.txt"
                },
                new Song
                {
                    Title = "Phóng Zìn Zìn",
                    ArtistId = artistLookup["Low G"],
                    Duration = TimeSpan.FromSeconds(202),
                    CoverUrl = "https://pub-324f8069623142339b8c13eaa25fe400.r2.dev/maxresdefault%20(1).jpg",
                    ReleaseDate = DateTime.UtcNow.AddDays(-2),
                    AudioUrl = "https://pub-58b2e24834214ddb922477279bdf5ff7.r2.dev/PH%C3%93NG%20Z%C3%8CN%20Z%C3%8CN.mp3",
                    LyricsUrl="https://pub-87373e218bc4407c8df5c79268c30d48.r2.dev/phong_zin_zin.txt"
                }
            };

            context.Songs.AddRange(songs);
            await context.SaveChangesAsync();

            // simple genre assignments
            var genres = await context.Genres.ToListAsync();
            foreach (var song in songs)
            {
                var genre = genres[new Random(song.Id).Next(genres.Count)];
                context.SongGenres.Add(new SongGenre { SongId = song.Id, GenreId = genre.Id });
            }

            await context.SaveChangesAsync();
        }

        if (!context.ChartEntries.Any())
        {
            var songs = await context.Songs.ToListAsync();
            var i = 1;
            foreach (var song in songs.Take(3))
            {
                context.ChartEntries.Add(new ChartEntry
                {
                    SongId = song.Id,
                    Rank = i,
                    ChartType = "weekly",
                    Percentage = i switch
                    {
                        1 => 0.45,
                        2 => 0.2,
                        _ => 0.15
                    }
                });
                i++;
            }

            await context.SaveChangesAsync();
        }

        // Seed Roles
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().RoleExistsAsync(role))
            {
                await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole(role));
            }
        }

        var demoUser = await userManager.FindByEmailAsync("demo@musicweb.local");
        if (demoUser is null)
        {
            demoUser = new ApplicationUser
            {
                UserName = "demo@musicweb.local",
                Email = "demo@musicweb.local",                
                DisplayName = "Nguyen Van A",
                AvatarUrl = "https://ui-avatars.com/api/?name=Nguyen+Van+A&background=9b4de0&color=fff"
            };

            var result = await userManager.CreateAsync(demoUser, "Demo@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(demoUser, "User");
            }
        }

        // Seed Admin User
        var adminUser = await userManager.FindByEmailAsync("admin@musicweb.local");
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin@musicweb.local",
                Email = "admin@musicweb.local",
                DisplayName = "Admin User",
                AvatarUrl = "https://ui-avatars.com/api/?name=Admin+User&background=ff0000&color=fff"
            };

            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
        else
        {
            // Ensure Admin Role
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
            
            // Ensure Password is known (Safe for Dev Environment)
            var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);
            await userManager.ResetPasswordAsync(adminUser, token, "Admin@123");
        }

        if (!context.Playlists.Any())
        {
            var playlist = new Playlist
            {
                Name = "Nhạc Của Tui",
                Description = "Những bài hát yêu thích nhất",
                CoverUrl = "https://picsum.photos/id/180/300/300",
                OwnerId = demoUser.Id,
                Songs = new List<PlaylistSong>()
            };

            var songs = await context.Songs.Take(3).ToListAsync();
            var order = 1;
            foreach (var song in songs)
            {
                playlist.Songs.Add(new PlaylistSong { SongId = song.Id, Order = order++ });
            }

            context.Playlists.Add(playlist);
            await context.SaveChangesAsync();
        }
    }
}

