-- =============================================
-- 在庫マスタ統合マイグレーション
-- 作成日: 2025-09-11
-- 目的: InventoryCarryoverMasterをInventoryMasterに統合
-- 注意: 実行前にバックアップ必須。ロールバック手順を準備してください。
-- =============================================

USE InventoryManagementDB;
GO

SET XACT_ABORT ON;

-- 推奨: 事前バックアップ（任意）
-- SELECT * INTO InventoryMaster_Backup_20250911 FROM InventoryMaster;
-- SELECT * INTO InventoryCarryoverMaster_Backup_20250911 FROM InventoryCarryoverMaster;

BEGIN TRANSACTION;
BEGIN TRY

    -- Step 1: 新テーブル（正しい桁数・統合カラム）を作成
    IF OBJECT_ID('dbo.InventoryMaster_New', 'U') IS NOT NULL
        DROP TABLE dbo.InventoryMaster_New;

    CREATE TABLE dbo.InventoryMaster_New (
        ProductCode        NVARCHAR(5)   NOT NULL,
        GradeCode          NVARCHAR(3)   NOT NULL,
        ClassCode          NVARCHAR(3)   NOT NULL,
        ShippingMarkCode   NVARCHAR(4)   NOT NULL,
        ManualShippingMark NVARCHAR(8)   NOT NULL,

        ProductName        NVARCHAR(100) NOT NULL DEFAULT N'',
        Unit               NVARCHAR(20)  NOT NULL DEFAULT N'PCS',
        StandardPrice      DECIMAL(18,4) NOT NULL DEFAULT 0,
        AveragePrice       DECIMAL(18,4) NOT NULL DEFAULT 0,
        ProductCategory1   NVARCHAR(10)  NOT NULL DEFAULT N'',
        ProductCategory2   NVARCHAR(10)  NOT NULL DEFAULT N'',

        -- Carryover統合（前残）
        CarryoverQuantity  DECIMAL(18,4) NOT NULL DEFAULT 0,
        CarryoverAmount    DECIMAL(18,4) NOT NULL DEFAULT 0,
        CarryoverUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,

        -- 在庫（当日/現在）
        CurrentStock       DECIMAL(18,4) NOT NULL DEFAULT 0,
        CurrentStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyStock         DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyStockAmount   DECIMAL(18,4) NOT NULL DEFAULT 0,

        -- 管理情報
        JobDate            DATE           NOT NULL,
        DataSetId          NVARCHAR(100)  NOT NULL DEFAULT N'',
        ImportType         NVARCHAR(20)   NOT NULL DEFAULT N'UNKNOWN',
        Origin             NVARCHAR(20)   NOT NULL DEFAULT N'UNKNOWN',
        IsActive           BIT            NOT NULL DEFAULT 1,
        DailyFlag          CHAR(1)        NOT NULL DEFAULT '9',
        LastReceiptDate    DATE           NULL,
        LastSalesDate      DATE           NULL,
        LastPurchaseDate   DATE           NULL,

        -- 監査
        CreatedDate        DATETIME2      NOT NULL DEFAULT (GETDATE()),
        UpdatedDate        DATETIME2      NOT NULL DEFAULT (GETDATE()),
        CreatedBy          NVARCHAR(50)   NOT NULL DEFAULT N'system',

        CONSTRAINT PK_InventoryMaster_New PRIMARY KEY (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
        )
    );

    -- Step 2: インデックス
    CREATE INDEX IX_InventoryMaster_New_JobDate ON dbo.InventoryMaster_New(JobDate);
    CREATE INDEX IX_InventoryMaster_New_DataSetId ON dbo.InventoryMaster_New(DataSetId);
    CREATE INDEX IX_InventoryMaster_New_IsActive ON dbo.InventoryMaster_New(IsActive) WHERE IsActive = 1;
    CREATE INDEX IX_InventoryMaster_New_Composite ON dbo.InventoryMaster_New(
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, JobDate
    );

    -- Step 3: 既存InventoryMasterデータを移行（桁数正規化 + 前残はPreviousMonth*→Carryover*に移送）
    INSERT INTO dbo.InventoryMaster_New (
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
        ProductName, Unit, StandardPrice, AveragePrice, ProductCategory1, ProductCategory2,
        CarryoverQuantity, CarryoverAmount, CarryoverUnitPrice,
        CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
        JobDate, DataSetId, ImportType, Origin, IsActive, DailyFlag,
        LastReceiptDate, LastSalesDate, LastPurchaseDate,
        CreatedDate, UpdatedDate, CreatedBy
    )
    SELECT 
        RIGHT('00000' + CAST(ProductCode AS NVARCHAR(50)), 5),
        RIGHT('000'   + CAST(GradeCode AS NVARCHAR(50)), 3),
        RIGHT('000'   + CAST(ClassCode AS NVARCHAR(50)), 3),
        RIGHT('0000'  + CAST(ShippingMarkCode AS NVARCHAR(50)), 4),
        LEFT(RTRIM(COALESCE(ManualShippingMark, N'')) + REPLICATE(' ', 8), 8),
        ProductName, Unit, StandardPrice, ISNULL(AveragePrice, 0), ProductCategory1, ProductCategory2,
        ISNULL(PreviousMonthQuantity, 0), ISNULL(PreviousMonthAmount, 0), 0,
        ISNULL(CurrentStock, 0), ISNULL(CurrentStockAmount, 0), ISNULL(DailyStock, 0), ISNULL(DailyStockAmount, 0),
        JobDate, DataSetId, ImportType, N'INVENTORY_MASTER', ISNULL(IsActive, 1), DailyFlag,
        NULL, NULL, NULL,
        CreatedDate, UpdatedDate, N'migration'
    FROM dbo.InventoryMaster WITH (NOLOCK);

    -- Step 4: Carryoverから統合（IsActive=1のみ）
    IF OBJECT_ID('dbo.InventoryCarryoverMaster', 'U') IS NOT NULL
    BEGIN
        MERGE dbo.InventoryMaster_New AS target
        USING (
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                ProductName, Unit, ProductCategory1, ProductCategory2,
                CarryoverQuantity, CarryoverAmount, CarryoverUnitPrice,
                LastReceiptDate, JobDate, DataSetId, ImportType, Origin, IsActive
            FROM dbo.InventoryCarryoverMaster WITH (NOLOCK)
            WHERE IsActive = 1
        ) AS source
        ON (
            target.ProductCode = source.ProductCode AND
            target.GradeCode = source.GradeCode AND
            target.ClassCode = source.ClassCode AND
            target.ShippingMarkCode = source.ShippingMarkCode AND
            target.ManualShippingMark = source.ManualShippingMark
        )
        WHEN MATCHED THEN
            UPDATE SET 
                target.CarryoverQuantity  = ISNULL(source.CarryoverQuantity, 0),
                target.CarryoverAmount    = ISNULL(source.CarryoverAmount, 0),
                target.CarryoverUnitPrice = ISNULL(source.CarryoverUnitPrice, 0),
                target.LastReceiptDate    = source.LastReceiptDate,
                target.Origin             = source.Origin,
                target.UpdatedDate        = GETDATE()
        WHEN NOT MATCHED THEN
            INSERT (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                ProductName, Unit, ProductCategory1, ProductCategory2,
                CarryoverQuantity, CarryoverAmount, CarryoverUnitPrice,
                JobDate, DataSetId, ImportType, Origin, IsActive, LastReceiptDate,
                CreatedDate, UpdatedDate, CreatedBy
            )
            VALUES (
                source.ProductCode, source.GradeCode, source.ClassCode,
                source.ShippingMarkCode, source.ManualShippingMark,
                source.ProductName, source.Unit, source.ProductCategory1, source.ProductCategory2,
                ISNULL(source.CarryoverQuantity, 0), ISNULL(source.CarryoverAmount, 0), ISNULL(source.CarryoverUnitPrice, 0),
                source.JobDate, source.DataSetId, source.ImportType, source.Origin, 1, source.LastReceiptDate,
                GETDATE(), GETDATE(), N'migration'
            );
    END

    -- Step 5: テーブル入れ替え
    EXEC sp_rename 'dbo.InventoryMaster', 'InventoryMaster_Old';
    EXEC sp_rename 'dbo.InventoryMaster_New', 'InventoryMaster';

    COMMIT TRANSACTION;
    PRINT N'InventoryMaster統合マイグレーション完了';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @Err nvarchar(4000) = ERROR_MESSAGE();
    RAISERROR(@Err, 16, 1);
END CATCH;

-- 参考: ロールバック手順
-- EXEC sp_rename 'dbo.InventoryMaster', 'InventoryMaster_Failed';
-- EXEC sp_rename 'dbo.InventoryMaster_Old', 'InventoryMaster';

