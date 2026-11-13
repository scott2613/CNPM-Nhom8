-- Báo cáo chỉ đọc trong SSMS. Chạy sau khi đã tạo hoặc nâng cấp bằng AudioGuideDB.sql.
USE [AudioGuideDb];
GO
SET NOCOUNT ON;

IF DB_NAME() <> N'AudioGuideDb'
    THROW 51020, N'Hãy chọn đúng AudioGuideDb.', 1;
IF COL_LENGTH('dbo.Pois', 'RowVersion') IS NULL OR COL_LENGTH('dbo.QrScanLogs', 'EventType') IS NULL
    THROW 51021, N'Hãy nâng cấp schema bằng AudioGuideDB.sql trước.', 1;

DECLARE @Page int = 1;
DECLARE @PageSize int = 50;
IF @Page < 1 OR @PageSize NOT BETWEEN 1 AND 200
    THROW 51022, N'Trang phải từ 1 và số dòng mỗi trang từ 1 đến 200.', 1;
DECLARE @Skip bigint = (CONVERT(bigint, @Page) - 1) * @PageSize;

-- 1. Địa điểm và tổng lượt tương tác đã ghi nhận.
;WITH LuotTuongTac AS (
    SELECT PoiId,
        COUNT_BIG(CASE WHEN EventType = N'PoiViewed' THEN 1 END) AS LuotMoDiem,
        COUNT_BIG(CASE WHEN EventType = N'PlayStarted' THEN 1 END) AS LuotBatDauNghe
    FROM dbo.QrScanLogs
    WHERE IsUtc = 1 AND PoiId IS NOT NULL AND EventType IN (N'PoiViewed', N'PlayStarted')
    GROUP BY PoiId
)
SELECT p.Code AS MaDiaDiem, t.Title AS TenDiaDiem, p.Floor AS Tang, p.IsActive AS DangHoatDong,
    p.Location.Lat AS ViDo, p.Location.Long AS KinhDo,
    COALESCE(l.LuotMoDiem, 0) AS LuotMoDiem, COALESCE(l.LuotBatDauNghe, 0) AS LuotBatDauNghe
FROM dbo.Pois p
LEFT JOIN dbo.PoiTranslations t ON t.PoiId = p.Id AND t.LanguageCode = N'vi'
LEFT JOIN LuotTuongTac l ON l.PoiId = p.Id
ORDER BY p.Floor, p.Id
OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;

-- 2. Nội dung thuyết minh theo ngôn ngữ.
SELECT p.Code AS MaDiaDiem, t.LanguageCode AS NgonNgu, t.Title AS TieuDe, t.Description AS NoiDung
FROM dbo.PoiTranslations t
JOIN dbo.Pois p ON p.Id = t.PoiId
ORDER BY p.Id, CASE t.LanguageCode WHEN N'vi' THEN 0 WHEN N'en' THEN 1
    WHEN N'zh' THEN 2 WHEN N'fr' THEN 3 WHEN N'ru' THEN 4 ELSE 5 END, t.Id
OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;

-- 3. Thiết bị đã ghi nhận. Phiên trực tuyến được theo dõi trong Admin.
SELECT Id AS MaThietBi, DeviceName AS TenThietBi, Platform AS NenTang, Browser AS TrinhDuyet,
    CASE WHEN IsBlocked = 1 THEN N'Đang chặn' ELSE N'Được truy cập' END AS QuyenTruyCap
FROM dbo.DeviceSessions
ORDER BY LastActiveAt DESC, Id
OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;

-- 4. Nhật ký sử dụng theo giờ Việt Nam.
SELECT CASE EventType WHEN N'GateEntry' THEN N'Vào cổng'
        WHEN N'PoiViewed' THEN N'Mở địa điểm' WHEN N'PlayStarted' THEN N'Bắt đầu nghe' END AS SuKien,
    PoiCode AS MaDiaDiem, LanguageCode AS NgonNgu, DATEADD(HOUR, 7, ScannedAt) AS GioVietNam
FROM dbo.QrScanLogs
WHERE IsUtc = 1 AND EventType IN (N'GateEntry', N'PoiViewed', N'PlayStarted')
ORDER BY ScannedAt DESC, Id DESC
OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
