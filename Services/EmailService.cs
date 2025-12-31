using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MusicWeb.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string userName)
    {
        try
        {
            var message = new MimeMessage();
            
            // Sender
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"];
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            
            // Recipient
            message.To.Add(new MailboxAddress(userName, toEmail));
            
            // Subject
            message.Subject = "Đặt lại mật khẩu - Music Web App";
            
            // Body - HTML Template
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = GetPasswordResetEmailTemplate(userName, resetLink)
            };
            message.Body = bodyBuilder.ToMessageBody();
            
            // Send via SMTP
            using var client = new SmtpClient();
            
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            
            await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            
            _logger.LogInformation("Password reset email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            throw;
        }
    }

    private string GetPasswordResetEmailTemplate(string userName, string resetLink)
    {
        return $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            margin: 0;
            padding: 20px;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background: #ffffff;
            border-radius: 16px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            padding: 40px 20px;
            text-align: center;
        }}
        .header h1 {{
            color: #ffffff;
            margin: 0;
            font-size: 28px;
            font-weight: 600;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .greeting {{
            font-size: 18px;
            color: #333;
            margin-bottom: 20px;
        }}
        .message {{
            font-size: 15px;
            color: #666;
            line-height: 1.6;
            margin-bottom: 30px;
        }}
        .cta-button {{
            display: inline-block;
            padding: 16px 40px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: #ffffff;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
            transition: transform 0.2s;
        }}
        .cta-button:hover {{
            transform: translateY(-2px);
        }}
        .warning {{
            background: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 12px 16px;
            margin: 20px 0;
            border-radius: 4px;
            font-size: 14px;
            color: #856404;
        }}
        .footer {{
            background: #f8f9fa;
            padding: 20px;
            text-align: center;
            font-size: 13px;
            color: #6c757d;
            border-top: 1px solid #dee2e6;
        }}
        .link-text {{
            color: #667eea;
            word-break: break-all;
            font-size: 12px;
            margin-top: 15px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎵 Music Web App</h1>
        </div>
        <div class='content'>
            <div class='greeting'>
                Xin chào <strong>{userName}</strong>,
            </div>
            <div class='message'>
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. 
                Nhấn vào nút bên dưới để tạo mật khẩu mới:
            </div>
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{resetLink}' class='cta-button'>Đặt lại mật khẩu</a>
            </div>
            <div class='warning'>
                ⏰ <strong>Lưu ý:</strong> Link này sẽ hết hạn sau <strong>15 phút</strong>.
            </div>
            <div class='message'>
                Nếu nút không hoạt động, bạn có thể sao chép link sau vào trình duyệt:
                <div class='link-text'>{resetLink}</div>
            </div>
            <div class='message' style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6;'>
                Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. 
                Tài khoản của bạn vẫn an toàn.
            </div>
        </div>
        <div class='footer'>
            © 2025 Music Web App. All rights reserved.
            <br>
            Email tự động, vui lòng không trả lời.
        </div>
    </div>
</body>
</html>
        ";
    }
}
