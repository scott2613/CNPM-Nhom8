SET XACT_ABORT ON;
IF COL_LENGTH('dbo.Pois','RowVersion') IS NULL ALTER TABLE dbo.Pois ADD RowVersion rowversion;
IF COL_LENGTH('dbo.QrScanLogs','EventId') IS NULL ALTER TABLE dbo.QrScanLogs ADD EventId uniqueidentifier NULL;
IF COL_LENGTH('dbo.QrScanLogs','EventType') IS NULL ALTER TABLE dbo.QrScanLogs ADD EventType nvarchar(20) NOT NULL CONSTRAINT DF_Log_EventType DEFAULT N'Legacy';
IF COL_LENGTH('dbo.QrScanLogs','VisitorId') IS NULL ALTER TABLE dbo.QrScanLogs ADD VisitorId nvarchar(100) NULL;
IF COL_LENGTH('dbo.QrScanLogs','PoiId') IS NULL ALTER TABLE dbo.QrScanLogs ADD PoiId int NULL;
IF COL_LENGTH('dbo.QrScanLogs','LanguageCode') IS NULL ALTER TABLE dbo.QrScanLogs ADD LanguageCode nvarchar(10) NOT NULL CONSTRAINT DF_Log_Language DEFAULT N'vi';
IF COL_LENGTH('dbo.QrScanLogs','IsUtc') IS NULL ALTER TABLE dbo.QrScanLogs ADD IsUtc bit NOT NULL CONSTRAINT DF_Log_IsUtc DEFAULT 0;
IF COL_LENGTH('dbo.DeviceSessions','PreferredLanguage') IS NULL ALTER TABLE dbo.DeviceSessions ADD PreferredLanguage nvarchar(10) NULL;
GO
UPDATE dbo.DeviceSessions SET PreferredLanguage=N'vi' WHERE PreferredLanguage IS NULL;
ALTER TABLE dbo.DeviceSessions ALTER COLUMN PreferredLanguage nvarchar(10) NOT NULL;
-- Abort on inconsistent historical data; do not silently delete duplicates.
IF EXISTS(SELECT PoiId FROM dbo.QrCodes WHERE PoiId IS NOT NULL GROUP BY PoiId HAVING COUNT(*)>1) THROW 51001,'Duplicate QR PoiId; reconcile before upgrading',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.QrCodes') AND name='IX_QrCodes_PoiId') CREATE UNIQUE INDEX IX_QrCodes_PoiId ON dbo.QrCodes(PoiId) WHERE PoiId IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.QrScanLogs') AND name='IX_QrScanLogs_EventId') CREATE UNIQUE INDEX IX_QrScanLogs_EventId ON dbo.QrScanLogs(EventId) WHERE EventId IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.QrScanLogs') AND name='IX_QrScanLogs_EventType_ScannedAt') CREATE INDEX IX_QrScanLogs_EventType_ScannedAt ON dbo.QrScanLogs(EventType,ScannedAt);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.QrScanLogs') AND name='IX_QrScanLogs_VisitorId_ScannedAt') CREATE INDEX IX_QrScanLogs_VisitorId_ScannedAt ON dbo.QrScanLogs(VisitorId,ScannedAt);
DECLARE @drop nvarchar(max)=N'';
SELECT @drop+=N'ALTER TABLE dbo.QrCodes DROP CONSTRAINT '+QUOTENAME(name)+N';' FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.QrCodes') AND referenced_object_id=OBJECT_ID('dbo.Pois');
EXEC sp_executesql @drop;
ALTER TABLE dbo.QrCodes ADD CONSTRAINT FK_QrCodes_Pois_Upgrade FOREIGN KEY(PoiId) REFERENCES dbo.Pois(Id) ON DELETE SET NULL;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_QrScanLogs_Pois_PoiId') ALTER TABLE dbo.QrScanLogs ADD CONSTRAINT FK_QrScanLogs_Pois_PoiId FOREIGN KEY(PoiId) REFERENCES dbo.Pois(Id) ON DELETE SET NULL;
UPDATE dbo.QrCodes SET PoiId=NULL WHERE QrToken=N'MUSEUM-MAIN-ENTRY';
UPDATE dbo.QrCodes SET IsActive=0 WHERE QrToken<>N'MUSEUM-MAIN-ENTRY';
IF NOT EXISTS(SELECT 1 FROM dbo.QrCodes WHERE QrToken=N'MUSEUM-MAIN-ENTRY') INSERT dbo.QrCodes(PoiId,QrToken,ScanCount,IsActive) VALUES(NULL,N'MUSEUM-MAIN-ENTRY',0,1);
-- Existing gate IsActive is preserved: an intentionally disabled gate is not silently enabled.
