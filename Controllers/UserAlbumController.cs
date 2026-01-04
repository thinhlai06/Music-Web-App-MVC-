using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MusicWeb.Models.Entities;
using MusicWeb.Services;

namespace MusicWeb.Controllers;

[Route("useralbum")]
[ApiController]
[Authorize]
public class UserAlbumController : Controller
{
    private readonly IUserAlbumService _albumService;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAlbumController(IUserAlbumService albumService, UserManager<ApplicationUser> userManager)
    {
        _albumService = albumService;
        _userManager = userManager;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateAlbum([FromForm] CreateUserAlbumRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "Tên album không được để trống" });
        }

        var userId = _userManager.GetUserId(User)!;
        var album = await _albumService.CreateAlbumAsync(
            userId,
            request.Name,
            request.IsPublic,
            request.Description,
            request.CoverFile,
            request.SongIds
        );

        if (album == null)
        {
            return BadRequest(new { success = false, message = "Không thể tạo album" });
        }

        return Ok(new { success = true, albumId = album.Id, message = "Tạo album thành công!" });
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAlbumDetail(int id)
    {
        var userId = _userManager.GetUserId(User);
        var album = await _albumService.GetAlbumByIdAsync(id, userId);

        if (album == null)
        {
            return NotFound();
        }

        return PartialView("~/Views/Home/_UserAlbumDetailSection.cshtml", album);
    }

    [HttpGet("myalbums")]
    public async Task<IActionResult> GetMyAlbums()
    {
        var userId = _userManager.GetUserId(User)!;
        var albums = await _albumService.GetUserAlbumsAsync(userId);

        return Ok(new { success = true, data = albums });
    }

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> UpdateAlbum(int id, [FromForm] UpdateUserAlbumRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "Tên album không được để trống" });
        }

        var userId = _userManager.GetUserId(User)!;
        var result = await _albumService.UpdateAlbumAsync(
            id,
            userId,
            request.Name,
            request.IsPublic,
            request.Description,
            request.CoverFile
        );

        if (!result)
        {
            return BadRequest(new { success = false, message = "Không thể cập nhật album" });
        }

        return Ok(new { success = true, message = "Cập nhật album thành công!" });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var result = await _albumService.DeleteAlbumAsync(id, userId);

        if (!result)
        {
            return BadRequest(new { success = false, message = "Không thể xóa album" });
        }

        return Ok(new { success = true, message = "Đã xóa album" });
    }

    [HttpPost("{albumId:int}/addsong")]
    public async Task<IActionResult> AddSongToAlbum(int albumId, [FromBody] AddSongToAlbumRequest request)
    {
        if (request.SongId <= 0)
        {
            return BadRequest(new { success = false, message = "Bài hát không hợp lệ" });
        }

        var userId = _userManager.GetUserId(User)!;
        var result = await _albumService.AddSongToAlbumAsync(albumId, userId, request.SongId);

        if (!result)
        {
            return BadRequest(new { success = false, message = "Không thể thêm bài hát vào album" });
        }

        return Ok(new { success = true, message = "Đã thêm bài hát vào album" });
    }

    [HttpPost("{albumId:int}/uploadsong")]
    public async Task<IActionResult> UploadSongToAlbum(int albumId, [FromForm] UploadSongToAlbumRequest request)
    {
        if (request.AudioFile == null || string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { success = false, message = "Vui lòng chọn file nhạc và nhập tên bài hát" });
        }

        var userId = _userManager.GetUserId(User)!;
        var result = await _albumService.AddNewSongToAlbumAsync(
            albumId,
            userId,
            request.AudioFile,
            request.CoverFile,
            request.Title,
            request.Description
        );

        if (!result)
        {
            return BadRequest(new { success = false, message = "Không thể upload bài hát" });
        }

        return Ok(new { success = true, message = "Đã thêm bài hát vào album" });
    }

    [HttpPost("{albumId:int}/removesong/{songId:int}")]
    public async Task<IActionResult> RemoveSongFromAlbum(int albumId, int songId, [FromBody] RemoveSongRequest? request)
    {
        var userId = _userManager.GetUserId(User)!;
        bool deleteSongFile = request?.DeleteFile ?? false;

        var result = await _albumService.RemoveSongFromAlbumAsync(albumId, userId, songId, deleteSongFile);

        if (!result)
        {
            return BadRequest(new { success = false, message = "Không thể xóa bài hát khỏi album" });
        }

        return Ok(new { success = true, message = "Đã xóa bài hát khỏi album" });
    }
}

// Request Models
public class CreateUserAlbumRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public IFormFile? CoverFile { get; set; }
    public List<int>? SongIds { get; set; }
}

public class UpdateUserAlbumRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public IFormFile? CoverFile { get; set; }
}

public class AddSongToAlbumRequest
{
    public int SongId { get; set; }
}

public class UploadSongToAlbumRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IFormFile AudioFile { get; set; } = null!;
    public IFormFile? CoverFile { get; set; }
}

public class RemoveSongRequest
{
    public bool DeleteFile { get; set; }
}
