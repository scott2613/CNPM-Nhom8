CREATE TABLE [DeviceSessions] (
    [Id] int NOT NULL IDENTITY,
    [DeviceFingerprint] nvarchar(100) NOT NULL,
    [IpAddress] nvarchar(50) NOT NULL,
    [DeviceName] nvarchar(100) NOT NULL,
    [Platform] nvarchar(50) NOT NULL,
    [Browser] nvarchar(50) NOT NULL,
    [PreferredLanguage] nvarchar(10) NOT NULL DEFAULT N'vi',
    [TotalScans] int NOT NULL,
    [IsBlocked] bit NOT NULL,
    [LastActiveAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_DeviceSessions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Pois] (
    [Id] int NOT NULL IDENTITY,
    [RowVersion] rowversion NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Floor] int NOT NULL DEFAULT 0,
    [Location] geography NOT NULL,
    [TriggerRadiusMeters] float NOT NULL DEFAULT 5.0E0,
    [ImageUrl] nvarchar(500) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Pois] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [PoiTranslations] (
    [Id] int NOT NULL IDENTITY,
    [PoiId] int NOT NULL,
    [LanguageCode] nvarchar(10) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NULL,
    [AudioUrl] nvarchar(500) NOT NULL,
    [DurationSeconds] int NOT NULL DEFAULT 30,
    CONSTRAINT [PK_PoiTranslations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PoiTranslations_Pois_PoiId] FOREIGN KEY ([PoiId]) REFERENCES [Pois] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [QrCodes] (
    [Id] int NOT NULL IDENTITY,
    [PoiId] int NULL,
    [QrToken] nvarchar(64) NOT NULL,
    [ScanCount] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_QrCodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QrCodes_Pois_PoiId] FOREIGN KEY ([PoiId]) REFERENCES [Pois] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [QrScanLogs] (
    [Id] int NOT NULL IDENTITY,
    [EventId] uniqueidentifier NULL,
    [EventType] nvarchar(20) NOT NULL,
    [VisitorId] nvarchar(100) NULL,
    [PoiId] int NULL,
    [LanguageCode] nvarchar(10) NOT NULL,
    [IsUtc] bit NOT NULL,
    [QrToken] nvarchar(64) NOT NULL,
    [PoiCode] nvarchar(50) NULL,
    [ScannedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [IpAddress] nvarchar(45) NULL,
    [UserAgent] nvarchar(500) NULL,
    CONSTRAINT [PK_QrScanLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QrScanLogs_Pois_PoiId] FOREIGN KEY ([PoiId]) REFERENCES [Pois] ([Id]) ON DELETE SET NULL
);
GO


CREATE UNIQUE INDEX [IX_DeviceSessions_DeviceFingerprint] ON [DeviceSessions] ([DeviceFingerprint]);
GO


CREATE INDEX [IX_DeviceSessions_LastActiveAt] ON [DeviceSessions] ([LastActiveAt]);
GO


CREATE UNIQUE INDEX [IX_Pois_Code] ON [Pois] ([Code]);
GO


CREATE INDEX [IX_Pois_Floor] ON [Pois] ([Floor]);
GO


CREATE INDEX [IX_Pois_IsActive] ON [Pois] ([IsActive]);
GO


CREATE UNIQUE INDEX [IX_PoiTranslations_PoiId_LanguageCode] ON [PoiTranslations] ([PoiId], [LanguageCode]);
GO


CREATE UNIQUE INDEX [IX_QrCodes_PoiId] ON [QrCodes] ([PoiId]) WHERE [PoiId] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_QrCodes_QrToken] ON [QrCodes] ([QrToken]);
GO


CREATE UNIQUE INDEX [IX_QrScanLogs_EventId] ON [QrScanLogs] ([EventId]) WHERE [EventId] IS NOT NULL;
GO


CREATE INDEX [IX_QrScanLogs_EventType_ScannedAt] ON [QrScanLogs] ([EventType], [ScannedAt]);
GO


CREATE INDEX [IX_QrScanLogs_PoiId] ON [QrScanLogs] ([PoiId]);
GO


CREATE INDEX [IX_QrScanLogs_QrToken] ON [QrScanLogs] ([QrToken]);
GO


CREATE INDEX [IX_QrScanLogs_ScannedAt] ON [QrScanLogs] ([ScannedAt]);
GO


CREATE INDEX [IX_QrScanLogs_VisitorId_ScannedAt] ON [QrScanLogs] ([VisitorId], [ScannedAt]);
GO


