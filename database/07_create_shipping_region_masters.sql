-- 荷印マスタテーブル作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ShippingMarkMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ShippingMarkMaster](
        [ShippingMarkCode] [nvarchar](10) NOT NULL,
        [ShippingMarkName] [nvarchar](50) NOT NULL,
        [SearchKana] [nvarchar](50) NULL,
        [NumericValue1] [decimal](18, 4) NULL,
        [NumericValue2] [decimal](18, 4) NULL,
        [NumericValue3] [decimal](18, 4) NULL,
        [NumericValue4] [decimal](18, 4) NULL,
        [NumericValue5] [decimal](18, 4) NULL,
        [DateValue1] [datetime] NULL,
        [DateValue2] [datetime] NULL,
        [DateValue3] [datetime] NULL,
        [DateValue4] [datetime] NULL,
        [DateValue5] [datetime] NULL,
        [TextValue1] [nvarchar](255) NULL,
        [TextValue2] [nvarchar](255) NULL,
        [TextValue3] [nvarchar](255) NULL,
        [TextValue4] [nvarchar](255) NULL,
        [TextValue5] [nvarchar](255) NULL,
        CONSTRAINT [PK_ShippingMarkMaster] PRIMARY KEY CLUSTERED 
        (
            [ShippingMarkCode] ASC
        )
    )
    PRINT 'テーブル [ShippingMarkMaster] を作成しました。'
END
ELSE
BEGIN
    PRINT 'テーブル [ShippingMarkMaster] は既に存在します。'
END
GO

-- 産地マスタテーブル作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RegionMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RegionMaster](
        [RegionCode] [nvarchar](10) NOT NULL,
        [RegionName] [nvarchar](50) NOT NULL,
        [SearchKana] [nvarchar](50) NULL,
        [NumericValue1] [decimal](18, 4) NULL,
        [NumericValue2] [decimal](18, 4) NULL,
        [NumericValue3] [decimal](18, 4) NULL,
        [NumericValue4] [decimal](18, 4) NULL,
        [NumericValue5] [decimal](18, 4) NULL,
        [DateValue1] [datetime] NULL,
        [DateValue2] [datetime] NULL,
        [DateValue3] [datetime] NULL,
        [DateValue4] [datetime] NULL,
        [DateValue5] [datetime] NULL,
        [TextValue1] [nvarchar](255) NULL,
        [TextValue2] [nvarchar](255) NULL,
        [TextValue3] [nvarchar](255) NULL,
        [TextValue4] [nvarchar](255) NULL,
        [TextValue5] [nvarchar](255) NULL,
        CONSTRAINT [PK_RegionMaster] PRIMARY KEY CLUSTERED 
        (
            [RegionCode] ASC
        )
    )
    PRINT 'テーブル [RegionMaster] を作成しました。'
END
ELSE
BEGIN
    PRINT 'テーブル [RegionMaster] は既に存在します。'
END
GO

-- インデックス作成
-- 荷印マスタ検索用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ShippingMarkMaster_SearchKana' AND object_id = OBJECT_ID('ShippingMarkMaster'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ShippingMarkMaster_SearchKana] ON [dbo].[ShippingMarkMaster]
    (
        [SearchKana] ASC
    )
    PRINT 'インデックス [IX_ShippingMarkMaster_SearchKana] を作成しました。'
END

-- 産地マスタ検索用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RegionMaster_SearchKana' AND object_id = OBJECT_ID('RegionMaster'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RegionMaster_SearchKana] ON [dbo].[RegionMaster]
    (
        [SearchKana] ASC
    )
    PRINT 'インデックス [IX_RegionMaster_SearchKana] を作成しました。'
END
GO

-- 初期データ（未設定レコード）を追加
-- 荷印マスタ未設定
IF NOT EXISTS (SELECT * FROM ShippingMarkMaster WHERE ShippingMarkCode = '未設定')
BEGIN
    INSERT INTO ShippingMarkMaster (ShippingMarkCode, ShippingMarkName, SearchKana)
    VALUES ('未設定', '荷印未設定', 'ミセッテイ')
    PRINT '荷印マスタに未設定レコードを追加しました。'
END

-- 産地マスタ未設定
IF NOT EXISTS (SELECT * FROM RegionMaster WHERE RegionCode = '未設定')
BEGIN
    INSERT INTO RegionMaster (RegionCode, RegionName, SearchKana)
    VALUES ('未設定', '産地未設定', 'ミセッテイ')
    PRINT '産地マスタに未設定レコードを追加しました。'
END
GO