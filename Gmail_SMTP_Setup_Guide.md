# Hướng Dẫn Cấu Hình Gmail SMTP

## Bước 1: Tạo App Password trong Google Account

1. Truy cập: https://myaccount.google.com/
2. Vào **Security** (Bảo mật)
3. Bật **2-Step Verification** (Xác minh 2 bước) nếu chưa bật
4. Sau khi bật 2FA, quay lại **Security**
5. Tìm mục **App passwords** (Mật khẩu ứng dụng)
6. Click **Create** → Chọn **Mail** và **Windows Computer**
7. Click **Generate**
8. Copy mật khẩu 16 ký tự (dạng: xxxx xxxx xxxx xxxx)

## Bước 2: Cấu Hình appsettings.Development.json

Mở file `appsettings.Development.json` và thêm/cập nhật section sau:

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "SenderEmail": "your-email@gmail.com",
    "SenderName": "Music Web App",
    "Username": "your-email@gmail.com",
    "Password": "xxxx xxxx xxxx xxxx"
  }
}
```

**Thay thế:**
- `your-email@gmail.com` → Email Gmail của bạn
- `xxxx xxxx xxxx xxxx` → App Password vừa tạo (giữ nguyên dấu cách)

## Bước 3: Chạy SQL Migration Script

Mở SQL Server Management Studio hoặc sử dụng tool kết nối database, sau đó chạy file:

`Database_Migration_PasswordResetToken.sql`

Script này sẽ tạo bảng `PasswordResetTokens` trong database `MusicWaveDb`.

## Bước 4: Test

1. Chạy app: `dotnet run`
2. Truy cập: http://localhost:5000
3. Click "Đăng nhập" → "Quên mật khẩu?"
4. Nhập email và kiểm tra hộp thư

## Troubleshooting

**Lỗi 535 Authentication failed:**
- Kiểm tra App Password đã đúng chưa (copy/paste chính xác)
- Đảm bảo 2FA đã được bật

**Không nhận được email:**
- Kiểm tra spam folder
- Verify SenderEmail và Username giống nhau
- Kiểm tra logs trong console

**Token hết hạn:**
- Token có hiệu lực 15 phút
- Request lại forgot password để nhận token mới
