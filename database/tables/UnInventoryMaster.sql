-- =============================================
-- UN在庫マスタテーブル（アンマッチチェック専用）
-- 作成日: 2025-07-27
-- 説明: アンマッチチェック専用の一時在庫マスタ
--       CP在庫マスタ（本番データ）とは完全に分離
-- =============================================
USE InventoryManagementDB;
GO

-- 既存テーブルが存在する場合は削除
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UnInventoryMaster]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[UnInventoryMaster];
    PRINT 'UnInventoryMaster テーブルを削除しました';
END
GO

CREATE TABLE UnInventoryMaster (
    -- 5項目複合キー
    ProductCode NVARCHAR(15) NOT NULL,
    GradeCode NVARCHAR(15) NOT NULL,
    ClassCode NVARCHAR(15) NOT NULL,
    ShippingMarkCode NVARCHAR(15) NOT NULL,
    ShippingMarkName NVARCHAR(50) NOT NULL,
    
    -- 管理項目
    DataSetId NVARCHAR(100) NOT NULL,
    
    -- 在庫数量（アンマッチチェック用、最小限の項目のみ）
    PreviousDayStock DECIMAL(18,4) DEFAULT 0,
    DailyStock DECIMAL(18,4) DEFAULT 0,
    DailyFlag CHAR(1) DEFAULT '9',
    
    -- 日付・監査項目
    JobDate DATE,
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    UpdatedDate DATETIME2 DEFAULT GETDATE(),
    
    -- 複合主キー（5項目キー + DataSetId）
    CONSTRAINT PK_UnInventoryMaster PRIMARY KEY (
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, DataSetId
    )
);
GO

-- インデックス作成
CREATE INDEX IX_UnInventoryMaster_DataSetId 
ON UnInventoryMaster(DataSetId);

CREATE INDEX IX_UnInventoryMaster_JobDate 
ON UnInventoryMaster(JobDate);

CREATE INDEX IX_UnInventoryMaster_DailyFlag 
ON UnInventoryMaster(DailyFlag);

-- 5項目キー検索用の複合インデックス
CREATE INDEX IX_UnInventoryMaster_InventoryKey 
ON UnInventoryMaster(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);

PRINT 'UnInventoryMaster テーブルを作成しました';
PRINT '- 用途: アンマッチチェック専用の一時在庫マスタ';
PRINT '- 特徴: 伝票に存在する5項目キーのみ作成される';
PRINT '- 削除: 日次終了処理時に削除される';
GO