using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;
using MusicWeb.Services;

namespace MusicWeb.Controllers;

[Route("playlists")]
[ApiController]
[Authorize]
public class PlaylistController : Controller
{
    private readonly IMusicService _musicService;
    private readonly IAIPlaylistService _aiPlaylistService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlaylistController(
        IMusicService musicService, 
        IAIPlaylistService aiPlaylistService,
        UserManager<ApplicationUser> userManager)
    {
        _musicService = musicService;
        _aiPlaylistService = aiPlaylistService;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlaylist(CreatePlaylistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "Tên playlist không được để trống" });
        }

        var userId = _userManager.GetUserId(User)!;
        var playlist = await _musicService.CreatePlaylistAsync(request.Name.Trim(), userId);
        return Ok(new { success = true, playlist });
    }

    [HttpPost("{playlistId:int}/songs")]
    public async Task<IActionResult> AddSong(int playlistId, AddSongRequest request)
    {
        if (request.SongId <= 0)
        {
            return BadRequest(new { success = false, message = "Bài hát không hợp lệ" });
        }

        var userId = _userManager.GetUserId(User)!;
        var added = await _musicService.AddSongToPlaylistAsync(playlistId, request.SongId, userId);
        if (!added)
        {
            return BadRequest(new { success = false, message = "Bài hát đã có trong playlist hoặc lỗi quyền truy cập" });
        }

        return Ok(new { success = true });
    }

    [HttpDelete("{playlistId:int}/songs/{songId:int}")]
    public async Task<IActionResult> RemoveSong(int playlistId, int songId)
    {
        var userId = _userManager.GetUserId(User)!;
        var removed = await _musicService.RemoveSongFromPlaylistAsync(playlistId, songId, userId);
        if (!removed)
        {
            return Forbid();
        }

        return Ok(new { success = true });
    }

    [HttpDelete("{playlistId:int}")]
    public async Task<IActionResult> DeletePlaylist(int playlistId)
    {
        var userId = _userManager.GetUserId(User)!;
        var deleted = await _musicService.DeletePlaylistAsync(playlistId, userId);
        if (!deleted)
        {
            return Forbid();
        }

        return Ok(new { success = true });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(int id)
    {
        var userId = _userManager.GetUserId(User);
        var playlist = await _musicService.GetPlaylistDetailAsync(id, userId);
        if (playlist == null) return NotFound();
        return PartialView("~/Views/Home/_PlaylistDetailSection.cshtml", playlist);
    }

    [HttpPost("{id}/update")]
    public async Task<IActionResult> Update(int id, [FromForm] UpdatePlaylistRequest request)
    {
        var userId = _userManager.GetUserId(User)!;
        string? coverUrl = request.CoverUrlInput;

        if (request.CoverFile != null)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "playlists");
            Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + request.CoverFile.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await request.CoverFile.CopyToAsync(fileStream);
            }
            coverUrl = "/uploads/playlists/" + uniqueFileName;
        }

        var result = await _musicService.UpdatePlaylistAsync(id, request.Name, coverUrl, request.IsPublic, userId);
        if (!result) return Forbid();

        return Ok(new { success = true, coverUrl });
    }

    // ========== AI PLAYLIST ENDPOINTS ==========

    /// <summary>
    /// Preview AI-generated playlist based on natural language prompt
    /// </summary>
    [HttpPost("ai/preview")]
    public async Task<IActionResult> AIPreview([FromBody] SmartPlaylistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { success = false, error = "Vui lòng nhập mô tả playlist" });
        }

        var userId = _userManager.GetUserId(User)!;
        var result = await _aiPlaylistService.GeneratePreviewAsync(request.Prompt, userId);
        
        return Ok(result);
    }

    /// <summary>
    /// Create actual playlist from AI preview selection
    /// </summary>
    [HttpPost("ai/create")]
    public async Task<IActionResult> AICreate([FromBody] CreateAIPlaylistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlaylistName))
        {
            return BadRequest(new { success = false, message = "Tên playlist không được để trống" });
        }

        if (request.SongIds == null || request.SongIds.Count == 0)
        {
            return BadRequest(new { success = false, message = "Vui lòng chọn ít nhất một bài hát" });
        }

        var userId = _userManager.GetUserId(User)!;
        var playlist = await _aiPlaylistService.CreatePlaylistFromPreviewAsync(
            request.PlaylistName, 
            request.SongIds, 
            userId);

        if (playlist == null)
        {
            return BadRequest(new { success = false, message = "Không thể tạo playlist" });
        }

        // Return same format as CreatePlaylist for frontend compatibility
        return Ok(new { 
            success = true, 
            playlist = new {
                id = playlist.Id,
                title = playlist.Name,
                subtitle = $"{request.SongIds.Count} bài hát",
                coverUrl = playlist.CoverUrl
            }
        });
    }
}

public class UpdatePlaylistRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string? CoverUrlInput { get; set; }
    public IFormFile? CoverFile { get; set; }
}

public record CreatePlaylistRequest(string Name);
public record AddSongRequest(int SongId);
