-- =============================================
-- UN在庫マスタテーブル（アンマッチチェック専用・使い捨て設計）
-- 作成日: 2025-07-27
-- 修正日: 2025-07-30
-- 説明: アンマッチチェック専用の一時在庫マスタ
--       CP在庫マスタ（本番データ）とは完全に分離
--       DataSetId管理を廃止し、使い捨てテーブル設計に変更
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
    
    -- 在庫数量（アンマッチチェック用、最小限の項目のみ）
    PreviousDayStock DECIMAL(18,4) DEFAULT 0,
    DailyStock DECIMAL(18,4) DEFAULT 0,
    DailyFlag CHAR(1) DEFAULT '9',
    
    -- 日付・監査項目
    JobDate DATE,
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    UpdatedDate DATETIME2 DEFAULT GETDATE(),
    
    -- 複合主キー（5項目キーのみ）
    CONSTRAINT PK_UnInventoryMaster PRIMARY KEY (
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    )
);
GO

-- インデックス作成
CREATE INDEX IX_UnInventoryMaster_JobDate 
ON UnInventoryMaster(JobDate);

CREATE INDEX IX_UnInventoryMaster_ProductCode 
ON UnInventoryMaster(ProductCode);

CREATE INDEX IX_UnInventoryMaster_DailyFlag 
ON UnInventoryMaster(DailyFlag);

PRINT 'UnInventoryMaster テーブルを作成しました（使い捨て設計）';
PRINT '- 用途: アンマッチチェック専用の一時在庫マスタ';
PRINT '- 特徴: 伝票に存在する5項目キーのみ作成される';
PRINT '- 設計: DataSetId管理なし、TRUNCATEによる高速全削除';
PRINT '- 削除: 日次終了処理時に削除される';
GO