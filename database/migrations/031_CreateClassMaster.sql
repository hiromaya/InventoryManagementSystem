-- =====================================================
-- 031_CreateClassMaster.sql
-- 作成日: 2025-07-17
-- 目的: ClassMasterテーブルの作成
-- 説明: 階級マスタテーブルを作成する。販売大臣の階級汎用マスタに対応。
-- =====================================================

USE InventoryManagementDB;
GO

-- ClassMaster（階級マスタ）テーブル作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ClassMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ClassMaster] (
        [ClassCode] NVARCHAR(15) NOT NULL,
        [ClassName] NVARCHAR(50) NOT NULL,
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
        CONSTRAINT [PK_ClassMaster] PRIMARY KEY CLUSTERED ([ClassCode])
    );
    
    -- インデックス作成
    CREATE NONCLUSTERED INDEX [IX_ClassMaster_ClassName] 
    ON [dbo].[ClassMaster] ([ClassName]);
    
    CREATE NONCLUSTERED INDEX [IX_ClassMaster_SearchKana] 
    ON [dbo].[ClassMaster] ([SearchKana]) 
    WHERE [SearchKana] IS NOT NULL;
    
    PRINT 'ClassMasterテーブルを作成しました';
    
    -- 5項目複合キーで使用される代表的な階級コードを初期データとして挿入
    INSERT INTO [dbo].[ClassMaster] ([ClassCode], [ClassName], [SearchKana])
    VALUES 
        ('000', '未指定', 'ミシテイ'),
        ('001', 'A級', 'エーキユウ'),
        ('002', 'B級', 'ビーキユウ'),
        ('003', 'C級', 'シーキユウ'),
        ('004', 'D級', 'ディーキユウ');
        
    PRINT '初期データを5件挿入しました';
END
ELSE
BEGIN
    PRINT 'ClassMasterテーブルは既に存在します';
END
GO

PRINT 'Migration 031: ClassMasterテーブルの作成が完了しました';