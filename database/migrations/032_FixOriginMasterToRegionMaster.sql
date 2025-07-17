-- =====================================================
-- 032_FixOriginMasterToRegionMaster.sql
-- 作成日: 2025-07-17
-- 目的: 産地マスタのテーブル名統一
-- 説明: OriginMaster テーブルを RegionMaster に統一する
-- 背景: エンティティクラスがRegionMasterだが、テーブル名がOriginMasterで不一致
-- =====================================================

USE InventoryManagementDB;
GO

PRINT '=== 032_FixOriginMasterToRegionMaster.sql 実行開始 ===';

-- OriginMasterテーブルの存在確認
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OriginMaster]') AND type in (N'U'))
BEGIN
    PRINT 'OriginMasterテーブルが見つかりました';
    
    -- RegionMasterテーブルの存在確認
    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RegionMaster]') AND type in (N'U'))
    BEGIN
        -- RegionMasterテーブルがない場合は、OriginMasterの名前を変更
        EXEC sp_rename 'OriginMaster', 'RegionMaster';
        PRINT '✓ OriginMasterテーブル名をRegionMasterに変更しました';
        
        -- インデックス名も変更（存在する場合）
        IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OriginMaster_OriginName' AND object_id = OBJECT_ID(N'[dbo].[RegionMaster]'))
        BEGIN
            EXEC sp_rename 'RegionMaster.IX_OriginMaster_OriginName', 'IX_RegionMaster_RegionName', 'INDEX';
            PRINT '✓ インデックス名を変更しました: IX_OriginMaster_OriginName → IX_RegionMaster_RegionName';
        END
        
        IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OriginMaster_SearchKana' AND object_id = OBJECT_ID(N'[dbo].[RegionMaster]'))
        BEGIN
            EXEC sp_rename 'RegionMaster.IX_OriginMaster_SearchKana', 'IX_RegionMaster_SearchKana', 'INDEX';
            PRINT '✓ インデックス名を変更しました: IX_OriginMaster_SearchKana → IX_RegionMaster_SearchKana';
        END
    END
    ELSE
    BEGIN
        -- 両方のテーブルが存在する場合は、データをマージしてOriginMasterを削除
        PRINT 'RegionMasterテーブルも存在します。データマージを実行します...';
        
        -- OriginMasterのデータをRegionMasterにマージ（重複回避）
        DECLARE @mergedCount INT = 0;
        
        INSERT INTO RegionMaster (RegionCode, RegionName, SearchKana, 
                                NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
                                DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
                                TextValue1, TextValue2, TextValue3, TextValue4, TextValue5)
        SELECT OriginCode as RegionCode, OriginName as RegionName, SearchKana,
               NumericValue1, NumericValue2, NumericValue3, NumericValue4, NumericValue5,
               DateValue1, DateValue2, DateValue3, DateValue4, DateValue5,
               TextValue1, TextValue2, TextValue3, TextValue4, TextValue5
        FROM OriginMaster 
        WHERE OriginCode NOT IN (SELECT RegionCode FROM RegionMaster);
        
        SET @mergedCount = @@ROWCOUNT;
        
        -- OriginMasterテーブルを削除
        DROP TABLE OriginMaster;
        PRINT '✓ OriginMasterのデータをRegionMasterにマージしました (' + CAST(@mergedCount AS VARCHAR) + '件)';
        PRINT '✓ OriginMasterテーブルを削除しました';
    END
END
ELSE
BEGIN
    PRINT 'OriginMasterテーブルは存在しません';
    
    -- RegionMasterテーブルの存在確認
    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RegionMaster]') AND type in (N'U'))
    BEGIN
        PRINT 'RegionMasterテーブルも存在しないため、新規作成します';
        
        -- RegionMasterテーブルを新規作成
        CREATE TABLE [dbo].[RegionMaster] (
            [RegionCode] NVARCHAR(15) NOT NULL,
            [RegionName] NVARCHAR(50) NOT NULL,
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
            CONSTRAINT [PK_RegionMaster] PRIMARY KEY CLUSTERED ([RegionCode])
        );
        
        -- インデックス作成
        CREATE NONCLUSTERED INDEX [IX_RegionMaster_RegionName] 
        ON [dbo].[RegionMaster] ([RegionName]);
        
        CREATE NONCLUSTERED INDEX [IX_RegionMaster_SearchKana] 
        ON [dbo].[RegionMaster] ([SearchKana]) 
        WHERE [SearchKana] IS NOT NULL;
        
        PRINT '✓ RegionMasterテーブルを新規作成しました';
        
        -- 初期データの挿入
        INSERT INTO [dbo].[RegionMaster] ([RegionCode], [RegionName], [SearchKana])
        VALUES 
            ('000', '未指定', 'ミシテイ'),
            ('001', '北海道', 'ホツカイドウ'),
            ('002', '青森', 'アオモリ'),
            ('003', '岩手', 'イワテ'),
            ('004', '宮城', 'ミヤギ');
            
        PRINT '✓ 初期データを5件挿入しました';
    END
    ELSE
    BEGIN
        PRINT 'RegionMasterテーブルは既に存在します';
    END
END

PRINT '';
PRINT '=== 032_FixOriginMasterToRegionMaster.sql 実行完了 ===';
PRINT 'Migration 032: 産地マスタのテーブル名統一が完了しました';
GO