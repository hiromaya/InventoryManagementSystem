-- 029_CreateShippingMarkMaster.sql
-- ShippingMarkMasterテーブルの作成
-- 作成日: 2025-07-16
-- 説明: 荷印マスタテーブルを作成する。各種マスタ情報を保持。

CREATE TABLE ShippingMarkMaster (
    ShippingMarkCode NVARCHAR(15) NOT NULL PRIMARY KEY,
    ShippingMarkName NVARCHAR(100) NOT NULL,
    SearchKana NVARCHAR(100) NULL,
    NumericValue1 DECIMAL(18,4) NULL,
    NumericValue2 DECIMAL(18,4) NULL,
    NumericValue3 DECIMAL(18,4) NULL,
    NumericValue4 DECIMAL(18,4) NULL,
    NumericValue5 DECIMAL(18,4) NULL,
    DateValue1 DATE NULL,
    DateValue2 DATE NULL,
    DateValue3 DATE NULL,
    DateValue4 DATE NULL,
    DateValue5 DATE NULL,
    TextValue1 NVARCHAR(100) NULL,
    TextValue2 NVARCHAR(100) NULL,
    TextValue3 NVARCHAR(100) NULL,
    TextValue4 NVARCHAR(100) NULL,
    TextValue5 NVARCHAR(100) NULL
);

-- インデックスの作成
CREATE INDEX IX_ShippingMarkMaster_ShippingMarkName ON ShippingMarkMaster(ShippingMarkName);
CREATE INDEX IX_ShippingMarkMaster_SearchKana ON ShippingMarkMaster(SearchKana);

-- 説明情報のコメント
EXEC sp_addextendedproperty 
    'MS_Description', '荷印マスタテーブル', 
    'SCHEMA', 'dbo', 
    'TABLE', 'ShippingMarkMaster';

EXEC sp_addextendedproperty 
    'MS_Description', '荷印コード（主キー）', 
    'SCHEMA', 'dbo', 
    'TABLE', 'ShippingMarkMaster', 
    'COLUMN', 'ShippingMarkCode';

EXEC sp_addextendedproperty 
    'MS_Description', '荷印名', 
    'SCHEMA', 'dbo', 
    'TABLE', 'ShippingMarkMaster', 
    'COLUMN', 'ShippingMarkName';

EXEC sp_addextendedproperty 
    'MS_Description', '検索用カナ', 
    'SCHEMA', 'dbo', 
    'TABLE', 'ShippingMarkMaster', 
    'COLUMN', 'SearchKana';