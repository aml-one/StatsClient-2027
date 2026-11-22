-- Useful Websites schema for StatsDB
-- Run once on StatsDB before using the Useful Websites module in StatsClient.

IF OBJECT_ID(N'dbo.UsefulWebsite', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsefulWebsite
    (
        WebsiteId          INT            IDENTITY(1, 1) NOT NULL,
        Title              NVARCHAR(256)  NOT NULL,
        Url                NVARCHAR(2048) NOT NULL,
        SortOrder          INT            NOT NULL CONSTRAINT DF_UsefulWebsite_SortOrder DEFAULT (0),
        CreatedUtc         DATETIME2(3)   NOT NULL CONSTRAINT DF_UsefulWebsite_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedUtc        DATETIME2(3)   NOT NULL CONSTRAINT DF_UsefulWebsite_ModifiedUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByMachine   NVARCHAR(128)  NOT NULL CONSTRAINT DF_UsefulWebsite_CreatedByMachine DEFAULT (N''),
        ModifiedByMachine  NVARCHAR(128)  NOT NULL CONSTRAINT DF_UsefulWebsite_ModifiedByMachine DEFAULT (N''),
        IsDeleted          BIT            NOT NULL CONSTRAINT DF_UsefulWebsite_IsDeleted DEFAULT (0),
        CONSTRAINT PK_UsefulWebsite PRIMARY KEY CLUSTERED (WebsiteId)
    );

    CREATE NONCLUSTERED INDEX IX_UsefulWebsite_Title
        ON dbo.UsefulWebsite (Title)
        WHERE IsDeleted = 0;

    CREATE NONCLUSTERED INDEX IX_UsefulWebsite_ModifiedUtc
        ON dbo.UsefulWebsite (ModifiedUtc DESC)
        WHERE IsDeleted = 0;
END
GO

IF COL_LENGTH(N'dbo.UsefulWebsite', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.UsefulWebsite
        ADD IsDeleted BIT NOT NULL CONSTRAINT DF_UsefulWebsite_IsDeleted DEFAULT (0);
END
GO

IF OBJECT_ID(N'dbo.UsefulWebsiteTag', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsefulWebsiteTag
    (
        TagId      INT           IDENTITY(1, 1) NOT NULL,
        Name       NVARCHAR(64)  NOT NULL,
        CreatedUtc DATETIME2(3)  NOT NULL CONSTRAINT DF_UsefulWebsiteTag_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_UsefulWebsiteTag PRIMARY KEY CLUSTERED (TagId),
        CONSTRAINT UQ_UsefulWebsiteTag_Name UNIQUE (Name)
    );
END
GO

IF OBJECT_ID(N'dbo.UsefulWebsiteTagMap', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsefulWebsiteTagMap
    (
        WebsiteId INT NOT NULL,
        TagId     INT NOT NULL,
        CONSTRAINT PK_UsefulWebsiteTagMap PRIMARY KEY CLUSTERED (WebsiteId, TagId),
        CONSTRAINT FK_UsefulWebsiteTagMap_Website FOREIGN KEY (WebsiteId) REFERENCES dbo.UsefulWebsite (WebsiteId) ON DELETE CASCADE,
        CONSTRAINT FK_UsefulWebsiteTagMap_Tag FOREIGN KEY (TagId) REFERENCES dbo.UsefulWebsiteTag (TagId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_UsefulWebsiteTagMap_TagId
        ON dbo.UsefulWebsiteTagMap (TagId);
END
GO
