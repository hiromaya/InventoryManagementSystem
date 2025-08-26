-- =====================================================
-- 029_CreateShippingMarkMaster.sql
-- 作成日: 2025-07-16
-- 目的: ShippingMarkMasterテーブルの作成
-- 説明: 荷印マスタテーブルを作成する。各種マスタ情報を保持。
-- =====================================================

-- テーブルが存在しない場合のみ、すべてを作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ShippingMarkMaster]') AND type in (N'U'))
BEGIN
    -- テーブル作成
    CREATE TABLE [dbo].[ShippingMarkMaster] (
        [ShippingMarkCode] NVARCHAR(15) NOT NULL,
        [ShippingMarkName] NVARCHAR(100) NOT NULL,
        [SearchKana] NVARCHAR(100) NULL,
        [NumericValue1] DECIMAL(18,4) NULL,
        [NumericValue2] DECIMAL(18,4) NULL,
        [NumericValue3] DECIMAL(18,4) NULL,
        [NumericValue4] DECIMAL(18,4) NULL,
        [NumericValue5] DECIMAL(18,4) NULL,
        [DateValue1] DATE NULL,
        [DateValue2] DATE NULL,
        [DateValue3] DATE NULL,
        [DateValue4] DATE NULL,
        [DateValue5] DATE NULL,
        [TextValue1] NVARCHAR(100) NULL,
        [TextValue2] NVARCHAR(100) NULL,
        [TextValue3] NVARCHAR(100) NULL,
        [TextValue4] NVARCHAR(100) NULL,
        [TextValue5] NVARCHAR(100) NULL,
        CONSTRAINT [PK_ShippingMarkMaster] PRIMARY KEY CLUSTERED ([ShippingMarkCode])
    );

    -- インデックス作成
    CREATE NONCLUSTERED INDEX [IX_ShippingMarkMaster_ShippingMarkName] 
    ON [dbo].[ShippingMarkMaster] ([ShippingMarkName]);
    
    CREATE NONCLUSTERED INDEX [IX_ShippingMarkMaster_SearchKana] 
    ON [dbo].[ShippingMarkMaster] ([SearchKana]) 
    WHERE [SearchKana] IS NOT NULL;

    PRINT 'ShippingMarkMasterテーブルを作成しました';
END
ELSE
BEGIN
    PRINT 'ShippingMarkMasterテーブルは既に存在します';
END