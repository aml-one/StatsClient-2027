-- Knowledge Base schema for StatsDB
-- Run once on StatsDB before using the Knowledge Base module in StatsClient.

IF OBJECT_ID(N'dbo.KnowledgeBaseCategory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeBaseCategory
    (
        CategoryId   INT            IDENTITY(1, 1) NOT NULL,
        Name         NVARCHAR(128)  NOT NULL,
        SortOrder    INT            NOT NULL CONSTRAINT DF_KnowledgeBaseCategory_SortOrder DEFAULT (0),
        CreatedUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_KnowledgeBaseCategory_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_KnowledgeBaseCategory PRIMARY KEY CLUSTERED (CategoryId),
        CONSTRAINT UQ_KnowledgeBaseCategory_Name UNIQUE (Name)
    );
END
GO

IF OBJECT_ID(N'dbo.KnowledgeBaseCard', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeBaseCard
    (
        CardId             INT            IDENTITY(1, 1) NOT NULL,
        Title              NVARCHAR(256)  NOT NULL CONSTRAINT DF_KnowledgeBaseCard_Title DEFAULT (N''),
        BodyText           NVARCHAR(MAX)  NOT NULL CONSTRAINT DF_KnowledgeBaseCard_BodyText DEFAULT (N''),
        CategoryId         INT            NULL,
        CreatedUtc         DATETIME2(3)   NOT NULL CONSTRAINT DF_KnowledgeBaseCard_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedUtc        DATETIME2(3)   NOT NULL CONSTRAINT DF_KnowledgeBaseCard_ModifiedUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByMachine   NVARCHAR(128)  NOT NULL CONSTRAINT DF_KnowledgeBaseCard_CreatedByMachine DEFAULT (N''),
        ModifiedByMachine  NVARCHAR(128)  NOT NULL CONSTRAINT DF_KnowledgeBaseCard_ModifiedByMachine DEFAULT (N''),
        IsDeleted          BIT            NOT NULL CONSTRAINT DF_KnowledgeBaseCard_IsDeleted DEFAULT (0),
        CONSTRAINT PK_KnowledgeBaseCard PRIMARY KEY CLUSTERED (CardId),
        CONSTRAINT FK_KnowledgeBaseCard_Category FOREIGN KEY (CategoryId) REFERENCES dbo.KnowledgeBaseCategory (CategoryId)
    );

    CREATE NONCLUSTERED INDEX IX_KnowledgeBaseCard_ModifiedUtc
        ON dbo.KnowledgeBaseCard (ModifiedUtc DESC)
        WHERE IsDeleted = 0;

    CREATE NONCLUSTERED INDEX IX_KnowledgeBaseCard_CategoryId
        ON dbo.KnowledgeBaseCard (CategoryId)
        WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID(N'dbo.KnowledgeBaseCardLink', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeBaseCardLink
    (
        LinkId    INT            IDENTITY(1, 1) NOT NULL,
        CardId    INT            NOT NULL,
        Label     NVARCHAR(256)  NOT NULL CONSTRAINT DF_KnowledgeBaseCardLink_Label DEFAULT (N''),
        Url       NVARCHAR(2048) NOT NULL,
        SortOrder INT            NOT NULL CONSTRAINT DF_KnowledgeBaseCardLink_SortOrder DEFAULT (0),
        CONSTRAINT PK_KnowledgeBaseCardLink PRIMARY KEY CLUSTERED (LinkId),
        CONSTRAINT FK_KnowledgeBaseCardLink_Card FOREIGN KEY (CardId) REFERENCES dbo.KnowledgeBaseCard (CardId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_KnowledgeBaseCardLink_CardId
        ON dbo.KnowledgeBaseCardLink (CardId, SortOrder);
END
GO

IF OBJECT_ID(N'dbo.KnowledgeBaseCardImage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeBaseCardImage
    (
        ImageId        INT            IDENTITY(1, 1) NOT NULL,
        CardId         INT            NOT NULL,
        FileName       NVARCHAR(260)  NOT NULL CONSTRAINT DF_KnowledgeBaseCardImage_FileName DEFAULT (N'image.png'),
        ContentType    NVARCHAR(64)   NOT NULL CONSTRAINT DF_KnowledgeBaseCardImage_ContentType DEFAULT (N'image/png'),
        ImageData      VARBINARY(MAX) NOT NULL,
        ThumbnailData  VARBINARY(MAX) NULL,
        SortOrder      INT            NOT NULL CONSTRAINT DF_KnowledgeBaseCardImage_SortOrder DEFAULT (0),
        CreatedUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_KnowledgeBaseCardImage_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_KnowledgeBaseCardImage PRIMARY KEY CLUSTERED (ImageId),
        CONSTRAINT FK_KnowledgeBaseCardImage_Card FOREIGN KEY (CardId) REFERENCES dbo.KnowledgeBaseCard (CardId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_KnowledgeBaseCardImage_CardId
        ON dbo.KnowledgeBaseCardImage (CardId, SortOrder);
END
GO

IF OBJECT_ID(N'dbo.KnowledgeBaseTag', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeBaseTag
    (
        TagId       INT           IDENTITY(1, 1) NOT NULL,
        TagName     NVARCHAR(64)  NOT NULL,
        UsageCount  INT           NOT NULL CONSTRAINT DF_KnowledgeBaseTag_UsageCount DEFAULT (0),
        CreatedUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_KnowledgeBaseTag_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_KnowledgeBaseTag PRIMARY KEY CLUSTERED (TagId),
        CONSTRAINT UQ_KnowledgeBaseTag_TagName UNIQUE (TagName)
    );

    CREATE NONCLUSTERED INDEX IX_KnowledgeBaseTag_UsageCount
        ON dbo.KnowledgeBaseTag (UsageCount DESC, TagName);
END
GO

IF OBJECT_ID(N'dbo.KnowledgeBaseCardTag', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeBaseCardTag
    (
        CardId INT NOT NULL,
        TagId  INT NOT NULL,
        CONSTRAINT PK_KnowledgeBaseCardTag PRIMARY KEY CLUSTERED (CardId, TagId),
        CONSTRAINT FK_KnowledgeBaseCardTag_Card FOREIGN KEY (CardId) REFERENCES dbo.KnowledgeBaseCard (CardId) ON DELETE CASCADE,
        CONSTRAINT FK_KnowledgeBaseCardTag_Tag FOREIGN KEY (TagId) REFERENCES dbo.KnowledgeBaseTag (TagId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_KnowledgeBaseCardTag_TagId
        ON dbo.KnowledgeBaseCardTag (TagId, CardId);
END
GO

IF OBJECT_ID(N'dbo.KnowledgeBaseCardBackup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeBaseCardBackup
    (
        BackupId      INT            IDENTITY(1, 1) NOT NULL,
        CardId        INT            NOT NULL,
        MachineName   NVARCHAR(128)  NOT NULL,
        SnapshotJson  NVARCHAR(MAX)  NOT NULL,
        BackedUpUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_KnowledgeBaseCardBackup_BackedUpUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_KnowledgeBaseCardBackup PRIMARY KEY CLUSTERED (BackupId),
        CONSTRAINT FK_KnowledgeBaseCardBackup_Card FOREIGN KEY (CardId) REFERENCES dbo.KnowledgeBaseCard (CardId) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_KnowledgeBaseCardBackup_CardMachine
        ON dbo.KnowledgeBaseCardBackup (CardId, MachineName);
END
GO
