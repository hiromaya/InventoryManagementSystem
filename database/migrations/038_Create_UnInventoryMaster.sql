-- 038_Create_UnInventoryMaster.sql
-- UnInventoryMasterテーブル作成（アンマッチチェック専用一時テーブル）
-- DataSetId管理を廃止し、使い捨てテーブル設計として再構築
-- 作成日: 2025-07-30

PRINT '038_Create_UnInventoryMaster: 開始';

-- ===================================================
-- UnInventoryMasterテーブル作成
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UnInventoryMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE UnInventoryMaster (
        -- 5項目複合キー
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ShippingMarkName NVARCHAR(50) NOT NULL,
        
        -- 在庫数量（アンマッチチェック用、最小限の項目のみ）
        PreviousDayStock DECIMAL(18,4) DEFAULT 0,
        DailyStock DECIMAL(18,4) DEFAULT 0,
        DailyFlag CHAR(1) DEFAULT '9',
        
        -- 日付・監査項目
        JobDate DATE,
        CreatedDate DATETIME2 DEFAULT GETDATE(),
        UpdatedDate DATETIME2 DEFAULT GETDATE(),
        
        -- 複合主キー（5項目キーのみ、DataSetIdは含まない）
        CONSTRAINT PK_UnInventoryMaster PRIMARY KEY (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
        )
    );
    
    PRINT '✓ UnInventoryMasterテーブルを作成しました（DataSetIdなし設計）';
    
    -- インデックス作成
    CREATE INDEX IX_UnInventoryMaster_JobDate ON UnInventoryMaster(JobDate);
    CREATE INDEX IX_UnInventoryMaster_ProductCode ON UnInventoryMaster(ProductCode);
    CREATE INDEX IX_UnInventoryMaster_DailyFlag ON UnInventoryMaster(DailyFlag);
    
    PRINT '✓ UnInventoryMasterテーブルのインデックスを作成しました';
END
ELSE
BEGIN
    PRINT '- UnInventoryMasterテーブルは既に存在します';
    
    -- DataSetIdカラムが存在する場合は警告表示
    IF EXISTS (
        SELECT * FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[UnInventoryMaster]') 
        AND name = 'DataSetId'
    )
    BEGIN
        PRINT '⚠️ 既存のUnInventoryMasterテーブルにDataSetIdカラムが存在します';
        PRINT '   039_DropDataSetIdFromUnInventoryMaster.sqlで削除される予定です';
    END
END

PRINT '038_Create_UnInventoryMaster: 完了';