# 📚 Phân Tích Project MusicWeb - Từ A đến Z

> **Mục tiêu**: Giúp bạn hiểu rõ **TẠI SAO** code được viết như vậy, **Ở ĐÂU** là MVC vs SPA, và **TỪNG CHỨC NĂNG** hoạt động như thế nào.

---

# PHẦN 1: MVC vs SPA - Ở ĐÂU TRONG CODE?

## 1.1 MVC Là Gì? SPA Là Gì?

| Khái niệm | Ý nghĩa | Ví dụ |
|-----------|---------|-------|
| **MVC** | Server render HTML, gửi về client. Mỗi lần click = reload trang | Click link → Server xử lý → Trả về HTML mới |
| **SPA** | Client (JavaScript) tự cập nhật giao diện, không reload trang | Click button → JS thay đổi DOM → Trang không tải lại |

**Project này: HYBRID = Kết hợp cả hai.**

---

## 1.2 MVC Ở Đâu Trong Code?

### 📍 File: `Controllers/HomeController.cs`

```csharp
// ĐÂY LÀ MVC: Server render HTML rồi trả về
public async Task<IActionResult> Index()
{
    var userId = _userManager.GetUserId(User);
    var model = await _musicService.BuildHomeAsync(userId);  // Query DB
    model.IsAdmin = User.IsInRole("Admin");
    return View(model);  // <-- TRẢ VỀ HTML (Views/Home/Index.cshtml)
}
```

**Giải thích:**
- User truy cập `http://localhost/` → Request đến `HomeController.Index()`
- Server query database, lấy dữ liệu bài hát, playlist...
- `return View(model)` → Razor engine render file `Index.cshtml` thành HTML
- HTML được gửi về browser → **ĐÂY LÀ MVC!**

### 📍 File: `Views/Home/Index.cshtml`

```html
@model MusicWeb.Models.ViewModels.HomeViewModel  <!-- Nhận data từ Controller -->

<!-- Include các section khác (Server render tất cả cùng lúc) -->
<partial name="_Sidebar" model="Model" />
<partial name="_HomeSection" model="Model" />
<partial name="_LibrarySection" model="Model" />
<partial name="_ProfileSection" model="Model" />
<!-- ... -->

<!-- Đây là "cầu nối" MVC → SPA: Chuyển data C# sang JavaScript -->
@section Scripts {
<script>
    window.musicModel = @Html.Raw(JsonSerializer.Serialize(Model));  // <-- DATA INJECTION
    window.isAuthenticated = @(isAuthenticated.ToString().ToLower());
</script>
}
```

**Điểm quan trọng:**
1. **Server render TẤT CẢ sections** (`_HomeSection`, `_LibrarySection`...) ngay từ đầu
2. **Inject data vào `window.musicModel`** để JavaScript có thể sử dụng
3. Sau bước này, JavaScript (SPA) sẽ "tiếp quản" giao diện

---

## 1.3 SPA Ở Đâu Trong Code?

### 📍 File: `wwwroot/js/site.js` - Hàm switchView()

```javascript
// ĐÂY LÀ SPA: JavaScript thay đổi giao diện KHÔNG reload trang
function switchView(viewId) {
    // 1. Ẩn TẤT CẢ các view
    ['home-view', 'library-view', 'profile-view', 'upload-section', 
     'stats-view', 'premium-view', 'notification-view'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.classList.add('hidden');
    });
    
    // 2. Ẩn các container động
    document.querySelectorAll('.dynamic-view-container').forEach(c => {
        c.classList.add('hidden');
    });
    
    // 3. Hiện view được chọn
    const targetView = document.getElementById(viewId);
    if (targetView) {
        targetView.classList.remove('hidden');
    }
}

// Expose ra global để HTML onclick có thể gọi
window.switchView = switchView;
```

**Tại sao đây là SPA?**
- Khi user click "Thư viện" → `switchView('library-view')`
- JavaScript ẩn `home-view`, hiện `library-view`
- **Trang KHÔNG reload** → Player vẫn chạy → **ĐÂY LÀ SPA!**

### 📍 Ví dụ: Click Sidebar chuyển view

```html
<!-- File: Views/Home/_Sidebar.cshtml -->
<li onclick="switchView('home-view')" class="nav-item active">
    <i class="fa-solid fa-house"></i> Trang chủ
</li>
<li onclick="switchView('library-view')" class="nav-item">
    <i class="fa-solid fa-headphones"></i> Thư viện
</li>
<li onclick="switchView('profile-view')" class="nav-item">
    <i class="fa-solid fa-user"></i> Cá nhân
</li>
```

**Flow:**
```
Click "Thư viện" 
    → onclick="switchView('library-view')" 
    → JS ẩn home, hiện library 
    → Trang KHÔNG tải lại
```

---

## 1.4 Hybrid: Khi Nào MVC, Khi Nào SPA?

| Tình huống | Dùng MVC hay SPA? | Code ở đâu |
|------------|-------------------|------------|
| Lần đầu truy cập `/` | **MVC** - Server render HTML | `HomeController.Index()` |
| Chuyển tab Home/Library/Profile | **SPA** - JS thay đổi DOM | `switchView()` trong site.js |
| Xem chi tiết Album | **Hybrid** - Fetch PartialView rồi inject | `fetch('/album/5')` → inject HTML |
| Toggle Like bài hát | **SPA** - JS gọi API rồi update icon | `toggleFavorite()` trong site.js |
| Tìm kiếm | **SPA** - JS gọi API rồi render kết quả | `performSearch()` trong site.js |

---

# PHẦN 2: CẤU TRÚC THƯ MỤC - TẠI SAO CẦN?

## 2.1 Tổng Quan

```
MusicWeb/
├── Controllers/     ← Bắt buộc: Xử lý HTTP
├── Services/        ← Bắt buộc: Logic nghiệp vụ  
├── Models/          ← Bắt buộc: Data structures
├── Views/           ← Bắt buộc: Giao diện HTML
├── wwwroot/         ← Bắt buộc: File tĩnh (JS, CSS)
├── Data/            ← Bắt buộc: Database context
├── Migrations/      ← Có thể xóa nếu rebuild DB
└── Program.cs       ← Bắt buộc: Entry point
```

---

## 2.2 Controllers/ - Bỏ được không? ❌ KHÔNG

### Vai trò
Nhận HTTP request → Gọi Service xử lý → Trả về response (HTML hoặc JSON)

### Code ví dụ: `HomeController.cs`

```csharp
// API trả về JSON (cho SPA)
[HttpGet("/search")]
public async Task<IActionResult> Search(string term)
{
    var results = await _musicService.SearchAsync(term, userId);
    return Json(new { success = true, data = results });  // <-- JSON cho JS fetch
}

// API trả về PartialView HTML (Hybrid)
[HttpGet("/album/{id:int}")]
public async Task<IActionResult> GetAlbum(int id)
{
    var album = await _musicService.GetAlbumDetailAsync(id, userId);
    return PartialView("_AlbumDetailSection", album);  // <-- HTML cho JS inject
}
```

**Nếu xóa Controllers?**
- ❌ Không có URL nào hoạt động
- ❌ App không khởi động được

---

## 2.3 Services/ - Bỏ được không? ⚠️ CÓ THỂ, NHƯNG KHÔNG NÊN

### Vai trò
Chứa **business logic** (logic nghiệp vụ) độc lập với HTTP.

### Tại sao tách riêng?

```csharp
// ❌ KHÔNG NÊN: Logic trong Controller
public class BadController : Controller
{
    public async Task<IActionResult> GetHome()
    {
        // Query database trực tiếp trong controller - KHÔNG TỐT
        var songs = await _context.Songs
            .Include(s => s.Artist)
            .OrderByDescending(s => s.ReleaseDate)
            .Take(8)
            .ToListAsync();
        // ... 100 dòng code nữa
    }
}

// ✅ NÊN: Logic trong Service
public class GoodController : Controller
{
    public async Task<IActionResult> GetHome()
    {
        var model = await _musicService.BuildHomeAsync(userId);  // Gọn gàng
        return View(model);
    }
}
```

**Lợi ích:**
- **Dễ test**: Test service không cần HTTP
- **Tái sử dụng**: Nhiều controller dùng chung service
- **Dễ đọc**: Controller ngắn, dễ hiểu

---

## 2.4 Models/Entities/ - Bỏ được không? ❌ KHÔNG

### Vai trò
Định nghĩa cấu trúc bảng database.

### Code ví dụ: `Song.cs`

```csharp
public class Song
{
    public int Id { get; set; }                    // Primary Key
    public string Title { get; set; }              // Tên bài
    public string? AudioUrl { get; set; }          // Link MP3 trên R2
    public string? CoverUrl { get; set; }          // Link ảnh bìa
    public TimeSpan Duration { get; set; }         // Thời lượng
    public int ViewCount { get; set; }             // Lượt nghe
    public bool IsPremium { get; set; }            // Bài Premium?
    
    public int ArtistId { get; set; }              // Foreign Key
    public Artist Artist { get; set; } = null!;    // Navigation property
}
```

**Entity Framework Core sẽ:**
1. Đọc class này
2. Tạo bảng `Songs` trong SQL Server
3. Map các property thành cột

**Nếu xóa?**
- ❌ Database không biết cấu trúc bảng
- ❌ EF Core không hoạt động

---

## 2.5 Models/ViewModels/ - Bỏ được không? ⚠️ CÓ THỂ

### Vai trò
Chứa **data structure riêng cho View**, không phải database entity.

### Tại sao cần?

```csharp
// Entity (DB) - có thể chứa thông tin nhạy cảm
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; }
    public string PasswordHash { get; set; }  // KHÔNG ĐƯỢC GỬI VỀ CLIENT!
}

// ViewModel (View) - chỉ chứa data cần hiển thị
public record UserProfileViewModel(
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string UserId,
    int FollowersCount = 0,
    int FollowingCount = 0
);
// Không có PasswordHash → An toàn
```

**Nếu xóa?**
- Có thể dùng Entity trực tiếp, nhưng:
- ⚠️ Dễ vô tình expose data nhạy cảm
- ⚠️ View phụ thuộc cấu trúc DB

---

## 2.6 Views/ - Bỏ được không? ❌ KHÔNG (cho MVC)

### Vai trò
Chứa template HTML (Razor) để render giao diện.

### Cấu trúc quan trọng:

```
Views/
├── Home/
│   ├── Index.cshtml          ← Layout chính (1 file duy nhất được render)
│   ├── _HomeSection.cshtml   ← Partial: Trang chủ
│   ├── _LibrarySection.cshtml ← Partial: Thư viện
│   ├── _ProfileSection.cshtml ← Partial: Profile
│   ├── _PlayerBar.cshtml      ← Partial: Player (quan trọng nhất)
│   └── _Modals.cshtml         ← Partial: Tất cả modal
├── Shared/
│   ├── _Layout.cshtml         ← Layout chung (head, body wrapper)
│   └── _ViewImports.cshtml    ← Import chung cho tất cả views
```

**Nếu xóa?**
- ❌ Controller không biết render HTML thế nào
- ❌ Lỗi 500: "View not found"

---

## 2.7 wwwroot/ - Bỏ được không? ❌ KHÔNG

### Vai trò
Chứa **static files** (file tĩnh) phục vụ trực tiếp cho browser.

### Cấu trúc:

```
wwwroot/
├── js/
│   └── site.js      ← TẤT CẢ JavaScript (2800+ dòng!)
├── css/
│   └── site.css     ← Styles
├── lib/             ← Bootstrap, FontAwesome, jQuery
└── ads/             ← File quảng cáo MP3
```

### Tại sao file JS không đặt ở folder khác?

```csharp
// Program.cs
app.UseStaticFiles();  // <-- Chỉ serve file từ wwwroot/
```

**Nếu xóa?**
- ❌ Không có CSS → Trang xấu
- ❌ Không có JS → SPA không hoạt động, Player chết

---

## 2.8 Data/ - Bỏ được không? ❌ KHÔNG

### Vai trò
Chứa `ApplicationDbContext` - cầu nối giữa C# code và SQL Server.

### Code: `ApplicationDbContext.cs`

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    // Mỗi DbSet = 1 bảng trong DB
    public DbSet<Song> Songs { get; set; }
    public DbSet<Artist> Artists { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<PlayHistory> PlayHistories { get; set; }
    // ...
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Cấu hình quan hệ, index, seed data...
    }
}
```

**Nếu xóa?**
- ❌ Không thể query database
- ❌ EF Core không hoạt động

---

# PHẦN 3: PHÂN TÍCH TỪNG CHỨC NĂNG

## 3.1 Chức Năng: Nghe Nhạc (Player)

### Thành phần chính

| File | Vai trò |
|------|---------|
| `Views/Home/_PlayerBar.cshtml` | HTML của player |
| `wwwroot/js/site.js` | Logic điều khiển |
| `Controllers/HomeController.cs` | API ghi lịch sử |

### Code HTML: `_PlayerBar.cshtml`

```html
<footer class="player-bar">
    <!-- Phần trái: Thông tin bài hát -->
    <div class="player-left">
        <img src="..." id="player-img" alt="Bìa">  <!-- Ảnh bìa -->
        <div class="song-info">
            <h4 id="player-title">Chọn bài hát</h4>  <!-- Tên bài -->
            <p id="player-artist">MusicWave</p>      <!-- Nghệ sĩ -->
        </div>
        <button id="player-favorite">♡</button>      <!-- Nút like -->
    </div>
    
    <!-- Phần giữa: Controls -->
    <div class="player-center">
        <div class="player-controls">
            <button id="btn-shuffle">🔀</button>    <!-- Shuffle -->
            <button id="btn-prev">⏮️</button>       <!-- Previous -->
            <button id="btn-play">▶️</button>       <!-- Play/Pause -->
            <button id="btn-next">⏭️</button>       <!-- Next -->
            <button id="btn-repeat">🔁</button>     <!-- Repeat -->
        </div>
        <div class="progress-container">
            <span id="player-current">0:00</span>   <!-- Thời gian hiện tại -->
            <div class="progress-bar" id="player-progress">
                <div id="player-progress-fill"></div>
            </div>
            <span id="player-duration">0:00</span>  <!-- Tổng thời lượng -->
        </div>
    </div>
    
    <!-- Phần phải: Volume, Lyrics -->
    <div class="player-right">
        <button onclick="toggleLyrics()">🎤</button>
        <div id="volume-bar">...</div>
    </div>
    
    <!-- ⭐ QUAN TRỌNG NHẤT: Thẻ audio HTML5 -->
    <audio id="audio-player" preload="metadata"></audio>
</footer>
```

### Code JavaScript: `site.js` - Hàm playSong()

```javascript
function playSong(song) {
    // 1. Kiểm tra nếu đang phát quảng cáo → bỏ qua
    if (state.isPlayingAd) return;
    
    // 2. Lấy URL audio (có thể là 'audio' hoặc 'audioUrl')
    const audioSrc = song.audio || song.audioUrl;
    if (!audioSrc) {
        showToast('Bản thu âm chưa sẵn sàng.');
        return;
    }
    
    // 3. Kiểm tra Premium
    if (song.isPremium && !state.isPremiumUser) {
        showPremiumRequired();  // Hiện thông báo yêu cầu nâng cấp
        return;
    }
    
    // 4. Kiểm tra quảng cáo (free user)
    if (shouldPlayAd()) {
        state.pendingSongAfterAd = song;  // Lưu lại để phát sau
        playAd();
        return;
    }
    
    // 5. Cập nhật state và UI
    state.currentSong = song;
    els.audio.src = audioSrc;                    // Gán nguồn audio
    els.audio.playbackRate = state.playbackSpeed;
    
    // 6. PHÁT NHẠC!
    els.audio.play().then(() => {
        state.isPlaying = true;
        els.playIcon.classList.remove('fa-circle-play');
        els.playIcon.classList.add('fa-circle-pause');
        updatePlayerUI();        // Cập nhật ảnh bìa, tên bài
        recordPlay(song.id);     // Gọi API ghi lịch sử
        
        // Ghi doanh thu nếu là Premium song
        if (song.isPremium) {
            recordPremiumPlay(song.id);
        }
        
        state.songsPlayedSinceAd++;  // Đếm để hiện quảng cáo
    });
}
```

### API ghi lịch sử: `HomeController.cs`

```csharp
[HttpPost("/player/play/{songId:int}")]
public async Task<IActionResult> RecordPlay(int songId)
{
    var userId = _userManager.GetUserId(User);
    await _musicService.RecordPlayAsync(songId, userId);
    return Json(new { success = true });
}
```

### Logic ghi lịch sử: `MusicService.cs`

```csharp
public async Task RecordPlayAsync(int songId, string? userId)
{
    // 1. Tăng ViewCount (cho cả guest)
    var song = await _context.Songs.FindAsync(songId);
    if (song != null)
    {
        song.ViewCount++;
    }
    
    // 2. Ghi PlayHistory (chỉ khi đăng nhập)
    if (!string.IsNullOrEmpty(userId))
    {
        _context.PlayHistories.Add(new PlayHistory
        {
            SongId = songId,
            UserId = userId,
            PlayedAt = DateTime.UtcNow
        });
    }
    
    await _context.SaveChangesAsync();
}
```

---

## 3.2 Chức Năng: Shuffle (Trộn bài)

### Thuật toán Fisher-Yates Shuffle

```javascript
// site.js - Hàm shuffleArray()
function shuffleArray(array) {
    const shuffled = [...array];  // Copy array
    
    // Fisher-Yates: Đảo từ cuối lên
    for (let i = shuffled.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));  // Random từ 0 đến i
        [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];  // Swap
    }
    
    return shuffled;
}
```

### Logic bật/tắt Shuffle

```javascript
function toggleShuffle() {
    state.isShuffled = !state.isShuffled;
    
    if (state.isShuffled && state.queue) {
        // BẬT: Lưu queue gốc, trộn queue mới
        state.originalQueue = [...state.queue];
        
        const currentSong = state.queue[state.queueIndex];
        state.queue = shuffleArray(state.queue);
        
        // Tìm vị trí bài đang phát trong queue mới
        const newIndex = state.queue.findIndex(s => s.id === currentSong.id);
        state.queueIndex = newIndex;
        
    } else if (!state.isShuffled && state.originalQueue) {
        // TẮT: Khôi phục queue gốc
        const currentSong = state.queue[state.queueIndex];
        state.queue = [...state.originalQueue];
        state.queueIndex = state.queue.findIndex(s => s.id === currentSong.id);
        state.originalQueue = null;
    }
    
    // Cập nhật UI
    document.getElementById('btn-shuffle').classList.toggle('active', state.isShuffled);
}
```

---

## 3.3 Chức Năng: Loop (Lặp lại)

### State Machine 3 trạng thái

```javascript
function toggleLoop() {
    // Chuyển state: off → all → one → off
    if (state.loopMode === 'off') {
        state.loopMode = 'all';
    } else if (state.loopMode === 'all') {
        state.loopMode = 'one';
    } else {
        state.loopMode = 'off';
    }
    
    updateRepeatButton();  // Đổi icon
}

function updateRepeatButton() {
    const btn = document.getElementById('btn-repeat');
    btn.classList.remove('active', 'one');
    
    if (state.loopMode === 'all') {
        btn.classList.add('active');      // Màu highlight
        btn.innerHTML = '<i class="fa-solid fa-repeat"></i>';
    } else if (state.loopMode === 'one') {
        btn.classList.add('active', 'one');
        btn.innerHTML = '<i class="fa-solid fa-repeat"></i><span class="repeat-one-badge">1</span>';
    } else {
        btn.innerHTML = '<i class="fa-solid fa-repeat"></i>';
    }
}
```

### Xử lý khi bài hát kết thúc

```javascript
// Event listener cho audio ended
els.audio.addEventListener('ended', () => {
    if (state.isPlayingAd) {
        onAdEnded();  // Quảng cáo kết thúc
        return;
    }
    
    // Loop One: Phát lại bài hiện tại
    if (state.loopMode === 'one') {
        els.audio.currentTime = 0;
        els.audio.play();
        return;
    }
    
    // Chuyển bài tiếp theo
    playNext();
});

function playNext() {
    if (!state.queue || state.queue.length === 0) return;
    
    state.queueIndex++;
    
    // Hết queue
    if (state.queueIndex >= state.queue.length) {
        if (state.loopMode === 'all') {
            state.queueIndex = 0;  // Loop All: Quay về đầu
        } else {
            state.queueIndex = state.queue.length - 1;  // Dừng ở cuối
            return;
        }
    }
    
    playSong(state.queue[state.queueIndex]);
}
```

---

## 3.4 Chức Năng: Toggle Favorite (Like/Unlike)

### JavaScript: Optimistic UI Update

```javascript
function toggleFavorite(songId, sourceBtn) {
    // 1. Kiểm tra đăng nhập
    if (!state.isAuthenticated) {
        toggleAuthModal(true);  // Hiện modal đăng nhập
        return;
    }
    
    // 2. Gọi API
    fetch(`/favorites/${songId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' }
    })
    .then(res => res.json())
    .then(data => {
        if (!data.success) return;
        const isFavorite = data.isFavorite;
        
        // 3. Cập nhật TẤT CẢ card cùng songId (vì có thể xuất hiện nhiều nơi)
        document.querySelectorAll(`.song-card[data-song-id="${songId}"]`).forEach(card => {
            card.dataset.favorite = isFavorite.toString();
            const btn = card.querySelector('.favorite-toggle');
            if (btn) btn.classList.toggle('active', isFavorite);
        });
        
        // 4. Cập nhật player nếu đang phát bài này
        if (state.currentSong?.id === songId) {
            state.currentSong.isFavorite = isFavorite;
            updatePlayerUI();
        }
        
        // 5. Hiện thông báo
        showToast(isFavorite ? 'Đã thêm vào yêu thích' : 'Đã gỡ khỏi yêu thích');
    });
}
```

### API Backend: `HomeController.cs`

```csharp
[Authorize]  // Yêu cầu đăng nhập
[HttpPost("/favorites/{songId:int}")]
public async Task<IActionResult> ToggleFavorite(int songId)
{
    var userId = _userManager.GetUserId(User)!;
    var added = await _musicService.ToggleFavoriteAsync(songId, userId);
    return Json(new { success = true, isFavorite = added });
}
```

### Logic nghiệp vụ: `MusicService.cs`

```csharp
public async Task<bool> ToggleFavoriteAsync(int songId, string userId)
{
    // Kiểm tra đã like chưa
    var favorite = await _context.FavoriteSongs
        .FirstOrDefaultAsync(f => f.SongId == songId && f.UserId == userId);
    
    if (favorite is null)
    {
        // Chưa like → Thêm
        _context.FavoriteSongs.Add(new FavoriteSong { SongId = songId, UserId = userId });
        await _context.SaveChangesAsync();
        return true;  // Đã thêm
    }
    
    // Đã like → Xóa
    _context.FavoriteSongs.Remove(favorite);
    await _context.SaveChangesAsync();
    return false;  // Đã gỡ
}
```

---

## 3.5 Chức Năng: Hiển Thị Lời Bài Hát (Lyrics)

### JavaScript: Load và Sync Lyrics

```javascript
function toggleLyrics() {
    if (!state.currentSong) {
        showToast('Hãy phát bài hát để xem lời.');
        return;
    }
    
    els.lyricsOverlay.classList.toggle('open');
    
    if (els.lyricsOverlay.classList.contains('open')) {
        loadLyrics(state.currentSong.id);
        els.audio.addEventListener('timeupdate', syncLyrics);  // Sync theo thời gian
    } else {
        els.audio.removeEventListener('timeupdate', syncLyrics);
    }
}

function loadLyrics(songId) {
    fetch(`/lyrics/${songId}`)
        .then(res => res.json())
        .then(data => {
            const lyrics = data.data.lyrics || [];
            
            if (lyrics.length === 0) {
                els.lyricsContent.innerHTML = '<p>Chưa có lời bài hát.</p>';
                return;
            }
            
            // Render từng dòng lời
            lyrics.forEach((line) => {
                const p = document.createElement('p');
                p.className = 'lyric-line';
                p.textContent = line.text || line;
                p.dataset.time = line.time || 0;  // Timestamp
                
                // Click để seek đến thời điểm đó
                p.addEventListener('click', () => {
                    els.audio.currentTime = line.time;
                    els.audio.play();
                });
                
                els.lyricsContent.appendChild(p);
            });
        });
}

function syncLyrics() {
    const currentTime = els.audio.currentTime;
    const lines = document.querySelectorAll('.lyric-line');
    
    // Tìm dòng có timestamp <= currentTime gần nhất
    let activeIndex = -1;
    for (let i = 0; i < lines.length; i++) {
        const lineTime = parseFloat(lines[i].dataset.time);
        if (lineTime <= currentTime) {
            activeIndex = i;
        } else {
            break;  // Đã qua currentTime, dừng
        }
    }
    
    // Highlight dòng active
    lines.forEach((line, index) => {
        if (index === activeIndex) {
            line.classList.add('active');
            line.scrollIntoView({ behavior: 'smooth', block: 'center' });
        } else {
            line.classList.remove('active');
        }
    });
}
```

### Backend: Parse LRC file

```csharp
// MusicService.cs - GetLyricsAsync()
public async Task<(...) GetLyricsAsync(int songId)
{
    var song = await _context.Songs.Include(s => s.Lyrics)...;
    var lyrics = new List<LyricLineViewModel>();
    
    // 1. Thử tải từ URL (file trên R2)
    if (!string.IsNullOrWhiteSpace(song.LyricsUrl))
    {
        var content = await _httpClient.GetStringAsync(song.LyricsUrl);
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            // Regex parse LRC format: [01:23.45]Lời bài hát
            var match = Regex.Match(line, @"\[(\d+):(\d+(\.\d+)?)\](.*)");
            if (match.Success)
            {
                var min = double.Parse(match.Groups[1].Value);
                var sec = double.Parse(match.Groups[2].Value);
                var text = match.Groups[4].Value.Trim();
                lyrics.Add(new LyricLineViewModel(min * 60 + sec, text));
            }
        }
    }
    
    // 2. Fallback: Lấy từ database
    if (!lyrics.Any() && song.Lyrics.Any())
    {
        lyrics = song.Lyrics.Select(l => new LyricLineViewModel(l.TimestampSeconds, l.Content)).ToList();
    }
    
    return (lyrics, song.Title, song.Artist.Name);
}
```

---

## 3.6 Chức Năng: AI Smart Playlist

### Flow hoạt động

```
1. User nhập prompt: "Nhạc buồn về tình yêu"
2. JS gọi API: POST /playlists/ai/preview
3. Backend gửi prompt lên Gemini API
4. Gemini trả về JSON: { genres: ["Bolero"], artists: [], keywords: ["buồn", "tình"] }
5. Backend query database với tiêu chí trên
6. Trả về danh sách bài hát preview
7. User chọn bài → Tạo playlist
```

### JavaScript: `site.js`

```javascript
async function generateAIPlaylistPreview() {
    const prompt = document.getElementById('ai-playlist-prompt').value.trim();
    
    // Hiện loading
    document.getElementById('ai-playlist-loading').classList.remove('hidden');
    
    const response = await fetch('/playlists/ai/preview', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prompt })
    });
    
    const result = await response.json();
    
    if (!result.songs || result.songs.length === 0) {
        showAIPlaylistError('Không tìm thấy bài hát phù hợp');
        return;
    }
    
    // Render preview
    aiPreviewSongs = result.songs.map(s => ({ ...s, selected: true }));
    renderAIPreviewSongs();
    document.getElementById('ai-playlist-name').value = result.suggestedName;
}
```

### Backend: Gọi Gemini API

```csharp
// AIPlaylistService.cs
private async Task<ParsedCriteria> ParsePromptWithGeminiAsync(string prompt)
{
    var requestBody = new
    {
        contents = new[]
        {
            new
            {
                parts = new[]
                {
                    new
                    {
                        text = $@"
Bạn là AI giúp tạo playlist nhạc Việt.

CÁC THỂ LOẠI CÓ SẴN:
- EDM Sôi Động, Acoustic Chill, Bolero Trữ Tình, Nhạc Việt...

ÁNH XẠ MOOD → THỂ LOẠI:
- Buồn, tâm trạng → Bolero Trữ Tình, Acoustic Chill
- Vui, sôi động → EDM Sôi Động

Yêu cầu: ""{prompt}""

Trả về JSON:
{{""genres"": [...], ""artists"": [...], ""keywords"": [...], ""suggestedName"": ""...""}}
"
                    }
                }
            }
        },
        generationConfig = new { temperature = 0.3, maxOutputTokens = 256 }
    };
    
    var response = await _httpClient.PostAsync(
        $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:generateContent?key={apiKey}",
        new StringContent(JsonSerializer.Serialize(requestBody)));
    
    // Parse response và trả về ParsedCriteria
}
```

---

# PHẦN 4: TÓM TẮT

## 4.1 Kiến Trúc

```
         MVC (Server Render)                    SPA (Client Update)
               ↓                                       ↓
┌─────────────────────────┐              ┌─────────────────────────┐
│  User truy cập /        │              │  User click "Thư viện"  │
│         ↓               │              │         ↓               │
│  HomeController.Index() │              │  switchView('library')  │
│         ↓               │              │         ↓               │
│  View(model) → HTML     │              │  JS ẩn/hiện div         │
│         ↓               │              │         ↓               │
│  Browser render lần đầu │              │  Không reload trang     │
└─────────────────────────┘              └─────────────────────────┘
```

## 4.2 File Quan Trọng Nhất

| File | Vai trò | Bỏ được không? |
|------|---------|----------------|
| `Program.cs` | Entry point, DI | ❌ |
| `HomeController.cs` | API chính | ❌ |
| `MusicService.cs` | Logic nghiệp vụ | ❌ |
| `site.js` | SPA + Player | ❌ |
| `_PlayerBar.cshtml` | Audio player | ❌ |
| `Index.cshtml` | Layout chính | ❌ |
| `ApplicationDbContext.cs` | Database | ❌ |

## 4.3 Flow Chính

```
Khởi động → Program.cs → Cấu hình DI + Database
    ↓
Truy cập / → HomeController.Index() → BuildHomeAsync() → View(model)
    ↓
Browser nhận HTML → site.js khởi tạo (IIFE tự chạy)
    ↓
User click bài hát → playSong() → audio.play() + recordPlay()
    ↓
User click "Thư viện" → switchView('library-view') → Ẩn/Hiện div
```

---

# PHẦN 5: AJAX - CÓ SỬ DỤNG KHÔNG? Ở ĐÂU?

## 5.1 AJAX Là Gì?

**AJAX** = Asynchronous JavaScript and XML
- Gọi server **không reload trang**
- Project này dùng **`fetch()` API** (chuẩn hiện đại thay cho jQuery $.ajax)

## 5.2 Tất Cả Các Chỗ Sử Dụng AJAX

### 📋 Bảng Tổng Hợp

| Chức năng | URL | Method | Trả về | File |
|-----------|-----|--------|--------|------|
| Tìm kiếm | `/search?term=...` | GET | JSON | site.js |
| Toggle Like | `/favorites/{id}` | POST | JSON | site.js |
| Ghi lịch sử | `/player/play/{id}` | POST | JSON | site.js |
| Lấy lời bài hát | `/lyrics/{id}` | GET | JSON | site.js |
| Đánh giá sao | `/songs/{id}/rating` | POST | JSON | site.js |
| Tạo playlist | `/playlists` | POST | JSON | site.js |
| Thêm bài vào playlist | `/playlists/{id}/songs` | POST | JSON | site.js |
| Follow/Unfollow | `/follow/{id}`, `/unfollow/{id}` | POST | JSON | site.js |
| Đăng nhập | `/account/login` | POST | JSON | site.js |
| Đăng ký | `/account/register` | POST | JSON | site.js |
| Lấy thông báo | `/Notification/GetNotifications` | GET | JSON | site.js |
| AI Playlist Preview | `/playlists/ai/preview` | POST | JSON | site.js |
| **Xem Album** | `/album/{id}` | GET | **HTML** | site.js |
| **Xem Genre** | `/genre/{id}` | GET | **HTML** | site.js |
| **Xem Artist** | `/artist/{id}` | GET | **HTML** | site.js |

### 5.3 Code Ví Dụ: AJAX Trả Về JSON

```javascript
// site.js - Tìm kiếm
function search(term) {
    // 1. Gọi API (AJAX = fetch)
    fetch(`/search?term=${encodeURIComponent(term)}`)
        .then(res => res.json())  // 2. Parse JSON response
        .then(data => {
            // 3. Xử lý data và render UI
            renderSearchResults(data.data, term);
        })
        .catch(err => {
            showToast('Có lỗi xảy ra khi tìm kiếm');
        });
}
```

**Giải thích:**
- `fetch('/search?term=...')` → Gửi GET request đến server
- Server xử lý, trả về JSON: `{ success: true, data: { songs: [...], artists: [...] } }`
- JS nhận JSON → `renderSearchResults()` tạo HTML từ data

### 5.4 Code Ví Dụ: AJAX Trả Về HTML (Partial View)

```javascript
// site.js - Load Album Detail
window.loadAlbum = function(albumId) {
    // 1. Ẩn tất cả views khác
    document.querySelectorAll('.content-padding').forEach(el => el.classList.add('hidden'));
    
    // 2. Gọi API - Trả về HTML (Partial View)
    fetch(`/album/${albumId}`)
        .then(res => {
            if (!res.ok) throw new Error('Không thể tải album');
            return res.text();  // <-- Nhận HTML, KHÔNG phải JSON
        })
        .then(html => {
            // 3. Inject HTML vào DOM
            const mainView = document.querySelector('.main-view');
            mainView.insertAdjacentHTML('beforeend', html);
            
            // 4. Parse data từ HTML attribute cho queue
            const albumView = document.getElementById('album-view');
            if (albumView && albumView.dataset.albumSongs) {
                state.contextQueue = JSON.parse(albumView.dataset.albumSongs);
            }
        })
        .catch(err => showToast(err.message));
};
```

**Giải thích:**
- `fetch('/album/5')` → Server trả về **HTML** (PartialView)
- `res.text()` → Nhận string HTML thay vì JSON
- `insertAdjacentHTML()` → Chèn HTML vào trang

---

# PHẦN 6: PARTIAL VIEW - VAI TRÒ GÌ?

## 6.1 Partial View Là Gì?

- **Partial View** = Một mảnh HTML nhỏ, có thể **tái sử dụng**
- File bắt đầu bằng `_` (underscore): `_HomeSection.cshtml`, `_PlayerBar.cshtml`
- Được **include** vào View chính hoặc **trả về qua AJAX**

## 6.2 Hai Cách Sử Dụng Partial View

### Cách 1: Include Khi Server Render (SSR)

```html
<!-- Views/Home/Index.cshtml -->
@model HomeViewModel

<!-- Server render TẤT CẢ partial views cùng lúc -->
<partial name="_Sidebar" model="Model" />
<partial name="_HomeSection" model="Model" />
<partial name="_LibrarySection" model="Model" />
<partial name="_ProfileSection" model="Model" />
<partial name="_PlayerBar" />
<partial name="_Modals" model="Model" />
```

**Khi nào?** Lần đầu load trang `/`
**Kết quả:** HTML chứa TẤT CẢ sections, JS chỉ cần ẩn/hiện

### Cách 2: Trả Về Qua AJAX (Dynamic Load)

```csharp
// HomeController.cs
[HttpGet("/album/{id:int}")]
public async Task<IActionResult> GetAlbum(int id)
{
    var album = await _musicService.GetAlbumDetailAsync(id, userId);
    return PartialView("_AlbumDetailSection", album);  // <-- Trả về HTML
}
```

**Khi nào?** User click vào album card
**Kết quả:** Server chỉ render 1 partial → Gửi về → JS inject vào DOM

## 6.3 Danh Sách Partial Views Trong Project

### Partial Views Load Lúc Đầu (SSR)

| Partial View | Vai trò | Luôn hiện? |
|--------------|---------|------------|
| `_Sidebar.cshtml` | Menu bên trái | ✅ Luôn hiện |
| `_Header.cshtml` | Thanh tìm kiếm, nút user | ✅ Luôn hiện |
| `_HomeSection.cshtml` | Trang chủ (Chart, New Releases) | Mặc định hiện |
| `_LibrarySection.cshtml` | Thư viện cá nhân | Ẩn, JS toggle |
| `_ProfileSection.cshtml` | Trang cá nhân | Ẩn, JS toggle |
| `_PlayerBar.cshtml` | Player (audio element) | ✅ Luôn hiện |
| `_FullScreenPlayer.cshtml` | Player toàn màn hình | Ẩn, JS toggle |
| `_Modals.cshtml` | Tất cả popup/modal | Ẩn, JS toggle |
| `_UploadSection.cshtml` | Form upload bài hát | Ẩn, JS toggle |
| `_NotificationSection.cshtml` | Danh sách thông báo | Ẩn, JS toggle |
| `_PremiumSection.cshtml` | Gói Premium, ví | Ẩn, JS toggle |
| `_UserAlbumsSection.cshtml` | Album của user | Ẩn, JS toggle |

### Partial Views Load Động (AJAX)

| Partial View | Khi nào load? | API endpoint |
|--------------|---------------|--------------|
| `_AlbumDetailSection.cshtml` | Click album card | `/album/{id}` |
| `_GenreDetailSection.cshtml` | Click genre tile | `/genre/{id}` |
| `_ArtistDetailSection.cshtml` | Click artist | `/artist/{id}` |
| `_PlaylistDetailSection.cshtml` | Click playlist | `/playlists/{id}` |
| `_UserAlbumDetailSection.cshtml` | Click user album | `/useralbums/{id}` |

## 6.4 Tại Sao Chia Thành Partial Views?

### ❌ Không dùng Partial View:

```html
<!-- Index.cshtml - Một file 2000+ dòng -->
<div class="home">
    <!-- 300 dòng home section -->
</div>
<div class="library">
    <!-- 200 dòng library section -->
</div>
<div class="profile">
    <!-- 400 dòng profile section -->
</div>
<!-- ... -->
```

**Vấn đề:** Khó đọc, khó maintain, khó tái sử dụng

### ✅ Dùng Partial View:

```html
<!-- Index.cshtml - Ngắn gọn, dễ đọc -->
<partial name="_HomeSection" model="Model" />
<partial name="_LibrarySection" model="Model" />
<partial name="_ProfileSection" model="Model" />
```

```html
<!-- _HomeSection.cshtml - File riêng, dễ chỉnh sửa -->
<div id="home-view" class="content-padding">
    <!-- Chỉ chứa code cho Home -->
</div>
```

**Lợi ích:**
1. **Tách biệt concern**: Mỗi file 1 chức năng
2. **Dễ maintain**: Sửa Home không ảnh hưởng Library
3. **Tái sử dụng**: Có thể dùng lại ở nhiều nơi
4. **Load động**: Có thể trả về qua AJAX

## 6.5 Flow Hoàn Chỉnh: Xem Album

```
1. User click Album card
    ↓
2. JS gọi: fetch('/album/5')
    ↓
3. HomeController.GetAlbum(5) được gọi
    ↓
4. MusicService.GetAlbumDetailAsync(5) query DB
    ↓
5. return PartialView("_AlbumDetailSection", albumData)
    ↓
6. Razor render _AlbumDetailSection.cshtml thành HTML
    ↓
7. HTML được gửi về browser
    ↓
8. JS nhận: fetch().then(res => res.text())
    ↓
9. JS inject: mainView.insertAdjacentHTML('beforeend', html)
    ↓
10. User thấy chi tiết Album (KHÔNG reload trang!)
```

---

# PHẦN 7: SO SÁNH TỔNG HỢP

| Kỹ thuật | Khi nào dùng? | Ví dụ trong Project |
|----------|---------------|---------------------|
| **MVC (SSR)** | Lần đầu load trang | `HomeController.Index()` → `View(model)` |
| **SPA (JS)** | Chuyển view không load | `switchView('library-view')` |
| **AJAX JSON** | Thao tác nhỏ, cập nhật UI | Like, Search, Rating |
| **AJAX HTML (Partial)** | Load nội dung lớn | Album detail, Artist detail |
| **Partial View (SSR)** | Tổ chức code, tái sử dụng | `_Sidebar`, `_PlayerBar` |
| **Partial View (AJAX)** | Load động nội dung | `_AlbumDetailSection` |

---

# PHẦN 8: CÁC PACKAGES/LIBRARIES SỬ DỤNG

## 8.1 Danh Sách Packages Trong Project

```xml
<!-- MusicWeb.csproj -->
<ItemGroup>
    <PackageReference Include="AWSSDK.S3" Version="4.0.14.3" />
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="MailKit" Version="4.14.1" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Facebook" Version="8.0.10" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="8.0.10" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.10" />
</ItemGroup>
```

---

## 8.2 AWSSDK.S3 - Upload File Lên Cloudflare R2

### Package này là gì?
- **AWS SDK for .NET** - Thư viện chính thức của Amazon để giao tiếp với các dịch vụ AWS
- Cloudflare R2 **tương thích S3 API** → Dùng SDK của AWS

### Cung cấp những gì?
| Class/Interface | Chức năng |
|-----------------|-----------|
| `IAmazonS3` | Client giao tiếp với S3/R2 |
| `TransferUtility` | Upload file dạng stream |
| `PutObjectRequest` | Cấu hình request upload |
| `DeleteObjectAsync()` | Xóa file khỏi bucket |

### Sử dụng ở đâu trong Project?

**File: `Services/CloudflareStorageService.cs`**

```csharp
using Amazon.S3;                    // <-- Từ AWSSDK.S3
using Amazon.S3.Transfer;           // <-- Từ AWSSDK.S3

public class CloudflareStorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;  // Client S3
    
    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder)
    {
        // TransferUtility giúp upload stream lên R2
        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            Key = $"{folder}/{Guid.NewGuid()}_{fileName}",
            BucketName = bucketName,
            DisablePayloadSigning = true  // Quan trọng cho R2!
        };
        
        var fileTransferUtility = new TransferUtility(_s3Client);
        await fileTransferUtility.UploadAsync(uploadRequest);  // <-- Upload lên cloud
        
        return $"{publicDomain}/{key}";  // Trả về URL public
    }
    
    public async Task DeleteFileAsync(string fileUrl)
    {
        await _s3Client.DeleteObjectAsync(bucketName, key);  // <-- Xóa file
    }
}
```

**Cấu hình trong `Program.cs`:**

```csharp
// Đăng ký S3 Client với endpoint của Cloudflare R2
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = new AmazonS3Config
    {
        ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true
    };
    return new AmazonS3Client(accessKey, secretKey, config);
});
```

---

## 8.3 MailKit - Gửi Email

### Package này là gì?
- Thư viện **gửi/nhận email** hiện đại cho .NET
- Hỗ trợ **SMTP, IMAP, POP3**
- **Async/await native** - tốt hơn System.Net.Mail

### Cung cấp những gì?
| Class | Chức năng |
|-------|-----------|
| `SmtpClient` | Client gửi email qua SMTP |
| `MimeMessage` | Tạo email (From, To, Subject, Body) |
| `BodyBuilder` | Tạo body HTML hoặc plain text |
| `MailboxAddress` | Địa chỉ email với tên hiển thị |
| `SecureSocketOptions` | Cấu hình TLS/SSL |

### Sử dụng ở đâu trong Project?

**File: `Services/EmailService.cs`**

```csharp
using MailKit.Net.Smtp;        // <-- Từ MailKit
using MailKit.Security;        // <-- Từ MailKit  
using MimeKit;                 // <-- Từ MailKit (email message)

public class EmailService : IEmailService
{
    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string userName)
    {
        // 1. Tạo email message
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Music Web", "noreply@musicweb.com"));
        message.To.Add(new MailboxAddress(userName, toEmail));
        message.Subject = "Đặt lại mật khẩu - Music Web App";
        
        // 2. Tạo body HTML
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = GetPasswordResetEmailTemplate(userName, resetLink)  // HTML template
        };
        message.Body = bodyBuilder.ToMessageBody();
        
        // 3. Gửi qua SMTP
        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(username, password);
        await client.SendAsync(message);  // <-- GỬI EMAIL
        await client.DisconnectAsync(true);
    }
}
```

### Chức năng sử dụng:
- **Quên mật khẩu**: Gửi link reset password qua email

---

## 8.4 Microsoft.AspNetCore.Authentication.Google - Đăng Nhập Google

### Package này là gì?
- **OAuth 2.0 handler** cho Google Identity
- Tích hợp sẵn với ASP.NET Core Authentication

### Cung cấp những gì?
| Thành phần | Chức năng |
|------------|-----------|
| `AddGoogle()` | Extension method đăng ký Google Auth |
| `GoogleOptions` | Cấu hình ClientId, ClientSecret |
| OAuth callback handling | Tự động xử lý redirect và token |

### Sử dụng ở đâu trong Project?

**File: `Program.cs`**

```csharp
// Cấu hình Google Authentication
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/signin-google";  // URL Google redirect về
    });
```

**File: `Controllers/AccountController.cs`**

```csharp
// Redirect user đến Google để đăng nhập
[HttpGet("google-login")]
public IActionResult GoogleLogin(string returnUrl = "/")
{
    var properties = new AuthenticationProperties { RedirectUri = "/account/external-callback" };
    return Challenge(properties, GoogleDefaults.AuthenticationScheme);
}

// Xử lý khi Google redirect về
[HttpGet("external-callback")]
public async Task<IActionResult> ExternalCallback()
{
    var info = await _signInManager.GetExternalLoginInfoAsync();
    // info.Principal chứa thông tin user từ Google (email, name, picture...)
    
    // Tạo hoặc link account trong database
    var user = await _userManager.FindByEmailAsync(email);
    if (user == null)
    {
        user = new ApplicationUser { Email = email, Provider = "Google" };
        await _userManager.CreateAsync(user);
    }
    
    await _signInManager.SignInAsync(user, isPersistent: true);
    return Redirect("/");
}
```

---

## 8.5 Microsoft.AspNetCore.Authentication.Facebook - Đăng Nhập Facebook

### Package này là gì?
- **OAuth 2.0 handler** cho Facebook Login
- Tương tự Google package

### Cung cấp những gì?
| Thành phần | Chức năng |
|------------|-----------|
| `AddFacebook()` | Extension method đăng ký Facebook Auth |
| `FacebookOptions` | Cấu hình AppId, AppSecret |
| `Fields` property | Chọn data lấy từ Facebook (email, name, picture) |

### Sử dụng ở đâu trong Project?

**File: `Program.cs`**

```csharp
builder.Services.AddAuthentication()
    .AddFacebook(options =>
    {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
        options.CallbackPath = "/signin-facebook";
        options.Fields.Add("email");     // Yêu cầu Facebook trả về email
        options.Fields.Add("name");      // Yêu cầu Facebook trả về tên
        options.Fields.Add("picture");   // Yêu cầu Facebook trả về avatar
    });
```

---

## 8.6 Microsoft.AspNetCore.Identity.EntityFrameworkCore - Quản Lý User

### Package này là gì?
- **ASP.NET Core Identity** tích hợp với **Entity Framework Core**
- Cung cấp sẵn hệ thống user, role, login, password hashing

### Cung cấp những gì?
| Class | Chức năng |
|-------|-----------|
| `IdentityUser` | Base class cho User entity |
| `IdentityRole` | Base class cho Role |
| `UserManager<T>` | CRUD user, đổi password, confirm email |
| `SignInManager<T>` | Đăng nhập, đăng xuất, OAuth |
| `RoleManager<T>` | CRUD roles (Admin, User...) |
| Password Hasher | Tự động hash mật khẩu (PBKDF2) |

### Sử dụng ở đâu trong Project?

**File: `Models/Entities/ApplicationUser.cs`**

```csharp
// Kế thừa IdentityUser để thêm custom fields
public class ApplicationUser : IdentityUser  // <-- Từ Identity
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Provider { get; set; } = "Local";  // Google/Facebook/Local
    public string? ProviderKey { get; set; }
    
    // Navigation properties
    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
}
```

**File: `Controllers/AccountController.cs`**

```csharp
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;      // CRUD user
    private readonly SignInManager<ApplicationUser> _signInManager;  // Login/Logout
    
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser { Email = request.Email, UserName = request.Email };
        
        // UserManager tự động hash password
        var result = await _userManager.CreateAsync(user, request.Password);
        
        if (!result.Succeeded)
            return BadRequest(result.Errors);
            
        await _signInManager.SignInAsync(user, isPersistent: true);
        return Ok();
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _signInManager.PasswordSignInAsync(
            request.Email, request.Password, 
            isPersistent: true, lockoutOnFailure: false);
            
        if (!result.Succeeded)
            return BadRequest("Email hoặc mật khẩu không đúng");
            
        return Ok();
    }
}
```

---

## 8.7 BCrypt.Net-Next - Hash Password (Backup)

### Package này là gì?
- Thư viện **hash password** sử dụng thuật toán BCrypt
- **Backup** cho Identity (Identity dùng PBKDF2 mặc định)

### Cung cấp những gì?
| Method | Chức năng |
|--------|-----------|
| `BCrypt.HashPassword(plain)` | Hash password |
| `BCrypt.Verify(plain, hash)` | So sánh password với hash |

### Lưu ý:
- Trong project này, **Identity đã handle password hashing**
- BCrypt có thể được dùng cho các trường hợp custom nếu cần

---

## 8.8 Entity Framework Core Packages

### Microsoft.EntityFrameworkCore.SqlServer
- Provider cho **SQL Server**
- Cho phép EF Core giao tiếp với SQL Server

### Microsoft.EntityFrameworkCore.Design
- Dùng cho **Migrations** (`dotnet ef migrations add`)
- Chỉ cần lúc development

### Microsoft.EntityFrameworkCore.Tools
- CLI tools: `dotnet ef database update`
- Chỉ cần lúc development

---

## 8.9 Tóm Tắt Package Theo Chức Năng

| Chức năng | Package | Class chính |
|-----------|---------|-------------|
| **Upload lên Cloudflare R2** | AWSSDK.S3 | `IAmazonS3`, `TransferUtility` |
| **Gửi email** | MailKit | `SmtpClient`, `MimeMessage` |
| **Đăng nhập Google** | Authentication.Google | `AddGoogle()` |
| **Đăng nhập Facebook** | Authentication.Facebook | `AddFacebook()` |
| **Quản lý User** | Identity.EntityFrameworkCore | `UserManager`, `SignInManager` |
| **Database** | EntityFrameworkCore.SqlServer | `DbContext`, LINQ |

---

**Hy vọng tài liệu này giúp bạn hiểu rõ project! 📚**
