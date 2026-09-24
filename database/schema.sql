-- ============================================================
-- DairyFlow Database Schema
-- SQL Server 2019+
-- Multi-tenant, row-level isolation by FarmId (TenantId)
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DairyFlow')
    CREATE DATABASE DairyFlow;
GO

USE DairyFlow;
GO

-- ============================================================
-- TENANTS / FARMS
-- ============================================================
CREATE TABLE Farms (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    Name            NVARCHAR(200)       NOT NULL,
    OwnerName       NVARCHAR(200)       NOT NULL,
    Phone           NVARCHAR(50),
    Email           NVARCHAR(200),
    Address         NVARCHAR(500),
    Region          NVARCHAR(100),
    Country         NVARCHAR(100)       NOT NULL DEFAULT 'Ethiopia',
    Timezone        NVARCHAR(100)       NOT NULL DEFAULT 'Africa/Addis_Ababa',
    CurrencyCode    NVARCHAR(10)        NOT NULL DEFAULT 'ETB',
    ExchangeRateUSD DECIMAL(10,4)       NOT NULL DEFAULT 155,
    IsActive        BIT                 NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- USERS
-- ============================================================
CREATE TABLE Users (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id) ON DELETE CASCADE,
    FullName        NVARCHAR(200)       NOT NULL,
    Email           NVARCHAR(200)       NOT NULL,
    Phone           NVARCHAR(50),
    PasswordHash    NVARCHAR(500)       NOT NULL,
    Role            NVARCHAR(50)        NOT NULL DEFAULT 'Worker',  -- Owner, Manager, Worker
    PreferredLang   NVARCHAR(10)        NOT NULL DEFAULT 'en',      -- en, am, om
    IsActive        BIT                 NOT NULL DEFAULT 1,
    LastLoginAt     DATETIME2,
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO

CREATE INDEX IX_Users_FarmId ON Users(FarmId);
GO

-- ============================================================
-- REFRESH TOKENS (JWT)
-- ============================================================
CREATE TABLE RefreshTokens (
    Id          UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    UserId      UNIQUEIDENTIFIER    NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Token       NVARCHAR(500)       NOT NULL,
    ExpiresAt   DATETIME2           NOT NULL,
    RevokedAt   DATETIME2,
    CreatedAt   DATETIME2           NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- COWS
-- ============================================================
CREATE TABLE Cows (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id) ON DELETE CASCADE,
    TagNumber       NVARCHAR(50)        NOT NULL,
    Name            NVARCHAR(100),
    Breed           NVARCHAR(100),
    DateOfBirth     DATE,
    PurchaseDate    DATE,
    PurchasePrice   DECIMAL(14,2),
    Status          NVARCHAR(50)        NOT NULL DEFAULT 'Milking',  -- Milking, Dry, Pregnant, Calf, Sick, Sold
    ExpectedYieldL  DECIMAL(8,2)        NOT NULL DEFAULT 25,
    IsHighYield     BIT                 NOT NULL DEFAULT 0,          -- 40L+ cows
    Notes           NVARCHAR(1000),
    IsActive        BIT                 NOT NULL DEFAULT 1,
    SoldAt          DATETIME2,
    SoldPrice       DECIMAL(14,2),
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       UNIQUEIDENTIFIER    REFERENCES Users(Id),
    CONSTRAINT UQ_Cows_FarmTag UNIQUE (FarmId, TagNumber)
);
GO

CREATE INDEX IX_Cows_FarmId ON Cows(FarmId);
CREATE INDEX IX_Cows_Status ON Cows(Status);
GO

-- ============================================================
-- LACTATION CYCLES
-- ============================================================
CREATE TABLE LactationCycles (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id),
    CowId           UNIQUEIDENTIFIER    NOT NULL REFERENCES Cows(Id) ON DELETE CASCADE,
    StartDate       DATE                NOT NULL,
    ExpectedEndDate DATE,
    ActualEndDate   DATE,
    CalvingDate     DATE,
    Notes           NVARCHAR(500),
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_LactationCycles_CowId ON LactationCycles(CowId);
GO

-- ============================================================
-- MILK LOGS
-- ============================================================
CREATE TABLE MilkLogs (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id),
    CowId           UNIQUEIDENTIFIER    NOT NULL REFERENCES Cows(Id) ON DELETE CASCADE,
    LogDate         DATE                NOT NULL,
    AMSession       DECIMAL(8,2)        NOT NULL DEFAULT 0,
    PMSession       DECIMAL(8,2)        NOT NULL DEFAULT 0,
    TotalLiters     AS (AMSession + PMSession) PERSISTED,
    Quality         NVARCHAR(50),       -- Fresh, Mastitis, Colostrum
    Notes           NVARCHAR(500),
    RecordedBy      UNIQUEIDENTIFIER    REFERENCES Users(Id),
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    SyncId          UNIQUEIDENTIFIER,   -- offline sync tracking
    CONSTRAINT UQ_MilkLogs_CowDate UNIQUE (CowId, LogDate)
);
GO

CREATE INDEX IX_MilkLogs_FarmId ON MilkLogs(FarmId);
CREATE INDEX IX_MilkLogs_LogDate ON MilkLogs(LogDate);
CREATE INDEX IX_MilkLogs_CowId ON MilkLogs(CowId);
GO

-- ============================================================
-- SALES
-- ============================================================
CREATE TABLE Sales (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id),
    SaleDate        DATE                NOT NULL,
    LitersSold      DECIMAL(10,2)       NOT NULL,
    PricePerLiter   DECIMAL(10,2)       NOT NULL,
    TotalAmount     AS (LitersSold * PricePerLiter) PERSISTED,
    BuyerName       NVARCHAR(200),
    BuyerPhone      NVARCHAR(50),
    PaymentStatus   NVARCHAR(50)        NOT NULL DEFAULT 'Paid',    -- Paid, Pending, Partial
    Notes           NVARCHAR(500),
    RecordedBy      UNIQUEIDENTIFIER    REFERENCES Users(Id),
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    SyncId          UNIQUEIDENTIFIER
);
GO

CREATE INDEX IX_Sales_FarmId ON Sales(FarmId);
CREATE INDEX IX_Sales_SaleDate ON Sales(SaleDate);
GO

-- ============================================================
-- EXPENSES
-- ============================================================
CREATE TABLE Expenses (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id),
    ExpenseDate     DATE                NOT NULL,
    Category        NVARCHAR(100)       NOT NULL,   -- Feed, Labor, Veterinary, Equipment, Utilities, Other
    Description     NVARCHAR(500)       NOT NULL,
    Amount          DECIMAL(14,2)       NOT NULL,
    Vendor          NVARCHAR(200),
    RecordedBy      UNIQUEIDENTIFIER    REFERENCES Users(Id),
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    SyncId          UNIQUEIDENTIFIER
);
GO

CREATE INDEX IX_Expenses_FarmId ON Expenses(FarmId);
CREATE INDEX IX_Expenses_Category ON Expenses(Category);
GO

-- ============================================================
-- HEALTH RECORDS
-- ============================================================
CREATE TABLE HealthRecords (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id),
    CowId           UNIQUEIDENTIFIER    NOT NULL REFERENCES Cows(Id) ON DELETE CASCADE,
    RecordDate      DATE                NOT NULL,
    RecordType      NVARCHAR(100)       NOT NULL,   -- Vaccination, Treatment, Checkup, Deworming
    Condition       NVARCHAR(500),
    Treatment       NVARCHAR(500),
    Medication      NVARCHAR(200),
    DosageML        DECIMAL(8,2),
    VetName         NVARCHAR(200),
    Cost            DECIMAL(10,2)       NOT NULL DEFAULT 0,
    FollowUpDate    DATE,
    IsResolved      BIT                 NOT NULL DEFAULT 0,
    Notes           NVARCHAR(1000),
    RecordedBy      UNIQUEIDENTIFIER    REFERENCES Users(Id),
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    SyncId          UNIQUEIDENTIFIER
);
GO

CREATE INDEX IX_HealthRecords_FarmId ON HealthRecords(FarmId);
CREATE INDEX IX_HealthRecords_CowId ON HealthRecords(CowId);
GO

-- ============================================================
-- FEED INVENTORY
-- ============================================================
CREATE TABLE FeedInventory (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id),
    FeedType        NVARCHAR(100)       NOT NULL,   -- Hay, Concentrate, Silage, Mineral
    QuantityKg      DECIMAL(12,2)       NOT NULL DEFAULT 0,
    UnitCostETB     DECIMAL(10,2)       NOT NULL DEFAULT 0,
    MinStockKg      DECIMAL(12,2)       NOT NULL DEFAULT 100,
    SupplierName    NVARCHAR(200),
    LastRestocked   DATE,
    UpdatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- OFFLINE SYNC QUEUE (server-side record of synced changes)
-- ============================================================
CREATE TABLE SyncQueue (
    Id              UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id),
    UserId          UNIQUEIDENTIFIER    NOT NULL REFERENCES Users(Id),
    DeviceId        NVARCHAR(200),
    Operation       NVARCHAR(20)        NOT NULL,   -- CREATE, UPDATE, DELETE
    EntityType      NVARCHAR(100)       NOT NULL,   -- Cow, MilkLog, Sale, HealthRecord
    EntityId        UNIQUEIDENTIFIER    NOT NULL,
    LocalSyncId     UNIQUEIDENTIFIER,
    Payload         NVARCHAR(MAX),
    ProcessedAt     DATETIME2,
    IsSuccess       BIT,
    ErrorMessage    NVARCHAR(1000),
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_SyncQueue_FarmId ON SyncQueue(FarmId);
CREATE INDEX IX_SyncQueue_ProcessedAt ON SyncQueue(ProcessedAt);
GO

-- ============================================================
-- NOTIFICATIONS
-- ============================================================
CREATE TABLE Notifications (
    Id          UNIQUEIDENTIFIER    DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    FarmId      UNIQUEIDENTIFIER    NOT NULL REFERENCES Farms(Id),
    UserId      UNIQUEIDENTIFIER    REFERENCES Users(Id),
    Type        NVARCHAR(100)       NOT NULL,   -- LowFeed, SickCow, HealthFollowUp, MilkTarget
    Title       NVARCHAR(200)       NOT NULL,
    Message     NVARCHAR(1000)      NOT NULL,
    IsRead      BIT                 NOT NULL DEFAULT 0,
    EntityId    UNIQUEIDENTIFIER,
    EntityType  NVARCHAR(100),
    CreatedAt   DATETIME2           NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ============================================================
-- AUDIT LOG
-- ============================================================
CREATE TABLE AuditLog (
    Id          BIGINT              IDENTITY(1,1) PRIMARY KEY,
    FarmId      UNIQUEIDENTIFIER    NOT NULL,
    UserId      UNIQUEIDENTIFIER,
    Action      NVARCHAR(100)       NOT NULL,
    EntityType  NVARCHAR(100),
    EntityId    UNIQUEIDENTIFIER,
    OldValues   NVARCHAR(MAX),
    NewValues   NVARCHAR(MAX),
    IPAddress   NVARCHAR(50),
    CreatedAt   DATETIME2           NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_AuditLog_FarmId ON AuditLog(FarmId);
CREATE INDEX IX_AuditLog_CreatedAt ON AuditLog(CreatedAt);
GO

-- ============================================================
-- VIEWS
-- ============================================================

-- Daily milk summary per farm
CREATE VIEW vw_DailyMilkSummary AS
SELECT
    ml.FarmId,
    ml.LogDate,
    COUNT(DISTINCT ml.CowId)        AS CowsLogged,
    SUM(ml.TotalLiters)             AS TotalLiters,
    AVG(ml.TotalLiters)             AS AvgLitersPerCow,
    MAX(ml.TotalLiters)             AS MaxLitersPerCow
FROM MilkLogs ml
GROUP BY ml.FarmId, ml.LogDate;
GO

-- Monthly financial summary per farm
CREATE VIEW vw_MonthlyFinancials AS
SELECT
    s.FarmId,
    YEAR(s.SaleDate)                AS Year,
    MONTH(s.SaleDate)               AS Month,
    SUM(s.TotalAmount)              AS TotalRevenue,
    COUNT(s.Id)                     AS SaleCount,
    SUM(s.LitersSold)               AS LitersSold,
    AVG(s.PricePerLiter)            AS AvgPricePerLiter
FROM Sales s
GROUP BY s.FarmId, YEAR(s.SaleDate), MONTH(s.SaleDate);
GO

-- Herd summary per farm
CREATE VIEW vw_HerdSummary AS
SELECT
    FarmId,
    COUNT(*)                        AS TotalCows,
    SUM(CASE WHEN Status = 'Milking'  THEN 1 ELSE 0 END) AS Milking,
    SUM(CASE WHEN Status = 'Dry'      THEN 1 ELSE 0 END) AS Dry,
    SUM(CASE WHEN Status = 'Pregnant' THEN 1 ELSE 0 END) AS Pregnant,
    SUM(CASE WHEN Status = 'Sick'     THEN 1 ELSE 0 END) AS Sick,
    SUM(CASE WHEN Status = 'Calf'     THEN 1 ELSE 0 END) AS Calves,
    AVG(ExpectedYieldL)             AS AvgExpectedYield
FROM Cows
WHERE IsActive = 1
GROUP BY FarmId;
GO

-- ============================================================
-- SEED: Default admin farm for testing
-- ============================================================
DECLARE @FarmId UNIQUEIDENTIFIER = NEWID();
DECLARE @UserId UNIQUEIDENTIFIER = NEWID();

INSERT INTO Farms (Id, Name, OwnerName, Phone, Email, Region)
VALUES (@FarmId, 'Tebe Demo Farm', 'Tebebeyesus', '+251911000000', 'admin@dairyflow.et', 'Addis Ababa');

-- Password: Admin@1234 (bcrypt hash placeholder — replace via app)
INSERT INTO Users (Id, FarmId, FullName, Email, PasswordHash, Role, PreferredLang)
VALUES (@UserId, @FarmId, 'Tebebeyesus', 'admin@dairyflow.et',
        '$2a$12$placeholder_replace_via_registration', 'Owner', 'en');

-- Seed 10 cows
DECLARE @i INT = 1;
WHILE @i <= 10
BEGIN
    INSERT INTO Cows (FarmId, TagNumber, Name, Breed, Status, ExpectedYieldL, PurchasePrice, PurchaseDate)
    VALUES (@FarmId, 'C-' + RIGHT('000' + CAST(@i AS VARCHAR), 3),
            'Cow ' + CAST(@i AS VARCHAR),
            CASE WHEN @i <= 5 THEN 'Holstein' ELSE 'Friesian' END,
            'Milking', 25, 350000, CAST(GETDATE() AS DATE));
    SET @i = @i + 1;
END
GO

PRINT 'DairyFlow schema created successfully.';
GO
