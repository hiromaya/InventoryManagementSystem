-- =====================================================
-- 030_CreateGradeMaster.sql
-- 作成日: 2025-07-17
-- 目的: GradeMasterテーブルの作成
-- 説明: 等級マスタテーブルを作成する。販売大臣の等級汎用マスタに対応。
-- =====================================================

USE InventoryManagementDB;
GO

-- GradeMaster（等級マスタ）テーブル作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GradeMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[GradeMaster] (
        [GradeCode] NVARCHAR(15) NOT NULL,
        [GradeName] NVARCHAR(50) NOT NULL,
        [SearchKana] NVARCHAR(100) NULL,
        [NumericValue1] DECIMAL(16,4) NULL,
        [NumericValue2] DECIMAL(16,4) NULL,
        [NumericValue3] DECIMAL(16,4) NULL,
        [NumericValue4] DECIMAL(16,4) NULL,
        [NumericValue5] DECIMAL(16,4) NULL,
        [DateValue1] DATE NULL,
        [DateValue2] DATE NULL,
        [DateValue3] DATE NULL,
        [DateValue4] DATE NULL,
        [DateValue5] DATE NULL,
        [TextValue1] NVARCHAR(255) NULL,
        [TextValue2] NVARCHAR(255) NULL,
        [TextValue3] NVARCHAR(255) NULL,
        [TextValue4] NVARCHAR(255) NULL,
        [TextValue5] NVARCHAR(255) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_GradeMaster] PRIMARY KEY CLUSTERED ([GradeCode])
    );
    
    -- インデックス作成
    CREATE NONCLUSTERED INDEX [IX_GradeMaster_GradeName] 
    ON [dbo].[GradeMaster] ([GradeName]);
    
    CREATE NONCLUSTERED INDEX [IX_GradeMaster_SearchKana] 
    ON [dbo].[GradeMaster] ([SearchKana]) 
    WHERE [SearchKana] IS NOT NULL;
    
    PRINT 'GradeMasterテーブルを作成しました';
    
    -- 5項目複合キーで使用される代表的な等級コードを初期データとして挿入
    INSERT INTO [dbo].[GradeMaster] ([GradeCode], [GradeName], [SearchKana])
    VALUES 
        ('000', '未指定', 'ミシテイ'),
        ('001', '特級', 'トツキユウ'),
        ('002', '1級', 'イチキユウ'),
        ('003', '2級', 'ニキユウ'),
        ('004', '3級', 'サンキユウ');
        
    PRINT '初期データを5件挿入しました';
END
ELSE
BEGIN
    PRINT 'GradeMasterテーブルは既に存在します';
END
GO

PRINT 'Migration 030: GradeMasterテーブルの作成が完了しました';