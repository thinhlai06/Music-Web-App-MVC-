using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;
using MusicWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace MusicWeb.Controllers;

[Route("account")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
        }

        await _userManager.AddToRoleAsync(user, "User");

        await _signInManager.SignInAsync(user, isPersistent: true);
        return Ok(new { success = true, user = new { user.DisplayName, user.Email } });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, true, lockoutOnFailure: false);
        
        if (result.IsLockedOut)
        {
            return Unauthorized(new { success = false, message = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Admin." });
        }
        
        if (!result.Succeeded)
        {
            return Unauthorized(new { success = false, message = "Sai thông tin đăng nhập" });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        return Ok(new { success = true, user = new { user?.DisplayName, user?.Email } });
    }

    [Authorize]
    [HttpPost("update-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        user.DisplayName = request.DisplayName;
        user.AvatarUrl = request.AvatarUrl;

        if (!string.IsNullOrEmpty(request.CurrentPassword) && !string.IsNullOrEmpty(request.NewPassword))
        {
            var changePasswordResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                return BadRequest(new { success = false, message = changePasswordResult.Errors.First().Description });
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            // Sync with Artist Table
            var artist = await _context.Artists.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (artist != null)
            {
                artist.Name = request.DisplayName;
                artist.AvatarUrl = request.AvatarUrl;
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, user = new { user.DisplayName, user.AvatarUrl, user.Email } });
        }

        return BadRequest(new { success = false, message = "Không thể cập nhật hồ sơ" });
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        return Ok(new { success = true, user = new { user.DisplayName, user.AvatarUrl, user.Email } });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { success = true });
    }
}

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);

