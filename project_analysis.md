# Phân Tích Chuyên Sâu Project MusicWeb

Tài liệu này phân tích chi tiết toàn bộ dự án MusicWeb, giúp bạn nắm vững kiến trúc, luồng hoạt động và các chi tiết kỹ thuật quan trọng.

## 1. Kiến Trúc Tổng Thể

### Mô hình: **Hybrid MVC (Lai giữa MVC và SPA)**
Project không thuần túy là MVC cổ điển, cũng không phải là Single Page Application (SPA) hoàn toàn. Nó kết hợp cả hai:
*   **MVC (Model-View-Controller)**: Dùng cho lần tải trang đầu tiên (`HomeController`). Server render toàn bộ HTML cơ bản để đảm bảo tốc độ hiển thị và SEO.
*   **Client-Side Interactivity**: Sau khi trang đã tải, JavaScript (`site.js`) đảm nhận việc chuyển đổi giao diện (Home/Library/Profile) và gọi API để lấy dữ liệu mới mà không tải lại trang.

### Kiến trúc phân lớp (Layered Architecture)
Luồng dữ liệu đi theo một chiều:
1.  **Presentation Layer**: `Views` (.cshtml) và `Controllers` (Home, Account...).
2.  **Service Layer**: `MusicService` chứa logic nghiệp vụ (business logic) như lấy bài hát, tạo playlist.
3.  **Data Access Layer**: `ApplicationDbContext` (Entity Framework Core) làm việc trực tiếp với Database.
4.  **Database**: SQL Server lưu trữ dữ liệu.

### Tại sao chọn kiến trúc này?
*   **Phù hợp cho web nghe nhạc**: Web nghe nhạc cần duy trì trình phát nhạc (Audio Player) liên tục. Nếu dùng MVC thuần (mỗi lần bấm link là reload trang), nhạc sẽ bị tắt. Cách làm hiện tại giữ nhạc luôn chạy khi người dùng chuyển tab.
*   **Đơn giản hơn React/Angular**: Không cần xây dựng 2 project riêng biệt (Backend API + Frontend React), giúp phát triển nhanh với team nhỏ hoặc cá nhân.

### Cơ Chế Hybrid Chi Tiết
Project sử dụng 2 cách hiển thị dữ liệu song song:
1.  **Server-Side Rendering (Partial Views)**:
    *   Dùng cho các block nội dung lớn như: `Album Detail`, `Genre Detail`, `Playlist Detail`.
    *   *Cách làm*: Controller trả về một đoạn HTML (`PartialViewResult`), JavaScript chỉ việc chèn (`innerHTML`) vào container. Không cần JS để parse dữ liệu phức tạp.
2.  **Client-Side Rendering (JSON API)**:
    *   Dùng cho các hành động tương tác nhỏ: `Search` (Tìm kiếm), `Toggle Favorite` (Thích), `Player Control` (Ghi nhận lượt nghe).
    *   *Cách làm*: Controller trả về JSON, Javascript nhận dữ liệu và vẽ lại từng phần tử DOM.

---

## 2. Phân Tích Từng Chức Năng

### A. Nghe Nhạc & Player
*   **Mục đích**: Phát nhạc, điều khiển (play/pause, next, prev), giữ trạng thái khi chuyển trang.
*   **Vận hành**:
    *   Thẻ `<audio>` trong `_PlayerBar.cshtml` là nòng cốt.
    *   `site.js` lắng nghe sự kiện click vào bài hát -> gán `src` cho thẻ audio -> gọi `audio.play()`.
*   **Flow**: User click bài hát -> JS bắt sự kiện -> Cập nhật UI Player -> Gọi API `/player/play/{id}` để ghi lịch sử -> Nhạc phát.

### B. Hệ thống Tài khoản (Identity)
*   **Mục đích**: Đăng ký, đăng nhập, phân quyền Admin/User.
*   **Component**: `AccountController`, `ApplicationUser`, `Program.cs` (cấu hình Identity).
*   **Logic**: Sử dụng thư viện **ASP.NET Core Identity**. Mật khẩu được băm (hash) tự động.
*   **Flow**: User nhập form -> JS gửi POST `/account/login` -> Controller kiểm tra DB -> Trả về Cookie xác thực.

### C. Playlist & Yêu thích
*   **Mục đích**: Cá nhân hóa trải nghiệm.
*   **Component**: `PlaylistController`, `MusicService`.
*   **Logic**:
    *   Playlist là quan hệ 1-nhiều (1 User có nhiều Playlist).
    *   Playlist-Song là quan hệ nhiều-nhiều (1 Playlist có nhiều bài, 1 bài nằm trong nhiều Playlist).
*   **Flow**: User bấm "Thêm vào playlist" -> JS gọi API -> Server thêm record vào bảng `PlaylistSongs` -> Trả về Success -> JS báo Toast "Thành công".

### D. Tích hợp Cloudflare R2 (Deep Dive)
*   **Mục đích**: Tận dụng Storage giá rẻ, băng thông không giới hạn của Cloudflare R2 (S3-compatible) để chứa media.
*   **Thư viện**: `AWSSDK.S3` (Do R2 tương thích API S3 của Amazon).
*   **Kết nối (`Program.cs`)**:
    *   Đăng ký `IAmazonS3` Client dạng Singleton.
    *   Cấu hình `ServiceURL` trỏ tới Endpoint của R2.
    *   **Quan trọng**: `DisablePayloadSigning = true` (trong `CloudflareStorageService.cs`) để tương thích tốt nhất với R2, tránh lỗi signature mismatch.
*   **Cơ chế Đặt tên & Tổ chức Folder**:
    *   Để tránh trùng lặp và dễ quản lý, file không lưu lộn xộn mà theo cấu trúc:
        `{loại_file}/{tên_user}-{id_user}/{guid}_{tên_file_gốc}`
    *   *Ví dụ*: `music/thinhlai-01/a1b2..._bai-hat-1.mp3`
    *   **Guid**: Đảm bảo 2 file cùng tên up lên không đè nhau.
    *   **User Folder**: Giúp dễ dàng cleanup, backup dữ liệu của từng user.
*   **Quy trình Upload**:
    1.  `UploadController` nhận file từ Form.
    2.  Tạo "Safe Folder Name" từ Username + ID.
    3.  Gọi `UploadFileAsync` của Service.
    4.  Service dùng `TransferUtility` để bắn stream file lên R2.
    5.  Ghép `PublicDomain` cấu hình sẵn (ví dụ: `https://pub-xxx.r2.dev`) + `Key` để tạo thành URL public lưu vào DB.

### E. Player Logic (Shuffle & Loop)
*   **Mục đích**: Tăng trải nghiệm nghe nhạc với các chế độ phát ngẫu nhiên và lặp lại.
*   **Shuffle (Xáo trộn)**:
    *   **Thuật toán**: Sử dụng **Fisher-Yates Shuffle** để đảm bảo tính ngẫu nhiên công bằng.
    *   **Logic**: Khi bật Shuffle, `queue` hiện tại được sao lưu vào `state.originalQueue`. Một bản sao của `queue` được trộn và gán lại cho `state.queue`. Khi tắt, khôi phục từ `originalQueue`.
*   **Loop (Lặp lại)**:
    *   **Cơ chế**: State Machine 3 trạng thái (`off` -> `all` -> `one`).
    *   **Loop One**: Khi bài hát kết thúc (sự kiện `ended` hoặc hàm `playNext`), logic kiểm tra `state.loopMode === 'one'`. Nếu đúng, đặt thời gian bài hát về 0 (`currentTime = 0`) và phát lại ngay lập tức.
    *   **Loop All**: Khi danh sách phát đi đến bài cuối cùng (`index >= length`), nếu `loopMode === 'all'`, chỉ số bài hát (`queueIndex`) được reset về 0 để phát lại từ đầu.
    *   **Loop Off**: Nếu đến cuối danh sách mà không bật Loop, trình phát sẽ dừng lại.

### F. Lời bài hát (Lyrics) & Thuật toán Fallback
*   **Mục đích**: Hiển thị lời bài hát cho người dùng.
*   **Logic (MusicService.GetLyricsAsync)**:
    1.  Ưu tiên 1: Kiểm tra `LyricsUrl` (Link file .txt từ R2). Dùng `HttpClient` tải nội dung text về và cắt dòng (`Split`).
    2.  Ưu tiên 2 (Fallback): Nếu không có URL hoặc lỗi tải, lấy dữ liệu text thô từ bảng `SongLyrics` trong Database.
    3.  Kết quả: Trả về danh sách từng dòng để hiển thị lên UI.

### F. Chức Năng Admin & Quản Lý
*   **Quản lý Album**: Thêm/Xóa bài hát trong Album, Upload ảnh bìa mới.
*   **Quản lý User**: Khóa (Lockout) hoặc Mở khóa tài khoản người dùng vi phạm.
*   **Xóa dọn dẹp**: Khi xóa bài hát (`DeleteSong`), hệ thống tự động gọi API của R2 để xóa file vật lý trên cloud, tránh rác dữ liệu.

### G. Mạng Xã Hội & Tương Tác (Mới)
*   **Mục đích**: Tăng tính kết nối giữa người dùng (Follow/Unfollow).
*   **Component**: `FollowController`, `UserFollows` Table.
*   **Logic**:
    *   Quan hệ N-N giữa User và User (thông qua bảng `UserFollows`).
    *   Hệ thống đếm số lượng Followers/Following để hiển thị trên Profile.
    *   Danh sách User được hiển thị trong Modal (`_Modals.cshtml`) và kết quả tìm kiếm.
*   **Flow**: User bấm "Follow" -> JS gọi API `/follow/{id}` -> Server kiểm tra và tạo record -> UI cập nhật nút thành "Đang theo dõi" (Optimistic Update).

### H. Quản lý Hiển thị (Private/Public)
*   **Mục đích**: Cho phép upload bài hát ở chế độ riêng tư (chỉ mình nghe) hoặc công khai.
*   **Logic**:
    *   Cột `IsPublic` (bit) trong bảng `Songs`.
    *   User click icon "Mắt" trong tab "Bài hát đã đăng" -> Gọi API `/upload/toggle/{id}` -> Server đảo giá trị -> UI cập nhật icon/badge "Riêng tư".

---

## 3. Giải Thích Code Quan Trọng

### `Program.cs` (Trái tim của ứng dụng)
*   `AddDbContext`: Kết nối SQL Server.
*   `AddIdentity`: Cấu hình hệ thống user (yêu cầu password, email unique...).
    *   `AddScoped<IMusicService, MusicService>`: Đăng ký Dependency Injection. Mỗi khi Controller cần `IMusicService`, hệ thống sẽ tự tạo mới một `MusicService`.
*   `AddSingleton<IStorageService, CloudflareStorageService>`: Dịch vụ lưu trữ Cloud được đăng ký dạng Singleton (hoặc Scoped) để dùng chung cấu hình kết nối R2.

### `MusicService.cs` (Bộ não logic)
*   **`BuildHomeAsync`**: Hàm "khủng" nhất. Nó thực hiện song song nhiều query (Lấy bài mới, lấy chart, lấy playlist...) để đóng gói vào `HomeViewModel`.
*   **`GetLyricsAsync`**: Logic thông minh - nếu bài hát có `LyricsUrl` (link file text), nó sẽ dùng `HttpClient` tải nội dung về. Nếu không, nó lấy từ database.

### `CloudflareStorageService.cs` (Giao tiếp Cloud)
*   Implement `IStorageService` sử dụng SDK của AWS S3.
*   **Trick**: Cấu hình `DisablePayloadSigning = true` vì Cloudflare R2 không yêu cầu (và đôi khi không hỗ trợ) ký payload giống AWS s3 gốc, giúp upload nhanh hơn.
*   **Sanitize**: Tự động làm sạch tên file (bỏ dấu tiếng Việt, khoảng trắng) và gắn `Guid` để đảm bảo Unique.

### `site.js` (Người điều phối giao diện)
*   Sử dụng **Module Pattern** (bọc trong `(function(){...})()`) để tránh xung đột biến toàn cục.
*   `state` object: Lưu trữ trạng thái hiện tại (bài đang hát, playlist đang chọn...) ngay tại trình duyệt.
*   **Client-Side Sorting**: Hàm `sortSongs` thực hiện thuật toán sắp xếp ngay trên trình duyệt (dựa vào `data-attributes` của thẻ HTML) cho các tiêu chí: Lượt xem, Đánh giá, Ngày phát hành. Giúp phản hồi tức thì không cần request lại server.

---

## 4. Phân Tích Thư Viện & Công Nghệ

| Thư viện / Công nghệ | Mục đích | Tại sao dùng? |
| :--- | :--- | :--- |
| **ASP.NET Core MVC** | Framework chính | Hiệu năng cao, bảo mật tốt, chuẩn công nghiệp. |
| **Entity Framework Core** | ORM (Làm việc với DB) | Thao tác DB bằng code C# (LINQ) thay vì viết SQL thủ công. |
| **ASP.NET Core Identity** | Quản lý User | Bảo mật cao, có sẵn tính năng hash password, chống tấn công, quản lý role. |
| **AWSSDK.S3** | Client Storage | Giao tiếp với Cloudflare R2 (tương thích S3 API) để upload/delete file. |
| **FontAwesome** | Icon | Hiển thị các icon play, pause, user đẹp mắt. |
| **JsonSerializer** | Xử lý JSON | Chuyển đổi object C# sang JSON để gửi xuống JavaScript. |

---

## 5. Danh Sách API & Endpoints

Các API chạy trên cùng server với web (Internal API).

### 1. Account (Xác thực & Cá nhân)
| Method | Route | Chức năng | Auth |
| :--- | :--- | :--- | :--- |
| `POST` | `/account/register` | Đăng ký tài khoản mới. | Public |
| `POST` | `/account/login` | Đăng nhập hệ thống (Cookie). | Public |
| `POST` | `/account/logout` | Đăng xuất. | User |
| `GET` | `/account/profile` | Lấy thông tin user hiện tại. | User |
| `POST` | `/account/update-profile` | Cập nhật tên, avatar, mật khẩu. | User |

### 2. Home & Music (Tương tác chính)
| Method | Route | Chức năng | Trả về |
| :--- | :--- | :--- | :--- |
| `POST` | `/search?term=...` | Tìm kiếm bài hát, album, nghệ sĩ. | JSON |
| `GET` | `/lyrics/{id}` | Lấy lời bài hát (từ R2 hoặc DB). | JSON |
| `POST` | `/favorites/{id}` | Thích / Bỏ thích bài hát. | JSON (Auth) |
| `POST` | `/player/play/{id}` | Ghi nhận lượt nghe (tăng ViewCount). | JSON |
| `POST` | `/songs/{id}/rating` | Đánh giá bài hát (1-5 sao). | JSON (Auth) |
| `GET` | `/album/{id}` | Lấy chi tiết Album. | **Partial View** |
| `GET` | `/genre/{id}` | Lấy chi tiết Thể Loại. | **Partial View** |

### 3. Queue & Playlist (Quản lý danh sách)
| Method | Route | Chức năng | Auth |
| :--- | :--- | :--- | :--- |
| `POST` | `/playlists` | Tạo Playlist mới. | User |
| `GET` | `/playlists/{id}` | Lấy chi tiết Playlist. | User (Partial HTML) |
| `POST` | `/playlists/{id}/update` | Sửa tên/ảnh Playlist. | Owner |
| `DELETE` | `/playlists/{id}` | Xóa Playlist. | Owner |
| `POST` | `/playlists/{id}/songs` | Thêm bài hát vào playlist. | Owner |
| `DELETE` | `/playlists/{id}/songs/{sid}` | Xóa bài khỏi playlist. | Owner |

### 4. Social & Follow (Mạng xã hội)
| Method | Route | Chức năng | Auth |
| :--- | :--- | :--- | :--- |
| `POST` | `/follow/{userId}` | Theo dõi người dùng. | User |
| `POST` | `/unfollow/{userId}` | Hủy theo dõi. | User |
| `GET` | `/follow/list/followers/{id}` | Lấy danh sách người theo dõi. | Public |
| `GET` | `/follow/list/following/{id}` | Lấy danh sách đang theo dõi. | Public |

### 5. Upload & Admin (Quản trị)
| Method | Route | Chức năng | Role |
| :--- | :--- | :--- | :--- |
| `POST` | `/upload` | Upload bài hát (User/Artist). | User |
| `POST` | `/admin/createsong` | Admin đăng bài hát mới. | Admin |
| `POST` | `/admin/toggleuserstatus` | Khóa/Mở khóa User. | Admin |
| `DELETE` | `/admin/deletealbum/{id}` | Xóa Album. | Admin |
| `POST` | `/upload/toggle/{id}` | Bật/Tắt chế độ Công khai/Riêng tư. | Owner |


---

## 6. Phân Tích Database

### Các bảng chính (Entities):
1.  **Songs**: Chứa thông tin bài hát (Tên, Url Audio, Url Ảnh...).
2.  **Artists**: Nghệ sĩ.
3.  **Playlists**: Danh sách phát.
4.  **AspNetUsers**: Bảng user của hệ thống Identity.

### Quan hệ (Relationships):
*   **Artist - Song**: 1-N (Một nghệ sĩ có nhiều bài).
*   **User - Playlist**: 1-N (Một user tạo nhiều playlist).
*   **Playlist - Song**: N-N (Thông qua bảng trung gian `PlaylistSong`).
*   **User - Song (Favorite)**: N-N (Thông qua bảng `FavoriteSong`).

### Cập nhật Schema Mới:
*   **Songs**: Thêm cột `LyricsUrl` (Nvarchar) để lưu link file lời.
*   **UserFollows**: Bảng mới quan hệ N-N (FollowerId, FolloweeId) để lưu trữ theo dõi.
*   **PlaylistSongs**: Thêm cột `Order` (Int) để cho phép user sắp xếp lại thứ tự bài hát trong playlist.

---

## 7. Kiến Thức Nền Cần Có

Để làm chủ project này, bạn cần nắm:
1.  **C# & OOP**: Class, Interface, Async/Await (Bất đồng bộ).
2.  **ASP.NET Core**: Dependency Injection, Middleware, Controller, Razor View.
3.  **Entity Framework Core**: Cách định nghĩa DbSet, quan hệ (HasOne, HasMany), LINQ queries.
4.  **Frontend cơ bản**: HTML/CSS, JavaScript (DOM manipulation, Fetch API).
5.  **SQL**: Hiểu cơ bản về bảng và khóa ngoại (Foreign Key).

---

## 8. Các Điểm "Tinh Tế" & "Trick" Trong Code

#### 1. Chiến lược "Hybrid Storage" cho Lời bài hát (`GetLyricsAsync`)
*   **Vấn đề**: Lời bài hát có thể rất dài (văn bản) hoặc cần hiển thị theo thời gian thực (karaoke). Lưu tất cả vào DB làm nặng bảng.
*   **Giải pháp**:
    *   **Ưu tiên 1 (Cloud)**: Nếu có `LyricsUrl`, hệ thống coi đây là file `.txt` tĩnh trên R2. Dùng `HttpClient` tải về -> Nhẹ DB, tận dụng băng thông Cloudflare.
    *   **Ưu tiên 2 (Database)**: Nếu không có URL, dùng dữ liệu trong bảng `SongLyrics` (có `Timestamp`). Đây là bước chuẩn bị cho tính năng Karaoke (lời chạy theo nhạc) sau này.
*   **Code**: `try-catch` bọc quanh `HttpClient` để nếu mạng lỗi vẫn fallback về DB mượt mà.

#### 2. Pattern "ViewModel Factory" trong `BuildHomeAsync`
*   **Mục đích**: Trang chủ cần load rất nhiều dữ liệu (Chart, Mới, Gợi ý, Playlist...).
*   **Cách làm**:
    *   Thay vì gọi 5-6 API từ JS (gây giật giao diện), Controller gọi DB 1 lần (Server-Side).
    *   Sử dụng `HashSet<int> favoriteSongIds` để tra cứu trạng thái "Thích" (O(1)) cho hàng loạt bài hát, thay vì query DB cho từng bài (N+1 query problem).
    *   **Thuật toán Chart**: Tính phần trăm (`ViewCount / TotalTop10Views`) ngay tại server để Frontend chỉ việc hiển thị thanh bar.

#### 3. Quản lý trạng thái Client (State Management)
*   **Vấn đề**: Khi chuyển trang, người dùng muốn nhạc vẫn chạy và nhớ danh sách đang phát.
*   **Giải pháp**:
    *   Sử dụng biến toàn cục `state` trong `site.js` như một "Store" (giống Redux thu nhỏ).
    *   `state.queue`: Lưu danh sách bài hát đang chờ (Context Queue).
    *   `playQueue(songs)`: Hàm trung tâm để nạp danh sách nhạc từ Album/Playlist vào Player.
*   **Trick UI**: Khi người dùng bấm "Thích", icon đổi màu *ngay lập tức* (Optimistic UI) rồi mới gửi request nền. Nếu lỗi, Toast báo và đổi lại icon. Tạo cảm giác app cực nhanh.

#### 4. Sanitization & R2 Upload
*   **Cơ chế**: File user upload thường có tên rác (có dấu, space..).
*   **Xử lý trong `AdminController/UploadController`**:
    *   `Guid.NewGuid()`: Chống trùng lặp tuyệt đối.
    *   `Replace(" ", "-")`: Làm sạch URL.
    *   Tự động tạo folder theo User (`username-id`) để dễ quản lý file vật lý trên R2 bucket.

#### 5. Cơ chế "Router thủ công" (Manual SPA Router)
*   **Mục đích**: Chuyển đổi giữa các màn hình (Home, Search, Library, Playlist Detail) mà không load lại trang.
*   **Trick**:
    *   HTML có sẵn các thẻ `div` container cho từng view (`#home-view`, `#search-view`, `#library-view`...).
    *   Hàm `switchView(viewId)` trong `site.js` hoạt động theo nguyên lý: Ẩn tất cả (`display: none`) -> Chỉ hiện `viewId` được chọn.
    *   Kết hợp với `fetch`: Khi bấm vào Album, JS gọi API lấy HTML -> Inject vào `#playlist-view` -> Gọi `switchView('playlist-view')`.

#### 6. Thuật toán Tìm kiếm Đa thực thể (Multi-Entity Search)
*   **Logic**: Khi user gõ từ khóa, server không chỉ tìm tên bài hát.
*   **Code (`SearchAsync`)**:
    *   Tạo 5 câu lệnh truy vấn (`IQueryable`) độc lập cho: `Song`, `Artist`, `Playlist`, `Album`, `User`.
    *   Trả về một `SearchResultsViewModel` chứa tất cả kết quả gói gọn.
    *   Frontend nhận 1 cục JSON và render ra 5 cột kết quả cùng lúc.

---

## 9. Câu Hỏi Phỏng Vấn Tiềm Năng

1.  **Q: Tại sao bạn không dùng React/Vue mà lại dùng cấu trúc này?**
    *   A: Để tối ưu SEO ban đầu và giảm độ phức tạp khi build/deploy. Cấu trúc này đủ tốt cho yêu cầu hiện tại mà không cần tách rời Backend/Frontend.
2.  **Q: Làm sao để xử lý khi có hàng triệu bài hát (Performance)?**
    *   A: Hiện tại đang dùng `Take(10)` để phân trang đơn giản. Nếu dữ liệu lớn, cần áp dụng **Pagination** (phân trang) thật sự ở Database và có thể dùng **Caching** (Redis) cho các query phổ biến như "Bảng xếp hạng".
3.  **Q: Project này bảo mật password như thế nào?**
    *   A: Sử dụng cơ chế Hashing mặc định của ASP.NET Identity (PBKDF2), không lưu password thô.

---

## 10. Tóm Tắt Luồng Chạy (Sequence)

1.  **Khởi động**: `Program.cs` chạy -> Kết nối DB -> Seed dữ liệu.
2.  **Truy cập**: User vào `/` -> `HomeController.Index()` gọi `MusicService.BuildHomeAsync()` -> Trả về HTML.
3.  **Tương tác**:
    *   User bấm Play -> JS `site.js` phát nhạc.
    *   User bấm Like -> JS gọi API `/favorites` -> Controller cập nhật DB -> Trả về OK -> JS đổi màu trái tim.
