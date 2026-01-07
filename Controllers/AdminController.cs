using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;
using MusicWeb.Services;

namespace MusicWeb.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly IStorageService _storageService;

    public AdminController(
        ApplicationDbContext context, 
        UserManager<ApplicationUser> userManager, 
        IWebHostEnvironment environment,
        IStorageService storageService)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
        _storageService = storageService;
    }

    public async Task<IActionResult> Index()
    {
        var songs = await _context.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .OrderByDescending(s => s.ReleaseDate)
            .Take(50) // Limit display
            .ToListAsync();
            
        return View(songs);
    }

    [HttpGet]
    public IActionResult CreateSong()
    {
        ViewBag.Genres = _context.Genres.ToList();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateSong(CreateSongViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Genres = _context.Genres.ToList();
            return View(model);
        }

        // Handle Audio
        string? audioUrl = model.AudioUrlInput;
        if (model.AudioFile != null)
        {
            // Create uploads folder if not exists
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "music");
            Directory.CreateDirectory(uploadsFolder);
            
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.AudioFile.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.AudioFile.CopyToAsync(fileStream);
            }
            audioUrl = "/uploads/music/" + uniqueFileName;
        }

        // Handle Cover
        string? coverUrl = model.CoverUrlInput;
        if (model.CoverFile != null)
        {
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "covers");
            Directory.CreateDirectory(uploadsFolder);
            
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.CoverFile.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.CoverFile.CopyToAsync(fileStream);
            }
            coverUrl = "/uploads/covers/" + uniqueFileName;
        }

        // Handle Artist (Find or Create)
        var artist = await _context.Artists.FirstOrDefaultAsync(a => a.Name == model.ArtistName);
        if (artist == null)
        {
            artist = new Artist { Name = model.ArtistName, AvatarUrl = coverUrl }; // Use cover as avatar fallback
            _context.Artists.Add(artist);
            await _context.SaveChangesAsync();
        }

        // Create Song
        var song = new Song
        {
            Title = model.Title,
            ArtistId = artist.Id,
            Duration = TimeSpan.FromSeconds(model.DurationSeconds > 0 ? model.DurationSeconds : 180), // Default 3 mins if 0
            AudioUrl = audioUrl,
            CoverUrl = coverUrl,
            ReleaseDate = DateTime.UtcNow
        };
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Add Genre
        if (model.GenreId > 0)
        {
            _context.SongGenres.Add(new SongGenre { SongId = song.Id, GenreId = model.GenreId });
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    public class CreateSongViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int GenreId { get; set; }
        public int DurationSeconds { get; set; }
        public IFormFile? AudioFile { get; set; }
        public string? AudioUrlInput { get; set; }
        public IFormFile? CoverFile { get; set; }
        public string? CoverUrlInput { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> EditSong(int id)
    {
        var song = await _context.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (song == null) return NotFound();

        ViewBag.Genres = _context.Genres.ToList();
        ViewBag.Albums = _context.Albums.ToList(); // Assume Albums exist or empty

        var model = new EditSongViewModel
        {
            Id = song.Id,
            Title = song.Title,
            ArtistName = song.Artist.Name,
            GenreId = song.SongGenres.FirstOrDefault()?.GenreId ?? 0,
            AlbumId = song.AlbumId,
            DurationSeconds = (int)song.Duration.TotalSeconds,
            ReleaseDate = song.ReleaseDate,
            LyricsUrlInput = song.LyricsUrl
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> EditSong(EditSongViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Genres = _context.Genres.ToList();
            ViewBag.Albums = _context.Albums.ToList();
            return View(model);
        }

        var song = await _context.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres)
            .FirstOrDefaultAsync(s => s.Id == model.Id);

        if (song == null) return NotFound();

        // Update basic info
        song.Title = model.Title;
        song.Duration = TimeSpan.FromSeconds(model.DurationSeconds > 0 ? model.DurationSeconds : 180);
        song.AlbumId = model.AlbumId;
        song.ReleaseDate = model.ReleaseDate;
        song.LyricsUrl = model.LyricsUrlInput; // Allow direct URL update for lyrics

        // Update Artist (Find or Create)
        if (song.Artist.Name != model.ArtistName)
        {
            var artist = await _context.Artists.FirstOrDefaultAsync(a => a.Name == model.ArtistName);
            if (artist == null)
            {
                artist = new Artist { Name = model.ArtistName, AvatarUrl = song.CoverUrl }; 
                _context.Artists.Add(artist);
                await _context.SaveChangesAsync();
            }
            song.ArtistId = artist.Id;
        }

        // Update Genre
        var currentGenre = song.SongGenres.FirstOrDefault();
        if (model.GenreId > 0)
        {
            if (currentGenre != null)
            {
                if (currentGenre.GenreId != model.GenreId)
                {
                    _context.SongGenres.Remove(currentGenre);
                    _context.SongGenres.Add(new SongGenre { SongId = song.Id, GenreId = model.GenreId });
                }
            }
            else
            {
                _context.SongGenres.Add(new SongGenre { SongId = song.Id, GenreId = model.GenreId });
            }
        }

        // Handle File Updates
        string uploadsFolder = _environment.WebRootPath;
        
        if (model.AudioFile != null)
        {
            string path = Path.Combine(uploadsFolder, "uploads", "music");
            Directory.CreateDirectory(path);
            string fileName = Guid.NewGuid() + "_" + model.AudioFile.FileName;
            using (var stream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
            {
                await model.AudioFile.CopyToAsync(stream);
            }
            song.AudioUrl = "/uploads/music/" + fileName;
        }
        else if (!string.IsNullOrEmpty(model.AudioUrlInput))
        {
            song.AudioUrl = model.AudioUrlInput;
        }

        if (model.CoverFile != null)
        {
            string path = Path.Combine(uploadsFolder, "uploads", "covers");
            Directory.CreateDirectory(path);
            string fileName = Guid.NewGuid() + "_" + model.CoverFile.FileName;
            using (var stream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
            {
                await model.CoverFile.CopyToAsync(stream);
            }
            song.CoverUrl = "/uploads/covers/" + fileName;
        }
        else if (!string.IsNullOrEmpty(model.CoverUrlInput))
        {
            song.CoverUrl = model.CoverUrlInput;
        }

        // Handle Lyrics File Upload
        if (model.LyricsFile != null)
        {
            string path = Path.Combine(uploadsFolder, "uploads", "lyrics");
            Directory.CreateDirectory(path);
            string fileName = Guid.NewGuid() + "_" + model.LyricsFile.FileName;
            using (var stream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
            {
                await model.LyricsFile.CopyToAsync(stream);
            }
            song.LyricsUrl = "/uploads/lyrics/" + fileName;
        }

        await _context.SaveChangesAsync();
        TempData["Message"] = "Cập nhật thành công";
        return RedirectToAction(nameof(Index)); // Or stay on Edit page? User said "Lưu thay đổi -> Cập nhật thành công", usually implies stay or list. Redirect to Index with toast is safer.
    }

    public class EditSongViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int GenreId { get; set; }
        public int? AlbumId { get; set; }
        public int DurationSeconds { get; set; }
        public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;
        public IFormFile? AudioFile { get; set; }
        public string? AudioUrlInput { get; set; }
        public IFormFile? CoverFile { get; set; }
        public string? CoverUrlInput { get; set; }
        public IFormFile? LyricsFile { get; set; }
        public string? LyricsUrlInput { get; set; }
    }
    [HttpPost]
    public async Task<IActionResult> DeleteSong(int id)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song != null)
        {
            // Delete files from R2 if they are external URLs
            if (!string.IsNullOrEmpty(song.AudioUrl) && song.AudioUrl.StartsWith("http"))
            {
                await _storageService.DeleteFileAsync(song.AudioUrl);
            }

            if (!string.IsNullOrEmpty(song.CoverUrl) && song.CoverUrl.StartsWith("http"))
            {
                await _storageService.DeleteFileAsync(song.CoverUrl);
            }

            // Also handle local files deletion if needed (logic existed implicitly or not, but good to have)
            // (Existing code for local files was likely missing or manually handled else where, but user focus is R2)

            _context.Songs.Remove(song);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    // User Management
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.ToListAsync();
        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUserStatus(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }
        
        return RedirectToAction(nameof(Users));
    }
    // Album Management
    public async Task<IActionResult> Albums(string? tab)
    {
        var systemAlbums = await _context.Albums
            .Include(a => a.Artist)
            .Include(a => a.Songs)
            .OrderByDescending(a => a.ReleaseDate)
            .ToListAsync();
        
        var userAlbums = await _context.UserAlbums
            .Include(a => a.Owner)
            .Include(a => a.UserAlbumSongs)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        
        ViewBag.Tab = tab ?? "system";
        
        return View(new AdminAlbumsViewModel
        {
            SystemAlbums = systemAlbums,
            UserAlbums = userAlbums
        });
    }
    
    [HttpPost]
    public async Task<IActionResult> ToggleUserAlbumVisibility(int id)
    {
        var album = await _context.UserAlbums.FindAsync(id);
        if (album != null)
        {
            album.IsPublic = !album.IsPublic;
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Đã {(album.IsPublic ? "công khai" : "ẩn")} album";
        }
        return RedirectToAction(nameof(Albums), new { tab = "user" });
    }
    
    [HttpPost]
    public async Task<IActionResult> DeleteUserAlbum(int id)
    {
        var album = await _context.UserAlbums
            .Include(a => a.UserAlbumSongs)
            .FirstOrDefaultAsync(a => a.Id == id);
        
        if (album != null)
        {
            // Remove songs from album first
            album.UserAlbumSongs?.Clear();
            _context.UserAlbums.Remove(album);
            await _context.SaveChangesAsync();
            TempData["Message"] = "Đã xóa album";
        }
        return RedirectToAction(nameof(Albums), new { tab = "user" });
    }

    [HttpGet]
    public IActionResult CreateAlbum()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateAlbum(CreateAlbumViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string? coverUrl = null;
        if (model.CoverFile != null)
        {
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "albums");
            Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.CoverFile.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.CoverFile.CopyToAsync(fileStream);
            }
            coverUrl = "/uploads/albums/" + uniqueFileName;
        }
        else if (!string.IsNullOrEmpty(model.CoverUrlInput))
        {
            coverUrl = model.CoverUrlInput;
        }

        var artist = await _context.Artists.FirstOrDefaultAsync(a => a.Name == model.ArtistName);
        if (artist == null)
        {
            artist = new Artist { Name = model.ArtistName, AvatarUrl = coverUrl };
            _context.Artists.Add(artist);
            await _context.SaveChangesAsync();
        }

        var album = new Album
        {
            Title = model.Title,
            ArtistId = artist.Id,
            ReleaseDate = model.ReleaseDate.HasValue ? model.ReleaseDate.Value : DateTime.UtcNow,
            CoverUrl = coverUrl
        };
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Albums));
    }

    [HttpGet]
    public async Task<IActionResult> EditAlbum(int id)
    {
        var album = await _context.Albums
            .Include(a => a.Artist)
            .Include(a => a.Songs)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (album == null) return NotFound();

        var model = new EditAlbumViewModel
        {
            Id = album.Id,
            Title = album.Title,
            ArtistName = album.Artist.Name,
            CoverUrl = album.CoverUrl,
            Songs = album.Songs.OrderBy(s => s.Title).ToList(),
            AvailableSongs = await _context.Songs
                .Include(s => s.Artist)
                .Where(s => s.AlbumId == null || s.AlbumId != id) // Optional: Just show all or filter. Let's show all for flexibility, or maybe just those without album? User said "Select by name".
                .OrderBy(s => s.Title)
                .ToListAsync()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> EditAlbum(EditAlbumViewModel model)
    {
        if (!ModelState.IsValid)
        {
             var alb = await _context.Albums.Include(a => a.Songs).FirstOrDefaultAsync(a => a.Id == model.Id);
             if(alb != null) model.Songs = alb.Songs.ToList(); 
             return View(model);
        }

        var album = await _context.Albums.Include(a => a.Artist).FirstOrDefaultAsync(a => a.Id == model.Id);
        if (album == null) return NotFound();

        album.Title = model.Title;
        
        if (album.Artist.Name != model.ArtistName)
        {
             var artist = await _context.Artists.FirstOrDefaultAsync(a => a.Name == model.ArtistName);
             if (artist == null)
             {
                 artist = new Artist { Name = model.ArtistName };
                 _context.Artists.Add(artist);
                 await _context.SaveChangesAsync();
             }
             album.ArtistId = artist.Id;
        }

        if (model.CoverFile != null)
        {
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "albums");
            Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.CoverFile.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.CoverFile.CopyToAsync(fileStream);
            }
            album.CoverUrl = "/uploads/albums/" + uniqueFileName;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Albums));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var album = await _context.Albums.Include(a => a.Songs).FirstOrDefaultAsync(a => a.Id == id);
        if (album != null)
        {
            // Set AlbumId to null for all songs in this album to avoid FK constraint error
            foreach (var song in album.Songs)
            {
                song.AlbumId = null;
            }
            
            _context.Albums.Remove(album);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Albums));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveSongFromAlbum(int albumId, int songId)
    {
        var song = await _context.Songs.FindAsync(songId);
        if (song != null && song.AlbumId == albumId)
        {
            song.AlbumId = null;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(EditAlbum), new { id = albumId });
    }

    [HttpPost]
    public async Task<IActionResult> AddSongToAlbum(int albumId, string songTitle) // Simple search by title for now or we can use ID if UI supports it
    {
        // Better implementation: Receives Song ID from a selection list in UI.
        // Or if we implemented "Connect Song", but let's assume we pass ID if we build a proper UI.
        // For simplicity let's assume we might pass an ID if we have a modal selector.
        // But to make it easier to start, maybe just a partial view or separate action?
        // Let's stick to "Add existing song" via a search/dropdown in the View. 
        // I will implement a separate action that adds based on ID provided by a form.
        return RedirectToAction(nameof(EditAlbum), new { id = albumId });
    }
    
    [HttpPost]
    public async Task<IActionResult> AddSongToAlbumById(int albumId, int songId)
    {
        var song = await _context.Songs.FindAsync(songId);
        if (song != null)
        {
            song.AlbumId = albumId;
            await _context.SaveChangesAsync();
        }
         return RedirectToAction(nameof(EditAlbum), new { id = albumId });
    }


    public class CreateAlbumViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public IFormFile? CoverFile { get; set; }
        public string? CoverUrlInput { get; set; }
        public DateTime? ReleaseDate { get; set; }
    }

    public class EditAlbumViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public IFormFile? CoverFile { get; set; }
        public List<Song> Songs { get; set; } = new();
        public List<Song> AvailableSongs { get; set; } = new();
    }

    [HttpGet]
    public IActionResult SendNotification()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SendNotification(string title, string message, string? link)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            ModelState.AddModelError("", "Tiêu đề và nội dung là bắt buộc");
            return View();
        }

        var users = await _userManager.Users.Select(u => u.Id).ToListAsync();
        var notifications = new List<Notification>();

        foreach (var userId in users)
        {
            notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Link = link,
                Type = "System",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        // Add in batches if too many, but for now assuming reasonable user count
        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Đã gửi thông báo đến " + notifications.Count + " người dùng.";
        return RedirectToAction(nameof(SendNotification));
    }
    
    // =============================================
    // DASHBOARD
    // =============================================
    
    public async Task<IActionResult> Dashboard()
    {
        var totalSongs = await _context.Songs.CountAsync();
        var totalUsers = await _context.Users.CountAsync();
        var premiumUsers = await _context.UserSubscriptions
            .Where(s => s.Status == "Active" && (s.EndDate == null || s.EndDate > DateTime.UtcNow))
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync();
        
        // Total revenue from successful subscription purchases
        var totalRevenue = await _context.WalletTransactions
            .Where(t => t.Type == "Purchase" && t.Amount < 0)
            .SumAsync(t => Math.Abs(t.Amount));
        
        var recentRequests = await _context.PremiumSongRequests
            .Include(r => r.Song).ThenInclude(s => s!.Artist)
            .Include(r => r.RequestedByUser)
            .OrderByDescending(r => r.RequestDate)
            .Take(5)
            .ToListAsync();
        
        ViewBag.TotalSongs = totalSongs;
        ViewBag.TotalUsers = totalUsers;
        ViewBag.PremiumUsers = premiumUsers;
        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.RecentRequests = recentRequests;
        
        return View();
    }
    
    [HttpGet]
    [Route("/api/admin/stats")]
    public async Task<IActionResult> GetAdminStats()
    {
        var totalSongs = await _context.Songs.CountAsync();
        var totalUsers = await _context.Users.CountAsync();
        var premiumUsers = await _context.UserSubscriptions
            .Where(s => s.Status == "Active" && (s.EndDate == null || s.EndDate > DateTime.UtcNow))
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync();
        
        var totalRevenue = await _context.WalletTransactions
            .Where(t => t.Type == "Purchase" && t.Amount < 0)
            .SumAsync(t => Math.Abs(t.Amount));
        
        var pendingRequests = await _context.PremiumSongRequests
            .Where(r => r.Status == "Pending")
            .CountAsync();
        
        return Ok(new
        {
            totalSongs,
            totalUsers,
            premiumUsers,
            totalRevenue,
            pendingRequests
        });
    }
    
    // =============================================
    // PREMIUM SONG REQUESTS
    // =============================================
    
    public async Task<IActionResult> PremiumSongRequests(string? status)
    {
        var query = _context.PremiumSongRequests
            .Include(r => r.Song).ThenInclude(s => s!.Artist)
            .Include(r => r.RequestedByUser)
            .OrderByDescending(r => r.RequestDate)
            .AsQueryable();
        
        ViewBag.Status = status ?? "Pending";
        
        if (!string.IsNullOrEmpty(status) && status != "all")
        {
            query = query.Where(r => r.Status == status);
        }
        else if (string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == "Pending");
        }
        
        var requests = await query.ToListAsync();
        return View(requests);
    }
    
    [HttpPost]
    public async Task<IActionResult> ApprovePremiumSong(int requestId)
    {
        var request = await _context.PremiumSongRequests
            .Include(r => r.Song)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        
        if (request != null && request.Status == "Pending")
        {
            request.Status = "Approved";
            request.ReviewedDate = DateTime.UtcNow;
            request.ReviewedByAdminId = _userManager.GetUserId(User);
            request.AdminNote = "Đã duyệt";
            
            // Update song to Premium
            if (request.Song != null)
            {
                request.Song.IsPremium = true;
                request.Song.PremiumStatus = "Approved";
            }
            
            // Send notification to user
            _context.Notifications.Add(new Notification
            {
                UserId = request.RequestedByUserId,
                Title = "Yêu cầu Premium được duyệt",
                Message = $"Bài hát '{request.Song?.Title}' của bạn đã được duyệt thành Premium!",
                Type = "PremiumApproved",
                CreatedAt = DateTime.UtcNow
            });
            
            await _context.SaveChangesAsync();
            TempData["Message"] = "Đã duyệt bài hát Premium";
        }
        
        return RedirectToAction(nameof(PremiumSongRequests));
    }
    
    [HttpPost]
    public async Task<IActionResult> RejectPremiumSong(int requestId, string reason)
    {
        var request = await _context.PremiumSongRequests
            .Include(r => r.Song)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        
        if (request != null && request.Status == "Pending")
        {
            request.Status = "Rejected";
            request.ReviewedDate = DateTime.UtcNow;
            request.ReviewedByAdminId = _userManager.GetUserId(User);
            request.AdminNote = reason;
            
            // Send notification to user
            _context.Notifications.Add(new Notification
            {
                UserId = request.RequestedByUserId,
                Title = "Yêu cầu Premium bị từ chối",
                Message = $"Bài hát '{request.Song?.Title}' không được duyệt. Lý do: {reason}",
                Type = "PremiumRejected",
                CreatedAt = DateTime.UtcNow
            });
            
            await _context.SaveChangesAsync();
            TempData["Message"] = "Đã từ chối yêu cầu";
        }
        
        return RedirectToAction(nameof(PremiumSongRequests));
    }
    
    [HttpPost]
    public async Task<IActionResult> RemovePremiumFromSong(int songId)
    {
        var song = await _context.Songs
            .Include(s => s.Artist)
            .FirstOrDefaultAsync(s => s.Id == songId);
        
        if (song != null && song.IsPremium)
        {
            song.IsPremium = false;
            song.PremiumStatus = "";  // Use empty string as DB doesn't allow NULL
            
            // Notify the owner
            if (song.Artist.UserId != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = song.Artist.UserId,
                    Title = "Bài hát không còn Premium",
                    Message = $"Bài hát '{song.Title}' đã bị gỡ trạng thái Premium bởi Admin.",
                    Type = "PremiumRemoved",
                    CreatedAt = DateTime.UtcNow
                });
            }
            
            await _context.SaveChangesAsync();
            TempData["Message"] = "Đã gỡ Premium khỏi bài hát";
        }
        
        return RedirectToAction(nameof(Index));
    }
    
    // =============================================
    // SUBSCRIPTION PLANS
    // =============================================
    
    public async Task<IActionResult> SubscriptionPlans()
    {
        var plans = await _context.SubscriptionPlans
            .OrderBy(p => p.Price)
            .ToListAsync();
        return View(plans);
    }
    
    [HttpPost]
    public async Task<IActionResult> SavePlan(SubscriptionPlan model)
    {
        if (model.Id == 0)
        {
            // Create new
            model.CreatedAt = DateTime.UtcNow;
            model.IsActive = true;
            _context.SubscriptionPlans.Add(model);
            TempData["Message"] = "Đã thêm gói mới";
        }
        else
        {
            // Update existing
            var plan = await _context.SubscriptionPlans.FindAsync(model.Id);
            if (plan != null)
            {
                plan.Name = model.Name;
                plan.Description = model.Description;
                plan.Price = model.Price;
                plan.DurationDays = model.DurationDays;
                plan.DownloadLimit = model.DownloadLimit;
                plan.NoAds = model.NoAds;
                plan.HighQualityAudio = model.HighQualityAudio;
                plan.CanAccessPremiumSongs = model.CanAccessPremiumSongs;
                TempData["Message"] = "Đã cập nhật gói";
            }
        }
        
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(SubscriptionPlans));
    }
    
    [HttpPost]
    public async Task<IActionResult> TogglePlan(int id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan != null)
        {
            plan.IsActive = !plan.IsActive;
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Đã {(plan.IsActive ? "kích hoạt" : "tạm dừng")} gói";
        }
        return RedirectToAction(nameof(SubscriptionPlans));
    }
    
    // =============================================
    // SUBSCRIBERS
    // =============================================
    
    public async Task<IActionResult> Subscribers(string? status)
    {
        var query = _context.UserSubscriptions
            .Include(s => s.User)
            .Include(s => s.Plan)
            .OrderByDescending(s => s.StartDate)
            .AsQueryable();
        
        ViewBag.Status = status ?? "Active";
        
        if (status == "Active" || string.IsNullOrEmpty(status))
        {
            query = query.Where(s => s.Status == "Active" && (s.EndDate == null || s.EndDate > DateTime.UtcNow));
        }
        else if (status == "Expired")
        {
            query = query.Where(s => s.Status == "Expired" || (s.EndDate != null && s.EndDate <= DateTime.UtcNow));
        }
        
        var subs = await query.ToListAsync();
        return View(subs);
    }
    
    [HttpPost]
    public async Task<IActionResult> ExtendSubscription(int subId, int days)
    {
        var sub = await _context.UserSubscriptions.FindAsync(subId);
        if (sub != null)
        {
            sub.EndDate = (sub.EndDate ?? DateTime.UtcNow).AddDays(days);
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Đã gia hạn {days} ngày";
        }
        return RedirectToAction(nameof(Subscribers));
    }
    
    [HttpPost]
    public async Task<IActionResult> CancelSubscription(int subId)
    {
        var sub = await _context.UserSubscriptions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == subId);
        
        if (sub != null)
        {
            sub.Status = "Cancelled";
            sub.EndDate = DateTime.UtcNow;
            
            // Notify user
            _context.Notifications.Add(new Notification
            {
                UserId = sub.UserId,
                Title = "Subscription đã bị hủy",
                Message = "Subscription Premium của bạn đã bị hủy bởi Admin.",
                Type = "SubscriptionCancelled",
                CreatedAt = DateTime.UtcNow
            });
            
            await _context.SaveChangesAsync();
            TempData["Message"] = "Đã hủy subscription";
        }
        return RedirectToAction(nameof(Subscribers));
    }
}

