-- =============================================
-- PREMIUM FEATURE DATABASE SCHEMA
-- Run this script on your MusicWeb database
-- =============================================

-- 1. Subscription Plans (Các gói Premium)
CREATE TABLE SubscriptionPlans (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    Price DECIMAL(18,2) NOT NULL DEFAULT 0,
    DurationDays INT NOT NULL DEFAULT 30,
    DownloadLimit INT NOT NULL DEFAULT 0,
    NoAds BIT NOT NULL DEFAULT 0,
    HighQualityAudio BIT NOT NULL DEFAULT 0,
    CanAccessPremiumSongs BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- 2. User Subscriptions (Subscription của user)
CREATE TABLE UserSubscriptions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    PlanId INT NOT NULL,
    StartDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    EndDate DATETIME2 NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    PaymentMethod NVARCHAR(50),
    TransactionId NVARCHAR(100),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_UserSubscriptions_User FOREIGN KEY (UserId) 
        REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserSubscriptions_Plan FOREIGN KEY (PlanId) 
        REFERENCES SubscriptionPlans(Id)
);

-- 3. Premium Song Requests (Yêu cầu duyệt bài premium)
CREATE TABLE PremiumSongRequests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SongId INT NOT NULL,
    RequestedByUserId NVARCHAR(450) NOT NULL,
    RequestDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    AdminNote NVARCHAR(500),
    ReviewedDate DATETIME2 NULL,
    ReviewedByAdminId NVARCHAR(450),
    
    CONSTRAINT FK_PremiumSongRequests_Song FOREIGN KEY (SongId) 
        REFERENCES Songs(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PremiumSongRequests_User FOREIGN KEY (RequestedByUserId) 
        REFERENCES AspNetUsers(Id)
);

-- 4. User Wallet (Ví tiền của user - 10,000 VND test balance)
CREATE TABLE UserWallets (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL UNIQUE,
    Balance DECIMAL(18,2) NOT NULL DEFAULT 10000,
    TotalEarnings DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_UserWallets_User FOREIGN KEY (UserId) 
        REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);

-- 5. Wallet Transactions (Lịch sử giao dịch ví)
CREATE TABLE WalletTransactions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Type NVARCHAR(20) NOT NULL,
    Description NVARCHAR(500),
    ReferenceId NVARCHAR(100),
    BalanceAfter DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_WalletTransactions_User FOREIGN KEY (UserId) 
        REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);

-- 6. Earnings History (Revenue sharing cho uploaders)
CREATE TABLE EarningsHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UploaderUserId NVARCHAR(450) NOT NULL,
    ListenerUserId NVARCHAR(450) NOT NULL,
    SongId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_EarningsHistory_Uploader FOREIGN KEY (UploaderUserId) 
        REFERENCES AspNetUsers(Id),
    CONSTRAINT FK_EarningsHistory_Song FOREIGN KEY (SongId) 
        REFERENCES Songs(Id) ON DELETE CASCADE
);

-- =============================================
-- ALTER EXISTING TABLES
-- =============================================

-- Add premium fields to Songs table
ALTER TABLE Songs ADD IsPremium BIT NOT NULL DEFAULT 0;
ALTER TABLE Songs ADD PremiumStatus NVARCHAR(20) NOT NULL DEFAULT 'Free';
ALTER TABLE Songs ADD UploadedByUserId NVARCHAR(450) NULL;

-- Add foreign key for uploader
ALTER TABLE Songs ADD CONSTRAINT FK_Songs_Uploader 
    FOREIGN KEY (UploadedByUserId) REFERENCES AspNetUsers(Id);

-- =============================================
-- SEED DATA - Default Subscription Plans
-- =============================================

INSERT INTO SubscriptionPlans (Name, Description, Price, DurationDays, DownloadLimit, NoAds, HighQualityAudio, CanAccessPremiumSongs)
VALUES 
    (N'Free', N'Gói miễn phí với quảng cáo', 0, 0, 0, 0, 0, 0),
    (N'Premium Mini', N'Nghe không quảng cáo, tải 5 bài/tháng', 29000, 30, 5, 1, 0, 1),
    (N'Premium VIP', N'Tất cả tính năng, tải không giới hạn, chất lượng cao', 59000, 30, -1, 1, 1, 1);

-- =============================================
-- INDEXES
-- =============================================

CREATE INDEX IX_UserSubscriptions_UserId ON UserSubscriptions(UserId);
CREATE INDEX IX_UserSubscriptions_Status ON UserSubscriptions(Status);
CREATE INDEX IX_PremiumSongRequests_Status ON PremiumSongRequests(Status);
CREATE INDEX IX_UserWallets_UserId ON UserWallets(UserId);
CREATE INDEX IX_Songs_IsPremium ON Songs(IsPremium);
CREATE INDEX IX_Songs_UploadedByUserId ON Songs(UploadedByUserId);

PRINT 'Premium tables created successfully!';
