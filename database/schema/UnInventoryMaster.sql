-- UnInventoryMaster テーブル作成
-- アンマッチチェック専用の在庫マスタ（数量のみ管理）

CREATE TABLE UnInventoryMaster (
    -- 5項目複合キー
    ProductCode NVARCHAR(15) NOT NULL,
    GradeCode NVARCHAR(15) NOT NULL,
    ClassCode NVARCHAR(15) NOT NULL,
    ShippingMarkCode NVARCHAR(15) NOT NULL,
    ShippingMarkName NVARCHAR(50) NOT NULL,
    DataSetId NVARCHAR(100) NOT NULL,
    
    -- 数量データ（アンマッチチェック用）
    PreviousDayStock DECIMAL(18,4) DEFAULT 0,
    DailyStock DECIMAL(18,4) DEFAULT 0,
    DailyFlag CHAR(1) DEFAULT '9',
    
    -- 日時データ
    JobDate DATE NULL,
    
    -- メタデータ
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    UpdatedDate DATETIME2 DEFAULT GETDATE(),
    
    PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, DataSetId)
);

-- インデックス作成
CREATE INDEX IX_UnInventoryMaster_DataSetId ON UnInventoryMaster (DataSetId);
CREATE INDEX IX_UnInventoryMaster_JobDate ON UnInventoryMaster (JobDate);
CREATE INDEX IX_UnInventoryMaster_DailyFlag ON UnInventoryMaster (DailyFlag);

-- テーブル作成完了メッセージ
PRINT 'UnInventoryMaster テーブルが正常に作成されました。';