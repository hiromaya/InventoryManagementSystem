-- =============================================
-- 移行用在庫マスタ 作成スクリプト
-- 説明: 初期在庫/日次繰越の前残情報を永続化（在庫マスタとは別テーブル）
-- =============================================

IF OBJECT_ID('dbo.InventoryCarryoverMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryCarryoverMaster (
        -- 5項目キー
        ProductCode        NVARCHAR(5)   NOT NULL,
        GradeCode          NVARCHAR(3)   NOT NULL,
        ClassCode          NVARCHAR(3)   NOT NULL,
        ShippingMarkCode   NVARCHAR(4)   NOT NULL,
        ManualShippingMark NVARCHAR(8)   NOT NULL,

        -- 付帯情報
        ProductName        NVARCHAR(100) NOT NULL DEFAULT N'',
        Unit               NVARCHAR(10)  NOT NULL DEFAULT N'PCS',
        ProductCategory1   NVARCHAR(10)  NOT NULL DEFAULT N'',
        ProductCategory2   NVARCHAR(10)  NOT NULL DEFAULT N'',

        -- 前残（Carryover）
        CarryoverQuantity  DECIMAL(18,4) NOT NULL DEFAULT (0),
        CarryoverAmount    DECIMAL(18,4) NOT NULL DEFAULT (0),
        CarryoverUnitPrice DECIMAL(18,4) NOT NULL DEFAULT (0),

        -- 任意: 当日系（必要に応じて設定）
        CurrentStockQuantity  DECIMAL(18,4) NOT NULL DEFAULT (0),
        CurrentStockAmount    DECIMAL(18,4) NOT NULL DEFAULT (0),
        CurrentStockUnitPrice DECIMAL(18,4) NOT NULL DEFAULT (0),

        -- 管理
        JobDate           DATE           NOT NULL,
        DataSetId         NVARCHAR(50)   NOT NULL,
        ImportType        NVARCHAR(20)   NOT NULL,   -- 'INIT' or 'CARRYOVER'
        Origin            NVARCHAR(20)   NOT NULL,   -- 'INITIAL_IMPORT' or 'DAILY_CLOSE'
        CreatedAt         DATETIME       NOT NULL DEFAULT (GETDATE()),
        UpdatedAt         DATETIME       NOT NULL DEFAULT (GETDATE()),
        CreatedBy         NVARCHAR(50)   NOT NULL DEFAULT N'system',

        CONSTRAINT PK_InventoryCarryoverMaster PRIMARY KEY (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            JobDate, DataSetId
        )
    );

    CREATE INDEX IX_Carryover_5Key_JobDate ON dbo.InventoryCarryoverMaster (
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, JobDate
    );

    CREATE INDEX IX_Carryover_ImportType_JobDate ON dbo.InventoryCarryoverMaster (
        ImportType, JobDate
    );

    CREATE INDEX IX_Carryover_DataSetId ON dbo.InventoryCarryoverMaster (DataSetId);
END

