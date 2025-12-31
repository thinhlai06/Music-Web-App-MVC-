using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;
using MusicWeb.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MusicWeb.Services;
using Microsoft.Extensions.Caching.Memory;

namespace MusicWeb.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        IEmailService emailService,
        IMemoryCache cache)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _emailService = emailService;
        _cache = cache;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            return Json(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            Response.StatusCode = 400;
            return Json(new { success = false, errors = result.Errors.Select(e => e.Description) });
        }

        await _userManager.AddToRoleAsync(user, "User");

        await _signInManager.SignInAsync(user, isPersistent: true);
        return Json(new { success = true, user = new { user.DisplayName, user.Email } });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, true, lockoutOnFailure: false);
        
        if (result.IsLockedOut)
        {
            Response.StatusCode = 401;
            return Json(new { success = false, message = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Admin." });
        }
        
        if (!result.Succeeded)
        {
            Response.StatusCode = 401;
            return Json(new { success = false, message = "Sai thông tin đăng nhập" });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        return Json(new { success = true, user = new { user?.DisplayName, user?.Email } });
    }

    [Authorize]
    [HttpPost("update-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) {
            Response.StatusCode = 401;
            return Json(new { success = false });
        }

        user.DisplayName = request.DisplayName;
        user.AvatarUrl = request.AvatarUrl;

        if (!string.IsNullOrEmpty(request.CurrentPassword) && !string.IsNullOrEmpty(request.NewPassword))
        {
            var changePasswordResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = changePasswordResult.Errors.First().Description });
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

            return Json(new { success = true, user = new { user.DisplayName, user.AvatarUrl, user.Email } });
        }

        Response.StatusCode = 400;
        return Json(new { success = false, message = "Không thể cập nhật hồ sơ" });
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) {
            Response.StatusCode = 401;
            return Json(new { success = false });
        }

        return Json(new { success = true, user = new { user.DisplayName, user.AvatarUrl, user.Email } });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Json(new { success = true });
    }

    [HttpGet("external-login")]
    public IActionResult ExternalLogin(string provider, string returnUrl = "/")
    {
        var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("external-login-callback")]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/")
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return Redirect($"/?error=external-login-failed");
        }

        // Try to sign in with external login provider
        var result = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (result.Succeeded)
        {
            return Redirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            return Redirect($"/?error=account-locked");
        }

        // User doesn't have an account yet, create one
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            return Redirect($"/?error=email-not-provided");
        }

        // Check if user already exists with this email
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            // Link the external login to existing user
            var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
            if (addLoginResult.Succeeded)
            {
                await _signInManager.SignInAsync(existingUser, isPersistent: true);
                return Redirect(returnUrl);
            }
            return Redirect($"/?error=could-not-link-account");
        }

        // Create new user
        var name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = name,
            EmailConfirmed = true,  // OAuth providers verify email
            Provider = info.LoginProvider
        };

        var createResult = await _userManager.CreateAsync(user);
        if (createResult.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "User");
            await _userManager.AddLoginAsync(user, info);
            await _signInManager.SignInAsync(user, isPersistent: true);
            return Redirect(returnUrl);
        }

        return Redirect($"/?error=registration-failed");
    }

    // ============ FORGOT PASSWORD ENDPOINTS ============
    
    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword()
    {
        return View();
    }
    
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            return Json(new { success = false, message = "Email không hợp lệ" });
        }

        // Rate limiting - 3 requests per 15 minutes per email
        var cacheKey = $"forgot_password_{model.Email}";
        if (_cache.TryGetValue(cacheKey, out int requestCount))
        {
            if (requestCount >= 3)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Quá nhiều yêu cầu. Vui lòng thử lại sau 15 phút." });
            }
            _cache.Set(cacheKey, requestCount + 1, TimeSpan.FromMinutes(15));
        }
        else
        {
            _cache.Set(cacheKey, 1, TimeSpan.FromMinutes(15));
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        
        // Silent failure for security - don't reveal if email exists
        if (user == null)
        {
            return Json(new { success = true, message = "Nếu email tồn tại, bạn sẽ nhận được link đặt lại mật khẩu." });
        }

        // Check if user registered via OAuth (no password)
        if (!string.IsNullOrEmpty(user.Provider))
        {
            Response.StatusCode = 400;
            return Json(new { 
                success = false, 
                message = $"Tài khoản này đăng nhập qua {user.Provider}. Bạn không cần đặt lại mật khẩu." 
            });
        }

        // Create reset token
        var token = Guid.NewGuid().ToString();
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false
        };

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync();

        // Send email
        var resetLink = $"{Request.Scheme}://{Request.Host}/account/reset-password?token={token}&email={Uri.EscapeDataString(model.Email)}";
        
        try
        {
            await _emailService.SendPasswordResetEmailAsync(user.Email!, resetLink, user.DisplayName ?? "User");
        }
        catch (Exception ex)
        {
            // Log error but don't expose to user
            Console.WriteLine($"Failed to send email: {ex.Message}");
            Response.StatusCode = 500;
            return Json(new { success = false, message = "Không thể gửi email. Vui lòng thử lại sau." });
        }

        return Json(new { success = true, message = "Email đặt lại mật khẩu đã được gửi." });
    }

    [HttpGet("reset-password")]
    public async Task<IActionResult> ResetPassword([FromQuery] string token, [FromQuery] string email)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
        {
            return Redirect("/?error=invalid-reset-link");
        }

        var resetToken = await _context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.User!.Email == email);

        if (resetToken == null || resetToken.IsUsed || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return Redirect("/?error=invalid-or-expired-token");
        }

        return View(new ResetPasswordViewModel { Token = token, Email = email });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPasswordPost([FromBody] ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
        }

        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == model.Token && t.User!.Email == model.Email);

        if (resetToken == null || resetToken.IsUsed || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            Response.StatusCode = 400;
            return Json(new { success = false, message = "Token không hợp lệ hoặc đã hết hạn" });
        }

        var user = resetToken.User;
        if (user == null)
        {
            Response.StatusCode = 400;
            return Json(new { success = false, message = "Người dùng không tồn tại" });
        }

        // Remove old password and set new one
        var removePasswordResult = await _userManager.RemovePasswordAsync(user);
        if (!removePasswordResult.Succeeded)
        {
            Response.StatusCode = 400;
            return Json(new { success = false, message = "Không thể đặt lại mật khẩu" });
        }

        var addPasswordResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
        if (!addPasswordResult.Succeeded)
        {
            Response.StatusCode = 400;
            return Json(new { 
                success = false, 
                message = addPasswordResult.Errors.FirstOrDefault()?.Description ?? "Không thể đặt lại mật khẩu" 
            });
        }

        // Mark token as used
        resetToken.IsUsed = true;
        await _context.SaveChangesAsync();

        // Sign out all sessions for security
        await _userManager.UpdateSecurityStampAsync(user);

        return Json(new { success = true, message = "Mật khẩu đã được đặt lại thành công" });
    }

    [HttpGet("validate-reset-token")]
    public async Task<IActionResult> ValidateResetToken([FromQuery] string token, [FromQuery] string email)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
        {
            return Json(new { valid = false });
        }

        var resetToken = await _context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.User!.Email == email);

        var isValid = resetToken != null && !resetToken.IsUsed && resetToken.ExpiresAt >= DateTime.UtcNow;

        return Json(new { valid = isValid });
    }
}

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);

