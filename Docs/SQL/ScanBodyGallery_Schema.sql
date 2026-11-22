-- Scan Body Gallery schema for StatsDB

-- Run once on StatsDB before using the Scan Body Gallery module in StatsClient.



IF OBJECT_ID(N'dbo.ScanBodyGalleryEntry', N'U') IS NULL

BEGIN

    CREATE TABLE dbo.ScanBodyGalleryEntry

    (

        EntryId            INT            IDENTITY(1, 1) NOT NULL,

        Brand              NVARCHAR(128)  NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_Brand DEFAULT (N''),

        SystemName         NVARCHAR(128)  NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_SystemName DEFAULT (N''),

        SizeLabel          NVARCHAR(64)   NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_SizeLabel DEFAULT (N''),

        OtherInfo          NVARCHAR(1024) NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_OtherInfo DEFAULT (N''),

        SortOrder          INT            NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_SortOrder DEFAULT (0),

        CreatedUtc         DATETIME2(3)   NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_CreatedUtc DEFAULT (SYSUTCDATETIME()),

        ModifiedUtc        DATETIME2(3)   NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_ModifiedUtc DEFAULT (SYSUTCDATETIME()),

        CreatedByMachine   NVARCHAR(128)  NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_CreatedByMachine DEFAULT (N''),

        ModifiedByMachine  NVARCHAR(128)  NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_ModifiedByMachine DEFAULT (N''),

        IsDeleted          BIT            NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_IsDeleted DEFAULT (0),

        CONSTRAINT PK_ScanBodyGalleryEntry PRIMARY KEY CLUSTERED (EntryId)

    );



    CREATE NONCLUSTERED INDEX IX_ScanBodyGalleryEntry_ModifiedUtc

        ON dbo.ScanBodyGalleryEntry (ModifiedUtc DESC)

        WHERE IsDeleted = 0;



    CREATE NONCLUSTERED INDEX IX_ScanBodyGalleryEntry_Brand

        ON dbo.ScanBodyGalleryEntry (Brand)

        WHERE IsDeleted = 0;

END

GO



IF COL_LENGTH(N'dbo.ScanBodyGalleryEntry', N'IsDeleted') IS NULL

BEGIN

    ALTER TABLE dbo.ScanBodyGalleryEntry

        ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ScanBodyGalleryEntry_IsDeleted DEFAULT (0);

END

GO



IF OBJECT_ID(N'dbo.ScanBodyGalleryImage', N'U') IS NULL

BEGIN

    CREATE TABLE dbo.ScanBodyGalleryImage

    (

        ImageId        INT            IDENTITY(1, 1) NOT NULL,

        EntryId        INT            NOT NULL,

        FileName       NVARCHAR(260)  NOT NULL CONSTRAINT DF_ScanBodyGalleryImage_FileName DEFAULT (N'image.png'),

        ContentType    NVARCHAR(64)   NOT NULL CONSTRAINT DF_ScanBodyGalleryImage_ContentType DEFAULT (N'image/png'),

        ImageData      VARBINARY(MAX) NOT NULL,

        ThumbnailData  VARBINARY(MAX) NULL,

        SortOrder      INT            NOT NULL CONSTRAINT DF_ScanBodyGalleryImage_SortOrder DEFAULT (0),

        CreatedUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_ScanBodyGalleryImage_CreatedUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_ScanBodyGalleryImage PRIMARY KEY CLUSTERED (ImageId),

        CONSTRAINT FK_ScanBodyGalleryImage_Entry FOREIGN KEY (EntryId) REFERENCES dbo.ScanBodyGalleryEntry (EntryId) ON DELETE CASCADE

    );



    CREATE NONCLUSTERED INDEX IX_ScanBodyGalleryImage_EntryId

        ON dbo.ScanBodyGalleryImage (EntryId, SortOrder);

END

GO



IF OBJECT_ID(N'dbo.ScanBodyGalleryTag', N'U') IS NULL

BEGIN

    CREATE TABLE dbo.ScanBodyGalleryTag

    (

        TagId      INT           IDENTITY(1, 1) NOT NULL,

        Name       NVARCHAR(64)  NOT NULL,

        CreatedUtc DATETIME2(3)  NOT NULL CONSTRAINT DF_ScanBodyGalleryTag_CreatedUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_ScanBodyGalleryTag PRIMARY KEY CLUSTERED (TagId),

        CONSTRAINT UQ_ScanBodyGalleryTag_Name UNIQUE (Name)

    );

END

GO



IF OBJECT_ID(N'dbo.ScanBodyGalleryTagMap', N'U') IS NULL

BEGIN

    CREATE TABLE dbo.ScanBodyGalleryTagMap

    (

        EntryId INT NOT NULL,

        TagId   INT NOT NULL,

        CONSTRAINT PK_ScanBodyGalleryTagMap PRIMARY KEY CLUSTERED (EntryId, TagId),

        CONSTRAINT FK_ScanBodyGalleryTagMap_Entry FOREIGN KEY (EntryId) REFERENCES dbo.ScanBodyGalleryEntry (EntryId) ON DELETE CASCADE,

        CONSTRAINT FK_ScanBodyGalleryTagMap_Tag FOREIGN KEY (TagId) REFERENCES dbo.ScanBodyGalleryTag (TagId) ON DELETE CASCADE

    );



    CREATE NONCLUSTERED INDEX IX_ScanBodyGalleryTagMap_TagId

        ON dbo.ScanBodyGalleryTagMap (TagId);

END

GO

