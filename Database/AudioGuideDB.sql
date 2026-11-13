-- AudioGuideDB.sql | SQL Server / SSMS
-- Tạo database nếu chưa có, tạo bảng khi database trống và nạp bộ mẫu 8 POI × 5 ngôn ngữ.
-- DB đã có dữ liệu: chỉ nâng cấp cấu trúc khi bạn bật @UpgradeLegacy.
-- Bật @RefreshDemoNarratives nếu muốn thay nội dung cho đúng các mã mẫu 1–8.
-- Sao lưu AudioGuideDb trước khi bật nâng cấp hoặc thay bài. Không xóa bảng hay đặt lại database.
USE [master];
GO
IF DB_ID(N'AudioGuideDb') IS NULL
    EXEC(N'CREATE DATABASE [AudioGuideDb]');
GO
USE [AudioGuideDb];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @UpgradeLegacy bit = 0;          -- 1: cho phép nâng cấp DB cũ sau khi đã sao lưu
DECLARE @LoadDemoWhenEmpty bit = 1;      -- 0: bỏ qua nạp dữ liệu mẫu
DECLARE @RefreshDemoNarratives bit = 0;  -- 1: thay tiêu đề và bài mẫu của mã 1–8

IF DB_NAME() <> N'AudioGuideDb'
    THROW 51010, N'Hãy kết nối đúng AudioGuideDb.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'AudioGuideSchema',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=30000;
    IF @LockResult < 0 THROW 51011, N'Không lấy được khóa nâng cấp. Hãy thử lại.', 1;

    IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
        CREATE TABLE dbo.SchemaVersions (
            Name nvarchar(200) NOT NULL PRIMARY KEY,
            Sha256 char(64) NOT NULL,
            AppliedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
        );

    IF OBJECT_ID(N'dbo.Pois', N'U') IS NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0 AND object_id <> OBJECT_ID(N'dbo.SchemaVersions'))
            THROW 51012, N'Database đã có bảng nhưng thiếu Pois. Dừng để tránh ghi nhầm.', 1;
        EXEC sys.sp_executesql N'CREATE TABLE [DeviceSessions] (
    [Id] int NOT NULL IDENTITY,
    [DeviceFingerprint] nvarchar(100) NOT NULL,
    [IpAddress] nvarchar(50) NOT NULL,
    [DeviceName] nvarchar(100) NOT NULL,
    [Platform] nvarchar(50) NOT NULL,
    [Browser] nvarchar(50) NOT NULL,
    [PreferredLanguage] nvarchar(10) NOT NULL DEFAULT N''vi'',
    [TotalScans] int NOT NULL,
    [IsBlocked] bit NOT NULL,
    [LastActiveAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_DeviceSessions] PRIMARY KEY ([Id])
);';
        EXEC sys.sp_executesql N'CREATE TABLE [Pois] (
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
);';
        EXEC sys.sp_executesql N'CREATE TABLE [PoiTranslations] (
    [Id] int NOT NULL IDENTITY,
    [PoiId] int NOT NULL,
    [LanguageCode] nvarchar(10) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NULL,
    [AudioUrl] nvarchar(500) NOT NULL,
    [DurationSeconds] int NOT NULL DEFAULT 30,
    CONSTRAINT [PK_PoiTranslations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PoiTranslations_Pois_PoiId] FOREIGN KEY ([PoiId]) REFERENCES [Pois] ([Id]) ON DELETE CASCADE
);';
        EXEC sys.sp_executesql N'CREATE TABLE [QrCodes] (
    [Id] int NOT NULL IDENTITY,
    [PoiId] int NULL,
    [QrToken] nvarchar(64) NOT NULL,
    [ScanCount] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_QrCodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QrCodes_Pois_PoiId] FOREIGN KEY ([PoiId]) REFERENCES [Pois] ([Id]) ON DELETE SET NULL
);';
        EXEC sys.sp_executesql N'CREATE TABLE [QrScanLogs] (
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
);';
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [IX_DeviceSessions_DeviceFingerprint] ON [DeviceSessions] ([DeviceFingerprint]);';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_DeviceSessions_LastActiveAt] ON [DeviceSessions] ([LastActiveAt]);';
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [IX_Pois_Code] ON [Pois] ([Code]);';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_Pois_Floor] ON [Pois] ([Floor]);';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_Pois_IsActive] ON [Pois] ([IsActive]);';
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [IX_PoiTranslations_PoiId_LanguageCode] ON [PoiTranslations] ([PoiId], [LanguageCode]);';
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [IX_QrCodes_PoiId] ON [QrCodes] ([PoiId]) WHERE [PoiId] IS NOT NULL;';
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [IX_QrCodes_QrToken] ON [QrCodes] ([QrToken]);';
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [IX_QrScanLogs_EventId] ON [QrScanLogs] ([EventId]) WHERE [EventId] IS NOT NULL;';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_QrScanLogs_EventType_ScannedAt] ON [QrScanLogs] ([EventType], [ScannedAt]);';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_QrScanLogs_PoiId] ON [QrScanLogs] ([PoiId]);';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_QrScanLogs_QrToken] ON [QrScanLogs] ([QrToken]);';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_QrScanLogs_ScannedAt] ON [QrScanLogs] ([ScannedAt]);';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_QrScanLogs_VisitorId_ScannedAt] ON [QrScanLogs] ([VisitorId], [ScannedAt]);';
        INSERT dbo.SchemaVersions(Name, Sha256) VALUES (N'001_InitialSchema.sql', 'BFD0EEB2955A5D252194A756C1A82DA7A7A364A18C7EDB45D82F23130B02DBE3');
    END
    ELSE IF OBJECT_ID(N'dbo.PoiTranslations', N'U') IS NULL
         OR OBJECT_ID(N'dbo.QrCodes', N'U') IS NULL
         OR OBJECT_ID(N'dbo.DeviceSessions', N'U') IS NULL
         OR OBJECT_ID(N'dbo.QrScanLogs', N'U') IS NULL
        THROW 51013, N'Database thiếu bảng AudioGuide. Kiểm tra schema trước khi tiếp tục.', 1;
    ELSE IF COL_LENGTH('dbo.Pois','RowVersion') IS NULL
         OR COL_LENGTH('dbo.QrScanLogs','EventId') IS NULL
         OR COL_LENGTH('dbo.QrScanLogs','EventType') IS NULL
         OR COL_LENGTH('dbo.QrScanLogs','VisitorId') IS NULL
         OR COL_LENGTH('dbo.QrScanLogs','PoiId') IS NULL
         OR COL_LENGTH('dbo.QrScanLogs','LanguageCode') IS NULL
         OR COL_LENGTH('dbo.QrScanLogs','IsUtc') IS NULL
         OR COL_LENGTH('dbo.DeviceSessions','PreferredLanguage') IS NULL
    BEGIN
        IF @UpgradeLegacy = 0
            THROW 51014, N'Database đang dùng schema cũ. Hãy sao lưu rồi đặt @UpgradeLegacy = 1 để nâng cấp.', 1;
        IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Name=N'002_UpgradeLegacy.sql')
            THROW 51015, N'Database báo đã nâng cấp nhưng thiếu cột. Cần đối chiếu schema trước.', 1;
        EXEC sys.sp_executesql N'SET XACT_ABORT ON;
IF COL_LENGTH(''dbo.Pois'',''RowVersion'') IS NULL ALTER TABLE dbo.Pois ADD RowVersion rowversion;
IF COL_LENGTH(''dbo.QrScanLogs'',''EventId'') IS NULL ALTER TABLE dbo.QrScanLogs ADD EventId uniqueidentifier NULL;
IF COL_LENGTH(''dbo.QrScanLogs'',''EventType'') IS NULL ALTER TABLE dbo.QrScanLogs ADD EventType nvarchar(20) NOT NULL CONSTRAINT DF_Log_EventType DEFAULT N''Legacy'';
IF COL_LENGTH(''dbo.QrScanLogs'',''VisitorId'') IS NULL ALTER TABLE dbo.QrScanLogs ADD VisitorId nvarchar(100) NULL;
IF COL_LENGTH(''dbo.QrScanLogs'',''PoiId'') IS NULL ALTER TABLE dbo.QrScanLogs ADD PoiId int NULL;
IF COL_LENGTH(''dbo.QrScanLogs'',''LanguageCode'') IS NULL ALTER TABLE dbo.QrScanLogs ADD LanguageCode nvarchar(10) NOT NULL CONSTRAINT DF_Log_Language DEFAULT N''vi'';
IF COL_LENGTH(''dbo.QrScanLogs'',''IsUtc'') IS NULL ALTER TABLE dbo.QrScanLogs ADD IsUtc bit NOT NULL CONSTRAINT DF_Log_IsUtc DEFAULT 0;
IF COL_LENGTH(''dbo.DeviceSessions'',''PreferredLanguage'') IS NULL ALTER TABLE dbo.DeviceSessions ADD PreferredLanguage nvarchar(10) NULL;';
        EXEC sys.sp_executesql N'UPDATE dbo.DeviceSessions SET PreferredLanguage=N''vi'' WHERE PreferredLanguage IS NULL;
ALTER TABLE dbo.DeviceSessions ALTER COLUMN PreferredLanguage nvarchar(10) NOT NULL;
-- Abort on inconsistent historical data; do not silently delete duplicates.
IF EXISTS(SELECT PoiId FROM dbo.QrCodes WHERE PoiId IS NOT NULL GROUP BY PoiId HAVING COUNT(*)>1) THROW 51001,''Duplicate QR PoiId; reconcile before upgrading'',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.QrCodes'') AND name=''IX_QrCodes_PoiId'') CREATE UNIQUE INDEX IX_QrCodes_PoiId ON dbo.QrCodes(PoiId) WHERE PoiId IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.QrScanLogs'') AND name=''IX_QrScanLogs_EventId'') CREATE UNIQUE INDEX IX_QrScanLogs_EventId ON dbo.QrScanLogs(EventId) WHERE EventId IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.QrScanLogs'') AND name=''IX_QrScanLogs_EventType_ScannedAt'') CREATE INDEX IX_QrScanLogs_EventType_ScannedAt ON dbo.QrScanLogs(EventType,ScannedAt);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(''dbo.QrScanLogs'') AND name=''IX_QrScanLogs_VisitorId_ScannedAt'') CREATE INDEX IX_QrScanLogs_VisitorId_ScannedAt ON dbo.QrScanLogs(VisitorId,ScannedAt);
DECLARE @drop nvarchar(max)=N'''';
SELECT @drop+=N''ALTER TABLE dbo.QrCodes DROP CONSTRAINT ''+QUOTENAME(name)+N'';'' FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(''dbo.QrCodes'') AND referenced_object_id=OBJECT_ID(''dbo.Pois'');
EXEC sp_executesql @drop;
ALTER TABLE dbo.QrCodes ADD CONSTRAINT FK_QrCodes_Pois_Upgrade FOREIGN KEY(PoiId) REFERENCES dbo.Pois(Id) ON DELETE SET NULL;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=''FK_QrScanLogs_Pois_PoiId'') ALTER TABLE dbo.QrScanLogs ADD CONSTRAINT FK_QrScanLogs_Pois_PoiId FOREIGN KEY(PoiId) REFERENCES dbo.Pois(Id) ON DELETE SET NULL;
UPDATE dbo.QrCodes SET PoiId=NULL WHERE QrToken=N''MUSEUM-MAIN-ENTRY'';
UPDATE dbo.QrCodes SET IsActive=0 WHERE QrToken<>N''MUSEUM-MAIN-ENTRY'';
IF NOT EXISTS(SELECT 1 FROM dbo.QrCodes WHERE QrToken=N''MUSEUM-MAIN-ENTRY'') INSERT dbo.QrCodes(PoiId,QrToken,ScanCount,IsActive) VALUES(NULL,N''MUSEUM-MAIN-ENTRY'',0,1);
-- Existing gate IsActive is preserved: an intentionally disabled gate is not silently enabled.';
        INSERT dbo.SchemaVersions(Name, Sha256) VALUES (N'002_UpgradeLegacy.sql', 'F0838C8EDBE2BCCDC29C0D3F0752FDCFAD7703FCA128C094F9000E67D9BE1C18');
    END;

    -- Chỉ một token cổng được phép hoạt động. Giữ QR cũ dưới dạng bản ghi vô hiệu.
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.QrCodes') AND name=N'CK_QrCodes_OnlyActiveGate')
    BEGIN
        IF @UpgradeLegacy = 1
            EXEC sys.sp_executesql N'
                UPDATE dbo.QrCodes SET PoiId = NULL WHERE QrToken = N''MUSEUM-MAIN-ENTRY'' AND PoiId IS NOT NULL;
                UPDATE dbo.QrCodes SET IsActive = 0 WHERE QrToken <> N''MUSEUM-MAIN-ENTRY'' AND IsActive = 1;';
        EXEC sys.sp_executesql N'
            IF EXISTS (SELECT 1 FROM dbo.QrCodes WHERE IsActive = 1 AND (QrToken <> N''MUSEUM-MAIN-ENTRY'' OR PoiId IS NOT NULL))
                THROW 51018, N''Còn mã QR cũ đang hoạt động. Sao lưu rồi bật @UpgradeLegacy để chuẩn hóa về một QR cổng.'', 1;
            ALTER TABLE dbo.QrCodes WITH CHECK ADD CONSTRAINT CK_QrCodes_OnlyActiveGate
            CHECK (IsActive = 0 OR (QrToken = N''MUSEUM-MAIN-ENTRY'' AND PoiId IS NULL));';
    END;

    -- Mã cổng cần có cả khi bỏ qua dữ liệu mẫu. Giữ nguyên mã đã bị tắt chủ động.
    EXEC sys.sp_executesql N'
        IF NOT EXISTS (SELECT 1 FROM dbo.QrCodes WHERE QrToken = N''MUSEUM-MAIN-ENTRY'')
            INSERT dbo.QrCodes (PoiId, QrToken, ScanCount, IsActive)
            VALUES (NULL, N''MUSEUM-MAIN-ENTRY'', 0, 1);';

    -- Truy vấn đếm lượt nghe theo điểm chỉ đọc các sự kiện PlayStarted hợp lệ.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.QrScanLogs') AND name=N'IX_QrScanLogs_Poi_PlayStarted')
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_QrScanLogs_Poi_PlayStarted ON dbo.QrScanLogs(PoiId)
            WHERE EventType=N''PlayStarted'' AND IsUtc=1 AND PoiId IS NOT NULL;';

    IF @LoadDemoWhenEmpty = 1
    BEGIN
        EXEC sys.sp_executesql N'SET XACT_ABORT ON;

IF NOT EXISTS(SELECT 1 FROM dbo.Pois)
BEGIN
SET IDENTITY_INSERT [dbo].[Pois] ON;

INSERT INTO [dbo].[Pois] ([Id], [Code], [Floor], [TriggerRadiusMeters], [Location], [ImageUrl], [IsActive], [CreatedAt])
VALUES
-- #1. Mô hình Trại giam Chuồng Cọp (Tầng Trệt)
(1, N''1'', 0, 5, geography::Point(10.77896, 106.69192, 4326), 
 N''https://tse3.mm.bing.net/th/id/OIP.f_-6pnnRSM2HDc_8WJJwhgHaFj?r=0&rs=1&pid=ImgDetMain&o=7&rm=3'', 1, GETUTCDATE()),

-- #2. Khu trưng bày Máy bay Quân sự (Tầng Trệt)
(2, N''2'', 0, 6, geography::Point(10.77948, 106.69235, 4326), 
 N''https://tse2.mm.bing.net/th/id/OIP.gxBtJu_MQ0T8F3kbZfVgRAHaFU?r=0&rs=1&pid=ImgDetMain&o=7&rm=3'', 1, GETUTCDATE()),

-- #7. Ẩm thực Nam Bộ thời kháng chiến (Tầng Trệt)
(7, N''7'', 0, 5, geography::Point(10.77918, 106.69258, 4326), 
 N''https://res.cloudinary.com/dqoitnjnk/image/upload/v1763870787/large_1_2b308f1ce2.jpg'', 1, GETUTCDATE()),

-- #3. Chuyên đề Hậu quả Chất độc Da cam (Tầng 1)
(3, N''3'', 1, 5, geography::Point(10.77920, 106.69205, 4326), 
 N''https://res.klook.com/images/fl_lossy.progressive,q_65/c_fill,w_1000,h_563/w_61,x_11,y_11,g_south_west,l_Klook_water_br_trans_yhcmh3/activities/xxk2cigshdyoagir9ajp/V%C3%A9B%E1%BA%A3oT%C3%A0ngCh%E1%BB%A9ngT%C3%ADchChi%E1%BA%BFnTranhTPH%E1%BB%93Ch%C3%ADMinh.jpg'', 1, GETUTCDATE()),

-- #4. Phòng trưng bày Tội ác Chiến tranh (Tầng 1)
(4, N''4'', 1, 5, geography::Point(10.77935, 106.69212, 4326), 
 N''https://res.cloudinary.com/dqoitnjnk/image/upload/v1763866903/large_toiac3_476f90003b.webp'', 1, GETUTCDATE()),

-- #5. Phòng Sự thật Lịch sử (Tầng 2)
(5, N''5'', 2, 5, geography::Point(10.77925, 106.69218, 4326), 
 N''https://res.cloudinary.com/dqoitnjnk/image/upload/v1763869588/large_SUTHAT_5_ac1329a0e3.jpg'', 1, GETUTCDATE()),

-- #6. Nhân dân Thế giới Hỗ trợ Việt Nam (Tầng 2)
(6, N''6'', 2, 5, geography::Point(10.77936, 106.69238, 4326), 
 N''https://res.cloudinary.com/dqoitnjnk/image/upload/v1763868983/large_THEGIOI_b65216723a.webp'', 1, GETUTCDATE()),

-- #8. Nguyễn Thị Bình – Trí tuệ, Bản lĩnh và Nhân cách phụ nữ Việt Nam (Tầng 2)
(8, N''8'', 2, 5, geography::Point(10.77916, 106.69228, 4326), 
 N''https://baotangchungtichchientranh.vn/api/uploads/618064443_1584125866445712_8941509365781489122_n_a450d12105.jpg'', 1, GETUTCDATE());

SET IDENTITY_INSERT [dbo].[Pois] OFF;

INSERT INTO [dbo].[PoiTranslations] ([PoiId], [LanguageCode], [Title], [Description], [AudioUrl], [DurationSeconds])
VALUES
(1, N''vi'', N''Mô hình Trại giam Chuồng Cọp'', N''Trước mặt quý khách là mô hình giúp hình dung khu giam giữ Chuồng Cọp tại Côn Đảo. Hãy quan sát các vách ngăn và khoảng không bên trong để hiểu vì sao cách bố trí nơi giam giữ có thể làm người tù bị cô lập. Mô hình gợi mở câu chuyện về điều kiện sống khắc nghiệt và sức chịu đựng của những người bị giam. Khi xem các chú thích, quý khách có thể đối chiếu mô hình với tư liệu để phân biệt phần tái hiện và dấu tích lịch sử.'', N'''', 0),
(1, N''en'', N''Model of the Côn Đảo Tiger Cages'', N''Before you is a model that helps you picture the Tiger Cages detention area on Côn Đảo. Look at the partitions and the confined interior to see how the layout could isolate prisoners. The model introduces the harsh conditions of confinement and the endurance of those held there. As you read the labels, compare the reconstruction with the historical records to distinguish what has been recreated from surviving evidence.'', N'''', 0),
(1, N''zh'', N''昆岛虎笼牢房模型'', N''眼前的模型帮助您了解昆岛虎笼监禁区的布局。请观察隔墙和狭小的内部空间，体会这种布局如何使囚犯彼此隔离。模型引导我们思考严酷的监禁条件，以及被关押者如何承受这些困境。阅读说明时，可以将模型与历史资料对照，分清复原场景和留存的历史证据。'', N'''', 0),
(1, N''fr'', N''Maquette des cages à tigres de Côn Đảo'', N''Cette maquette aide à comprendre la disposition de la zone de détention des cages à tigres de Côn Đảo. Observez les cloisons et l’étroitesse des espaces pour voir comment cet aménagement pouvait isoler les détenus. La maquette évoque la dureté de la détention et l’endurance des personnes emprisonnées. En lisant les cartels, comparez la reconstitution aux documents historiques pour distinguer les éléments recréés des traces conservées.'', N'''', 0),
(1, N''ru'', N''Макет тюремных «тигриных клеток» Кондао'', N''Перед вами макет, который помогает представить устройство зоны заключения «тигриные клетки» на островах Кондао. Обратите внимание на перегородки и тесное внутреннее пространство: такая планировка могла изолировать заключённых друг от друга. Макет знакомит с суровыми условиями содержания и стойкостью людей, находившихся здесь. Сопоставьте реконструкцию с историческими материалами, чтобы отличить восстановленные элементы от сохранившихся свидетельств.'', N'''', 0),
(2, N''vi'', N''Khu trưng bày Máy bay Quân sự'', N''Tại điểm này, quý khách có thể tìm hiểu vai trò của máy bay quân sự trong chiến tranh. Hãy quan sát hình dáng thân, cánh và vị trí động cơ, rồi đọc bảng chú thích để nhận diện từng hiện vật và công dụng của nó. Những khác biệt về thiết kế gợi ý các nhiệm vụ khác nhau như trinh sát, vận chuyển hoặc tác chiến. Khu trưng bày cũng đặt ra một câu hỏi: các hoạt động quân sự trên không đã ảnh hưởng thế nào đến đời sống dưới mặt đất?'', N'''', 0),
(2, N''en'', N''Military Aircraft Exhibition'', N''At this stop, explore the role of military aircraft in wartime. Look at the fuselage, wings, and engine position, then read the labels to identify each exhibit and its purpose. Differences in design suggest different tasks, such as reconnaissance, transport, or combat. The display also invites a question: how did military operations in the air affect life on the ground?'', N'''', 0),
(2, N''zh'', N''军用飞机展区'', N''在这里，您可以了解军用飞机在战争中的作用。请观察机身、机翼和发动机的位置，再阅读说明牌，辨认各件展品及其用途。设计上的差异反映了侦察、运输或作战等不同任务。这一区域也引出一个问题：空中军事行动怎样影响地面上的生活？'', N'''', 0),
(2, N''fr'', N''Exposition des avions militaires'', N''Découvrez ici le rôle des avions militaires en temps de guerre. Observez le fuselage, les ailes et la position des moteurs, puis consultez les cartels pour identifier chaque appareil et son usage. Les différences de conception évoquent des missions de reconnaissance, de transport ou de combat. L’exposition invite aussi à réfléchir aux effets des opérations aériennes sur la vie au sol.'', N'''', 0),
(2, N''ru'', N''Экспозиция военной авиации'', N''Здесь можно узнать о роли военной авиации в годы войны. Рассмотрите фюзеляж, крылья и расположение двигателей, а затем прочитайте пояснения о каждом экспонате и его назначении. Различия в конструкции отражают разные задачи: разведку, перевозку или боевые действия. Экспозиция также предлагает задуматься о том, как военные операции в воздухе влияли на жизнь людей на земле.'', N'''', 0),
(3, N''vi'', N''Chuyên đề Hậu quả Chất độc Da cam'', N''Chuyên đề này hướng sự chú ý vào những hệ quả kéo dài của chất độc da cam đối với con người và môi trường. Khi đọc tư liệu, hãy phân biệt thời điểm phơi nhiễm, những hậu quả được ghi nhận và những nỗ lực hỗ trợ sau chiến tranh. Câu chuyện không dừng lại ở số liệu: lời kể của người trong cuộc cho thấy gia đình và cộng đồng phải thích nghi với những khó khăn lâu dài như thế nào. Quý khách có thể dành thêm thời gian để đọc các chú thích một cách tôn trọng.'', N'''', 0),
(3, N''en'', N''Consequences of Agent Orange'', N''This section examines the lasting consequences of Agent Orange for people and the environment. As you read the material, distinguish the period of exposure, the documented effects, and later efforts to provide support. The story goes beyond statistics: personal accounts show how families and communities have had to adapt to long-term difficulties. Take time to read the captions with care and respect.'', N'''', 0),
(3, N''zh'', N''橙剂后果专题'', N''这一专题关注橙剂对人和环境造成的长期影响。阅读资料时，请区分接触发生的时期、已有记录的后果以及战后开展的援助工作。数字之外，亲历者的讲述让我们看到家庭和社区如何应对持续多年的困难。欢迎您放慢脚步，认真而尊重地阅读展品说明。'', N'''', 0),
(3, N''fr'', N''Les conséquences de l’Agent orange'', N''Cette section aborde les conséquences durables de l’Agent orange sur les personnes et l’environnement. En lisant les documents, distinguez la période d’exposition, les effets documentés et les efforts d’accompagnement entrepris après la guerre. Au-delà des chiffres, les témoignages montrent comment les familles et les communautés ont dû s’adapter à des difficultés de longue durée. Prenez le temps de lire les légendes avec attention et respect.'', N'''', 0),
(3, N''ru'', N''Последствия применения «Агента Оранж»'', N''Этот раздел посвящён долгосрочным последствиям применения «Агента Оранж» для людей и окружающей среды. Читая документы, различайте время воздействия, зафиксированные последствия и меры поддержки, принятые после войны. За статистикой стоят личные истории о том, как семьи и сообщества справлялись с длительными трудностями. Прочитайте пояснения внимательно и с уважением к людям, о которых они рассказывают.'', N'''', 0),
(4, N''vi'', N''Phòng trưng bày Tội ác Chiến tranh'', N''Điểm dừng này đề nghị quý khách nhìn chiến tranh từ trải nghiệm của những người chịu ảnh hưởng trực tiếp. Ảnh và tư liệu có thể khó xem. Hãy đọc chú thích để biết bối cảnh, địa điểm và nguồn của từng tư liệu trước khi rút ra nhận xét. Khi đặt các tài liệu cạnh nhau, quý khách có thể nhận ra vì sao việc lưu giữ chứng cứ và ghi nhớ mất mát của dân thường có ý nghĩa đối với các thế hệ sau.'', N'''', 0),
(4, N''en'', N''War Crimes Exhibition Room'', N''This stop invites you to look at war through the experiences of those directly affected. Some photographs and records may be difficult to view. Read the captions for the context, location, and source of each item before drawing conclusions. Looking at the materials together helps explain why preserving evidence and remembering civilian losses matters to future generations.'', N'''', 0),
(4, N''zh'', N''战争罪行展厅'', N''这个展区邀请您从直接受到战争影响的人们的经历出发，重新审视战争。部分照片和资料可能令人难以直视。请先阅读说明，了解每件资料的背景、地点和来源，再作判断。将这些资料结合起来看，有助于理解为什么保存证据、铭记平民的损失对后代具有意义。'', N'''', 0),
(4, N''fr'', N''Salle des crimes de guerre'', N''Cette étape invite à regarder la guerre à travers l’expérience des personnes directement touchées. Certaines photographies et archives peuvent être difficiles à regarder. Avant de tirer des conclusions, lisez les légendes pour situer chaque document et en connaître la provenance. Leur rapprochement aide à comprendre pourquoi préserver les preuves et garder la mémoire des pertes civiles compte pour les générations futures.'', N'''', 0),
(4, N''ru'', N''Зал свидетельств о военных преступлениях'', N''Этот раздел предлагает взглянуть на войну глазами тех, кого она затронула непосредственно. Некоторые фотографии и документы могут быть тяжёлыми для восприятия. Прежде чем делать выводы, прочитайте пояснения о месте, обстоятельствах и происхождении каждого материала. Сопоставление свидетельств помогает понять, почему важно сохранять доказательства и память о потерях среди мирных жителей для будущих поколений.'', N'''', 0),
(5, N''vi'', N''Phòng Sự thật Lịch sử'', N''Tại đây, hãy thử đọc một bức ảnh hoặc một tài liệu theo ba câu hỏi: tư liệu được tạo khi nào, do ai tạo và ghi lại điều gì? Dòng chú thích giúp đặt hiện vật vào bối cảnh. Nếu nhiều nguồn cùng nói về một sự kiện, hãy để ý phần chúng xác nhận lẫn nhau và phần chúng kể từ những góc nhìn khác nhau. Cách đọc ấy giúp hình thành nhận xét có căn cứ thay vì chỉ dựa vào ấn tượng ban đầu.'', N'''', 0),
(5, N''en'', N''Historical Truths Gallery'', N''Here, try reading a photograph or document through three questions: when was it made, who made it, and what does it record? The caption puts the item in context. When several sources describe the same event, notice what they corroborate and where they present different perspectives. This approach helps you form an evidence-based judgment instead of relying only on first impressions.'', N'''', 0),
(5, N''zh'', N''历史真相展厅'', N''在这里，可以带着三个问题阅读一张照片或一份文献：它是什么时候形成的？由谁制作？记录了什么？展品说明能够帮助您把资料放回当时的背景。如果多个来源都谈到同一事件，请留意它们彼此印证的部分，以及各自不同的视角。这样可以依据证据形成判断，而不只依赖第一印象。'', N'''', 0),
(5, N''fr'', N''Salle des vérités historiques'', N''Pour lire une photographie ou un document, posez-vous trois questions : quand a-t-il été produit, par qui et que montre-t-il ? Le cartel replace la pièce dans son contexte. Si plusieurs sources évoquent le même événement, observez ce qu’elles confirment ensemble et les différences de perspective. Cette démarche aide à construire un jugement fondé sur les preuves plutôt que sur la seule première impression.'', N'''', 0),
(5, N''ru'', N''Зал исторической правды'', N''Рассматривая фотографию или документ, задайте три вопроса: когда он был создан, кем и что он фиксирует? Пояснение помогает понять контекст экспоната. Если несколько источников рассказывают об одном событии, обратите внимание на совпадающие свидетельства и различия в точках зрения. Так можно сделать вывод на основе источников, а не только первого впечатления.'', N'''', 0),
(6, N''vi'', N''Nhân dân Thế giới Hỗ trợ Việt Nam'', N''Điểm này mở rộng góc nhìn từ Việt Nam ra thế giới. Sự ủng hộ có thể được bày tỏ qua hoạt động phản đối chiến tranh, lời kêu gọi hòa bình hoặc những hình thức giúp đỡ cụ thể. Khi xem tư liệu, hãy để ý ai đang lên tiếng, họ muốn truyền đạt điều gì và thông điệp hướng tới ai. Những câu hỏi đó giúp quý khách hiểu cách tiếng nói của các cộng đồng có thể vượt qua biên giới.'', N'''', 0),
(6, N''en'', N''International Solidarity with Vietnam'', N''This stop widens the view from Vietnam to the rest of the world. Support could take the form of anti-war action, appeals for peace, or practical assistance. As you examine the material, notice who is speaking, what they want to convey, and who their message is meant to reach. These questions reveal how communities can make their voices heard across borders.'', N'''', 0),
(6, N''zh'', N''国际社会对越南的声援'', N''这里把视线从越南拓展到世界各地。声援可以通过反战行动、和平呼吁或具体援助表达出来。观看资料时，请留意是谁在发声、他们希望传达什么，以及信息面向哪些人。这些问题帮助我们理解不同社区的声音如何跨越国界。'', N'''', 0),
(6, N''fr'', N''Solidarité internationale avec le Vietnam'', N''Cette étape élargit le regard du Vietnam au reste du monde. Le soutien pouvait prendre la forme d’actions contre la guerre, d’appels à la paix ou d’une aide concrète. En examinant les documents, demandez-vous qui prend la parole, quel message est exprimé et à qui il s’adresse. Ces questions montrent comment des communautés peuvent se faire entendre au-delà des frontières.'', N'''', 0),
(6, N''ru'', N''Международная солидарность с Вьетнамом'', N''Этот раздел расширяет взгляд от Вьетнама к остальному миру. Поддержка могла выражаться в антивоенных выступлениях, призывах к миру или практической помощи. Изучая материалы, отметьте, кто говорит, что хочет сообщить и кому адресовано послание. Эти вопросы помогают понять, как голоса разных сообществ могут звучать за пределами государственных границ.'', N'''', 0),
(7, N''vi'', N''Ẩm thực Nam Bộ thời kháng chiến'', N''Bữa ăn kể một phần câu chuyện về đời sống trong hoàn cảnh thiếu thốn. Tại điểm này, quý khách có thể suy nghĩ về cách người dân Nam Bộ tận dụng nguyên liệu sẵn có, chế biến đơn giản và chia sẻ thức ăn trong thời kháng chiến. Hãy chú ý mối liên hệ giữa món ăn, môi trường sống và điều kiện bảo quản. Từ những chi tiết đời thường ấy, ta hiểu rõ hơn sự thích nghi và vai trò của cộng đồng.'', N'''', 0),
(7, N''en'', N''Southern Vietnamese Food During the Resistance'', N''Meals tell part of the story of everyday life under conditions of scarcity. At this stop, consider how people in southern Vietnam used available ingredients, prepared food simply, and shared meals during the resistance. Notice the connection between food, the local environment, and storage conditions. These everyday details reveal how people adapted and how communities supported them.'', N'''', 0),
(7, N''zh'', N''抗战时期的越南南部饮食'', N''一顿饭也能讲述物资短缺时的日常生活。在这个展点，请思考抗战时期越南南部居民如何利用手边的食材、采用简单的烹饪方式并分享食物。留意菜肴与生活环境、食物保存条件之间的联系。从这些日常细节中，我们可以更好地理解人们的适应能力和社区的作用。'', N'''', 0),
(7, N''fr'', N''La cuisine du Sud vietnamien pendant la résistance'', N''Les repas racontent une partie de la vie quotidienne en période de pénurie. Réfléchissez à la façon dont les habitants du Sud vietnamien utilisaient les ingrédients disponibles, préparaient des plats simples et partageaient la nourriture pendant la résistance. Observez les liens entre les aliments, le milieu de vie et les possibilités de conservation. Ces détails montrent comment les personnes s’adaptaient et s’entraidaient.'', N'''', 0),
(7, N''ru'', N''Кухня Южного Вьетнама в годы сопротивления'', N''Еда рассказывает о повседневной жизни в условиях нехватки ресурсов. Подумайте о том, как жители Южного Вьетнама использовали доступные продукты, выбирали простые способы приготовления и делились пищей в годы сопротивления. Обратите внимание на связь между блюдами, средой обитания и условиями хранения. Эти повседневные детали показывают, как люди приспосабливались и помогали друг другу.'', N'''', 0),
(8, N''vi'', N''Nguyễn Thị Bình và con đường ngoại giao'', N''Chuyên đề giới thiệu bà Nguyễn Thị Bình qua câu chuyện ngoại giao và các cuộc đàm phán Paris. Khi đọc tư liệu, hãy chú ý vai trò của đối thoại trong việc tìm kiếm một giải pháp chính trị cho chiến tranh. Một cuộc thương lượng đòi hỏi sự chuẩn bị, khả năng lắng nghe và kiên trì trình bày lập trường. Từ câu chuyện của bà, quý khách có thể suy ngẫm về vai trò của phụ nữ trong ngoại giao và những nỗ lực hướng tới hòa bình.'', N'''', 0),
(8, N''en'', N''Nguyễn Thị Bình and the Path of Diplomacy'', N''This section introduces Nguyễn Thị Bình through her diplomatic work and the Paris negotiations. As you read the materials, consider how dialogue helps parties seek a political solution to war. Negotiation requires preparation, the ability to listen, and persistence in explaining a position. Her story invites reflection on women’s role in diplomacy and on efforts to achieve peace.'', N'''', 0),
(8, N''zh'', N''阮氏平与外交之路'', N''这一专题通过外交工作和巴黎谈判介绍阮氏平。阅读资料时，请关注对话在寻求以政治方式解决战争中的作用。谈判需要充分准备、耐心倾听，并坚持阐明立场。她的故事启发我们思考女性在外交中的角色，以及为争取和平所作的努力。'', N'''', 0),
(8, N''fr'', N''Nguyễn Thị Bình et la voie diplomatique'', N''Cette section présente Nguyễn Thị Bình à travers son action diplomatique et les négociations de Paris. En lisant les documents, observez le rôle du dialogue dans la recherche d’une issue politique à la guerre. Négocier exige de se préparer, d’écouter et de défendre patiemment une position. Son parcours invite à réfléchir à la place des femmes dans la diplomatie et aux efforts en faveur de la paix.'', N'''', 0),
(8, N''ru'', N''Нгуен Тхи Бинь и путь дипломатии'', N''Этот раздел знакомит с Нгуен Тхи Бинь через её дипломатическую деятельность и парижские переговоры. Изучая материалы, обратите внимание на роль диалога в поиске политического решения войны. Переговоры требуют подготовки, умения слушать и настойчивости в изложении позиции. Её история помогает задуматься о роли женщин в дипломатии и об усилиях, направленных на достижение мира.'', N'''', 0);
END;
IF NOT EXISTS(SELECT 1 FROM dbo.QrCodes WHERE QrToken=N''MUSEUM-MAIN-ENTRY'') INSERT dbo.QrCodes(PoiId,QrToken,ScanCount,IsActive) VALUES(NULL,N''MUSEUM-MAIN-ENTRY'',0,1);';
    END
    ELSE
        EXEC sys.sp_executesql N'
            IF NOT EXISTS (SELECT 1 FROM dbo.QrCodes WHERE QrToken=N''MUSEUM-MAIN-ENTRY'')
                INSERT dbo.QrCodes(PoiId,QrToken,ScanCount,IsActive)
                VALUES (NULL,N''MUSEUM-MAIN-ENTRY'',0,1);';

    IF @RefreshDemoNarratives = 1
    BEGIN
        EXEC sys.sp_executesql N'-- Thay nội dung 8 POI mẫu mã 1–8. Chạy trên AudioGuideDb sau khi sao lưu.
-- Giữ tọa độ, thiết bị, nhật ký và QR. Nội dung POI tùy chỉnh ngoài 8 mã này được giữ.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N''AudioGuideDb'' THROW 51004, N''Hãy chọn đúng AudioGuideDb trước khi chạy.'', 1;
DECLARE @Narratives TABLE (
    Code nvarchar(50) NOT NULL, LanguageCode nvarchar(10) NOT NULL,
    Title nvarchar(200) NOT NULL, Description nvarchar(max) NOT NULL,
    PRIMARY KEY (Code, LanguageCode)
);
INSERT INTO @Narratives VALUES
(N''1'', N''vi'', N''Mô hình Trại giam Chuồng Cọp'', N''Trước mặt quý khách là mô hình giúp hình dung khu giam giữ Chuồng Cọp tại Côn Đảo. Hãy quan sát các vách ngăn và khoảng không bên trong để hiểu vì sao cách bố trí nơi giam giữ có thể làm người tù bị cô lập. Mô hình gợi mở câu chuyện về điều kiện sống khắc nghiệt và sức chịu đựng của những người bị giam. Khi xem các chú thích, quý khách có thể đối chiếu mô hình với tư liệu để phân biệt phần tái hiện và dấu tích lịch sử.''),
(N''1'', N''en'', N''Model of the Côn Đảo Tiger Cages'', N''Before you is a model that helps you picture the Tiger Cages detention area on Côn Đảo. Look at the partitions and the confined interior to see how the layout could isolate prisoners. The model introduces the harsh conditions of confinement and the endurance of those held there. As you read the labels, compare the reconstruction with the historical records to distinguish what has been recreated from surviving evidence.''),
(N''1'', N''zh'', N''昆岛虎笼牢房模型'', N''眼前的模型帮助您了解昆岛虎笼监禁区的布局。请观察隔墙和狭小的内部空间，体会这种布局如何使囚犯彼此隔离。模型引导我们思考严酷的监禁条件，以及被关押者如何承受这些困境。阅读说明时，可以将模型与历史资料对照，分清复原场景和留存的历史证据。''),
(N''1'', N''fr'', N''Maquette des cages à tigres de Côn Đảo'', N''Cette maquette aide à comprendre la disposition de la zone de détention des cages à tigres de Côn Đảo. Observez les cloisons et l’étroitesse des espaces pour voir comment cet aménagement pouvait isoler les détenus. La maquette évoque la dureté de la détention et l’endurance des personnes emprisonnées. En lisant les cartels, comparez la reconstitution aux documents historiques pour distinguer les éléments recréés des traces conservées.''),
(N''1'', N''ru'', N''Макет тюремных «тигриных клеток» Кондао'', N''Перед вами макет, который помогает представить устройство зоны заключения «тигриные клетки» на островах Кондао. Обратите внимание на перегородки и тесное внутреннее пространство: такая планировка могла изолировать заключённых друг от друга. Макет знакомит с суровыми условиями содержания и стойкостью людей, находившихся здесь. Сопоставьте реконструкцию с историческими материалами, чтобы отличить восстановленные элементы от сохранившихся свидетельств.''),
(N''2'', N''vi'', N''Khu trưng bày Máy bay Quân sự'', N''Tại điểm này, quý khách có thể tìm hiểu vai trò của máy bay quân sự trong chiến tranh. Hãy quan sát hình dáng thân, cánh và vị trí động cơ, rồi đọc bảng chú thích để nhận diện từng hiện vật và công dụng của nó. Những khác biệt về thiết kế gợi ý các nhiệm vụ khác nhau như trinh sát, vận chuyển hoặc tác chiến. Khu trưng bày cũng đặt ra một câu hỏi: các hoạt động quân sự trên không đã ảnh hưởng thế nào đến đời sống dưới mặt đất?''),
(N''2'', N''en'', N''Military Aircraft Exhibition'', N''At this stop, explore the role of military aircraft in wartime. Look at the fuselage, wings, and engine position, then read the labels to identify each exhibit and its purpose. Differences in design suggest different tasks, such as reconnaissance, transport, or combat. The display also invites a question: how did military operations in the air affect life on the ground?''),
(N''2'', N''zh'', N''军用飞机展区'', N''在这里，您可以了解军用飞机在战争中的作用。请观察机身、机翼和发动机的位置，再阅读说明牌，辨认各件展品及其用途。设计上的差异反映了侦察、运输或作战等不同任务。这一区域也引出一个问题：空中军事行动怎样影响地面上的生活？''),
(N''2'', N''fr'', N''Exposition des avions militaires'', N''Découvrez ici le rôle des avions militaires en temps de guerre. Observez le fuselage, les ailes et la position des moteurs, puis consultez les cartels pour identifier chaque appareil et son usage. Les différences de conception évoquent des missions de reconnaissance, de transport ou de combat. L’exposition invite aussi à réfléchir aux effets des opérations aériennes sur la vie au sol.''),
(N''2'', N''ru'', N''Экспозиция военной авиации'', N''Здесь можно узнать о роли военной авиации в годы войны. Рассмотрите фюзеляж, крылья и расположение двигателей, а затем прочитайте пояснения о каждом экспонате и его назначении. Различия в конструкции отражают разные задачи: разведку, перевозку или боевые действия. Экспозиция также предлагает задуматься о том, как военные операции в воздухе влияли на жизнь людей на земле.''),
(N''3'', N''vi'', N''Chuyên đề Hậu quả Chất độc Da cam'', N''Chuyên đề này hướng sự chú ý vào những hệ quả kéo dài của chất độc da cam đối với con người và môi trường. Khi đọc tư liệu, hãy phân biệt thời điểm phơi nhiễm, những hậu quả được ghi nhận và những nỗ lực hỗ trợ sau chiến tranh. Câu chuyện không dừng lại ở số liệu: lời kể của người trong cuộc cho thấy gia đình và cộng đồng phải thích nghi với những khó khăn lâu dài như thế nào. Quý khách có thể dành thêm thời gian để đọc các chú thích một cách tôn trọng.''),
(N''3'', N''en'', N''Consequences of Agent Orange'', N''This section examines the lasting consequences of Agent Orange for people and the environment. As you read the material, distinguish the period of exposure, the documented effects, and later efforts to provide support. The story goes beyond statistics: personal accounts show how families and communities have had to adapt to long-term difficulties. Take time to read the captions with care and respect.''),
(N''3'', N''zh'', N''橙剂后果专题'', N''这一专题关注橙剂对人和环境造成的长期影响。阅读资料时，请区分接触发生的时期、已有记录的后果以及战后开展的援助工作。数字之外，亲历者的讲述让我们看到家庭和社区如何应对持续多年的困难。欢迎您放慢脚步，认真而尊重地阅读展品说明。''),
(N''3'', N''fr'', N''Les conséquences de l’Agent orange'', N''Cette section aborde les conséquences durables de l’Agent orange sur les personnes et l’environnement. En lisant les documents, distinguez la période d’exposition, les effets documentés et les efforts d’accompagnement entrepris après la guerre. Au-delà des chiffres, les témoignages montrent comment les familles et les communautés ont dû s’adapter à des difficultés de longue durée. Prenez le temps de lire les légendes avec attention et respect.''),
(N''3'', N''ru'', N''Последствия применения «Агента Оранж»'', N''Этот раздел посвящён долгосрочным последствиям применения «Агента Оранж» для людей и окружающей среды. Читая документы, различайте время воздействия, зафиксированные последствия и меры поддержки, принятые после войны. За статистикой стоят личные истории о том, как семьи и сообщества справлялись с длительными трудностями. Прочитайте пояснения внимательно и с уважением к людям, о которых они рассказывают.''),
(N''4'', N''vi'', N''Phòng trưng bày Tội ác Chiến tranh'', N''Điểm dừng này đề nghị quý khách nhìn chiến tranh từ trải nghiệm của những người chịu ảnh hưởng trực tiếp. Ảnh và tư liệu có thể khó xem. Hãy đọc chú thích để biết bối cảnh, địa điểm và nguồn của từng tư liệu trước khi rút ra nhận xét. Khi đặt các tài liệu cạnh nhau, quý khách có thể nhận ra vì sao việc lưu giữ chứng cứ và ghi nhớ mất mát của dân thường có ý nghĩa đối với các thế hệ sau.''),
(N''4'', N''en'', N''War Crimes Exhibition Room'', N''This stop invites you to look at war through the experiences of those directly affected. Some photographs and records may be difficult to view. Read the captions for the context, location, and source of each item before drawing conclusions. Looking at the materials together helps explain why preserving evidence and remembering civilian losses matters to future generations.''),
(N''4'', N''zh'', N''战争罪行展厅'', N''这个展区邀请您从直接受到战争影响的人们的经历出发，重新审视战争。部分照片和资料可能令人难以直视。请先阅读说明，了解每件资料的背景、地点和来源，再作判断。将这些资料结合起来看，有助于理解为什么保存证据、铭记平民的损失对后代具有意义。''),
(N''4'', N''fr'', N''Salle des crimes de guerre'', N''Cette étape invite à regarder la guerre à travers l’expérience des personnes directement touchées. Certaines photographies et archives peuvent être difficiles à regarder. Avant de tirer des conclusions, lisez les légendes pour situer chaque document et en connaître la provenance. Leur rapprochement aide à comprendre pourquoi préserver les preuves et garder la mémoire des pertes civiles compte pour les générations futures.''),
(N''4'', N''ru'', N''Зал свидетельств о военных преступлениях'', N''Этот раздел предлагает взглянуть на войну глазами тех, кого она затронула непосредственно. Некоторые фотографии и документы могут быть тяжёлыми для восприятия. Прежде чем делать выводы, прочитайте пояснения о месте, обстоятельствах и происхождении каждого материала. Сопоставление свидетельств помогает понять, почему важно сохранять доказательства и память о потерях среди мирных жителей для будущих поколений.''),
(N''5'', N''vi'', N''Phòng Sự thật Lịch sử'', N''Tại đây, hãy thử đọc một bức ảnh hoặc một tài liệu theo ba câu hỏi: tư liệu được tạo khi nào, do ai tạo và ghi lại điều gì? Dòng chú thích giúp đặt hiện vật vào bối cảnh. Nếu nhiều nguồn cùng nói về một sự kiện, hãy để ý phần chúng xác nhận lẫn nhau và phần chúng kể từ những góc nhìn khác nhau. Cách đọc ấy giúp hình thành nhận xét có căn cứ thay vì chỉ dựa vào ấn tượng ban đầu.''),
(N''5'', N''en'', N''Historical Truths Gallery'', N''Here, try reading a photograph or document through three questions: when was it made, who made it, and what does it record? The caption puts the item in context. When several sources describe the same event, notice what they corroborate and where they present different perspectives. This approach helps you form an evidence-based judgment instead of relying only on first impressions.''),
(N''5'', N''zh'', N''历史真相展厅'', N''在这里，可以带着三个问题阅读一张照片或一份文献：它是什么时候形成的？由谁制作？记录了什么？展品说明能够帮助您把资料放回当时的背景。如果多个来源都谈到同一事件，请留意它们彼此印证的部分，以及各自不同的视角。这样可以依据证据形成判断，而不只依赖第一印象。''),
(N''5'', N''fr'', N''Salle des vérités historiques'', N''Pour lire une photographie ou un document, posez-vous trois questions : quand a-t-il été produit, par qui et que montre-t-il ? Le cartel replace la pièce dans son contexte. Si plusieurs sources évoquent le même événement, observez ce qu’elles confirment ensemble et les différences de perspective. Cette démarche aide à construire un jugement fondé sur les preuves plutôt que sur la seule première impression.''),
(N''5'', N''ru'', N''Зал исторической правды'', N''Рассматривая фотографию или документ, задайте три вопроса: когда он был создан, кем и что он фиксирует? Пояснение помогает понять контекст экспоната. Если несколько источников рассказывают об одном событии, обратите внимание на совпадающие свидетельства и различия в точках зрения. Так можно сделать вывод на основе источников, а не только первого впечатления.''),
(N''6'', N''vi'', N''Nhân dân Thế giới Hỗ trợ Việt Nam'', N''Điểm này mở rộng góc nhìn từ Việt Nam ra thế giới. Sự ủng hộ có thể được bày tỏ qua hoạt động phản đối chiến tranh, lời kêu gọi hòa bình hoặc những hình thức giúp đỡ cụ thể. Khi xem tư liệu, hãy để ý ai đang lên tiếng, họ muốn truyền đạt điều gì và thông điệp hướng tới ai. Những câu hỏi đó giúp quý khách hiểu cách tiếng nói của các cộng đồng có thể vượt qua biên giới.''),
(N''6'', N''en'', N''International Solidarity with Vietnam'', N''This stop widens the view from Vietnam to the rest of the world. Support could take the form of anti-war action, appeals for peace, or practical assistance. As you examine the material, notice who is speaking, what they want to convey, and who their message is meant to reach. These questions reveal how communities can make their voices heard across borders.''),
(N''6'', N''zh'', N''国际社会对越南的声援'', N''这里把视线从越南拓展到世界各地。声援可以通过反战行动、和平呼吁或具体援助表达出来。观看资料时，请留意是谁在发声、他们希望传达什么，以及信息面向哪些人。这些问题帮助我们理解不同社区的声音如何跨越国界。''),
(N''6'', N''fr'', N''Solidarité internationale avec le Vietnam'', N''Cette étape élargit le regard du Vietnam au reste du monde. Le soutien pouvait prendre la forme d’actions contre la guerre, d’appels à la paix ou d’une aide concrète. En examinant les documents, demandez-vous qui prend la parole, quel message est exprimé et à qui il s’adresse. Ces questions montrent comment des communautés peuvent se faire entendre au-delà des frontières.''),
(N''6'', N''ru'', N''Международная солидарность с Вьетнамом'', N''Этот раздел расширяет взгляд от Вьетнама к остальному миру. Поддержка могла выражаться в антивоенных выступлениях, призывах к миру или практической помощи. Изучая материалы, отметьте, кто говорит, что хочет сообщить и кому адресовано послание. Эти вопросы помогают понять, как голоса разных сообществ могут звучать за пределами государственных границ.''),
(N''7'', N''vi'', N''Ẩm thực Nam Bộ thời kháng chiến'', N''Bữa ăn kể một phần câu chuyện về đời sống trong hoàn cảnh thiếu thốn. Tại điểm này, quý khách có thể suy nghĩ về cách người dân Nam Bộ tận dụng nguyên liệu sẵn có, chế biến đơn giản và chia sẻ thức ăn trong thời kháng chiến. Hãy chú ý mối liên hệ giữa món ăn, môi trường sống và điều kiện bảo quản. Từ những chi tiết đời thường ấy, ta hiểu rõ hơn sự thích nghi và vai trò của cộng đồng.''),
(N''7'', N''en'', N''Southern Vietnamese Food During the Resistance'', N''Meals tell part of the story of everyday life under conditions of scarcity. At this stop, consider how people in southern Vietnam used available ingredients, prepared food simply, and shared meals during the resistance. Notice the connection between food, the local environment, and storage conditions. These everyday details reveal how people adapted and how communities supported them.''),
(N''7'', N''zh'', N''抗战时期的越南南部饮食'', N''一顿饭也能讲述物资短缺时的日常生活。在这个展点，请思考抗战时期越南南部居民如何利用手边的食材、采用简单的烹饪方式并分享食物。留意菜肴与生活环境、食物保存条件之间的联系。从这些日常细节中，我们可以更好地理解人们的适应能力和社区的作用。''),
(N''7'', N''fr'', N''La cuisine du Sud vietnamien pendant la résistance'', N''Les repas racontent une partie de la vie quotidienne en période de pénurie. Réfléchissez à la façon dont les habitants du Sud vietnamien utilisaient les ingrédients disponibles, préparaient des plats simples et partageaient la nourriture pendant la résistance. Observez les liens entre les aliments, le milieu de vie et les possibilités de conservation. Ces détails montrent comment les personnes s’adaptaient et s’entraidaient.''),
(N''7'', N''ru'', N''Кухня Южного Вьетнама в годы сопротивления'', N''Еда рассказывает о повседневной жизни в условиях нехватки ресурсов. Подумайте о том, как жители Южного Вьетнама использовали доступные продукты, выбирали простые способы приготовления и делились пищей в годы сопротивления. Обратите внимание на связь между блюдами, средой обитания и условиями хранения. Эти повседневные детали показывают, как люди приспосабливались и помогали друг другу.''),
(N''8'', N''vi'', N''Nguyễn Thị Bình và con đường ngoại giao'', N''Chuyên đề giới thiệu bà Nguyễn Thị Bình qua câu chuyện ngoại giao và các cuộc đàm phán Paris. Khi đọc tư liệu, hãy chú ý vai trò của đối thoại trong việc tìm kiếm một giải pháp chính trị cho chiến tranh. Một cuộc thương lượng đòi hỏi sự chuẩn bị, khả năng lắng nghe và kiên trì trình bày lập trường. Từ câu chuyện của bà, quý khách có thể suy ngẫm về vai trò của phụ nữ trong ngoại giao và những nỗ lực hướng tới hòa bình.''),
(N''8'', N''en'', N''Nguyễn Thị Bình and the Path of Diplomacy'', N''This section introduces Nguyễn Thị Bình through her diplomatic work and the Paris negotiations. As you read the materials, consider how dialogue helps parties seek a political solution to war. Negotiation requires preparation, the ability to listen, and persistence in explaining a position. Her story invites reflection on women’s role in diplomacy and on efforts to achieve peace.''),
(N''8'', N''zh'', N''阮氏平与外交之路'', N''这一专题通过外交工作和巴黎谈判介绍阮氏平。阅读资料时，请关注对话在寻求以政治方式解决战争中的作用。谈判需要充分准备、耐心倾听，并坚持阐明立场。她的故事启发我们思考女性在外交中的角色，以及为争取和平所作的努力。''),
(N''8'', N''fr'', N''Nguyễn Thị Bình et la voie diplomatique'', N''Cette section présente Nguyễn Thị Bình à travers son action diplomatique et les négociations de Paris. En lisant les documents, observez le rôle du dialogue dans la recherche d’une issue politique à la guerre. Négocier exige de se préparer, d’écouter et de défendre patiemment une position. Son parcours invite à réfléchir à la place des femmes dans la diplomatie et aux efforts en faveur de la paix.''),
(N''8'', N''ru'', N''Нгуен Тхи Бинь и путь дипломатии'', N''Этот раздел знакомит с Нгуен Тхи Бинь через её дипломатическую деятельность и парижские переговоры. Изучая материалы, обратите внимание на роль диалога в поиске политического решения войны. Переговоры требуют подготовки, умения слушать и настойчивости в изложении позиции. Её история помогает задуматься о роли женщин в дипломатии и об усилиях, направленных на достижение мира.'');

IF (SELECT COUNT(*) FROM dbo.Pois WITH (UPDLOCK, HOLDLOCK) WHERE Code IN (SELECT Code FROM @Narratives)) <> 8
    THROW 51005, N''Không tìm thấy đủ 8 mã POI mẫu. Dừng để tránh cập nhật sai dữ liệu.'', 1;

-- Bump rowversion so an Admin form opened before the refresh cannot overwrite the new text.
UPDATE p SET Code = p.Code
FROM dbo.Pois p
WHERE EXISTS (
    SELECT 1 FROM @Narratives n
    LEFT JOIN dbo.PoiTranslations t ON t.PoiId=p.Id AND t.LanguageCode=n.LanguageCode
    WHERE n.Code=p.Code AND (t.Id IS NULL
       OR t.Title COLLATE Latin1_General_100_BIN2 <> n.Title COLLATE Latin1_General_100_BIN2
       OR COALESCE(t.Description, N'''') COLLATE Latin1_General_100_BIN2 <> n.Description COLLATE Latin1_General_100_BIN2)
);

UPDATE t
SET t.Title = n.Title, t.Description = n.Description, t.AudioUrl = N'''', t.DurationSeconds = 0
FROM dbo.PoiTranslations t
JOIN dbo.Pois p ON p.Id = t.PoiId
JOIN @Narratives n ON n.Code = p.Code AND n.LanguageCode = t.LanguageCode
WHERE t.Title COLLATE Latin1_General_100_BIN2 <> n.Title COLLATE Latin1_General_100_BIN2
   OR COALESCE(t.Description, N'''') COLLATE Latin1_General_100_BIN2 <> n.Description COLLATE Latin1_General_100_BIN2;

INSERT INTO dbo.PoiTranslations (PoiId, LanguageCode, Title, Description, AudioUrl, DurationSeconds)
SELECT p.Id, n.LanguageCode, n.Title, n.Description, N'''', 0
FROM @Narratives n
JOIN dbo.Pois p ON p.Code = n.Code
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PoiTranslations t WITH (UPDLOCK, HOLDLOCK)
    WHERE t.PoiId = p.Id AND t.LanguageCode = n.LanguageCode
);';
    END;

    IF OBJECT_ID(N'dbo.AdminAccounts', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AdminAccounts (
            Id int NOT NULL,
            Username nvarchar(100) NOT NULL,
            PasswordHash nvarchar(512) NOT NULL,
            Version uniqueidentifier NOT NULL,
            CONSTRAINT PK_AdminAccounts PRIMARY KEY (Id),
            CONSTRAINT CK_AdminAccounts_SingleAccount CHECK (Id = 1)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Name = N'005_AdminAccounts.sql')
        INSERT dbo.SchemaVersions (Name, Sha256)
        VALUES (N'005_AdminAccounts.sql', '6F8D63A2DE97A48CD880A9F18F8445F247010DEDC911C731A16BADD27EB0947E');

    COMMIT TRANSACTION;
    PRINT N'Đã hoàn tất thiết lập AudioGuideDb. Xem dữ liệu bằng AudioGuideDB_DisplayTable.sql.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
