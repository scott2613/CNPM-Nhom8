-- Chạy trên CSDL AudioGuide đang dùng. Có thể chạy lại.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @AdminAccountSchemaLock int;
    EXEC @AdminAccountSchemaLock = sys.sp_getapplock @Resource = N'AudioGuideSchema',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
    IF @AdminAccountSchemaLock < 0
        THROW 51020, N'Không lấy được khóa nâng cấp. Hãy thử lại.', 1;

    IF OBJECT_ID(N'dbo.Pois', N'U') IS NULL OR OBJECT_ID(N'dbo.QrCodes', N'U') IS NULL
        THROW 51021, N'Hãy chọn cơ sở dữ liệu AudioGuide đã khởi tạo trước khi chạy.', 1;

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

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
