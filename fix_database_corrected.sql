-- ====================================================================
-- 手動実行用SQLスクリプト（データベース名修正版）
-- 作成日: 2025-07-10
-- 対象データベース: InventoryManagementDB
-- 接続先: localhost\SQLEXPRESS
-- ====================================================================

USE [InventoryManagementDB];
GO

-- 1. PreviousMonthInventoryテーブルの作成（存在しない場合のみ）
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PreviousMonthInventory')
BEGIN
    CREATE TABLE [dbo].[PreviousMonthInventory](
        [ProductCode] [nvarchar](5) NOT NULL,
        [GradeCode] [nvarchar](3) NOT NULL,
        [ClassCode] [nvarchar](3) NOT NULL,
        [ShippingMarkCode] [nvarchar](4) NOT NULL,
        [ManualShippingMark] [nvarchar](8) NOT NULL,
        [ProductName] [nvarchar](50) NULL,
        [Quantity] [decimal](18, 2) NOT NULL DEFAULT (0),
        [Amount] [decimal](18, 2) NOT NULL DEFAULT (0),
        [UnitPrice] [decimal](18, 2) NOT NULL DEFAULT (0),
        [Unit] [nvarchar](10) NULL,
        [StandardPrice] [decimal](18, 2) NULL,
        [JobDate] [date] NOT NULL,
        [DataSetId] [nvarchar](50) NOT NULL,
        [CreatedDate] [datetime2](7) NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] [datetime2](7) NOT NULL DEFAULT (GETDATE())
    ) ON [PRIMARY];
    
    PRINT 'PreviousMonthInventoryテーブルを作成しました。';
END
ELSE
BEGIN
    PRINT 'PreviousMonthInventoryテーブルは既に存在します。';
END
GO

-- 2. CpInventoryMasterテーブルの確認
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CpInventoryMaster')
BEGIN
    PRINT 'エラー: CpInventoryMasterテーブルが存在しません。';
    PRINT 'データベース初期化(dotnet run init-database --force)を実行してください。';
END
ELSE
BEGIN
    PRINT 'CpInventoryMasterテーブルが存在します。';
END
GO

-- 3. 5項目キー複合インデックスの作成
-- InventoryMaster用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_5ItemKey')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InventoryMaster_5ItemKey] ON [dbo].[InventoryMaster]
    (
        [ProductCode] ASC,
        [GradeCode] ASC,
        [ClassCode] ASC,
        [ShippingMarkCode] ASC,
        [ManualShippingMark] ASC
    )
    INCLUDE ([Quantity], [Amount], [UnitPrice], [UpdatedDate])
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
    ON [PRIMARY];
    
    PRINT 'InventoryMaster用5項目キー複合インデックスを作成しました。';
END
ELSE
BEGIN
    PRINT 'InventoryMaster用5項目キー複合インデックスは既に存在します。';
END
GO

-- CpInventoryMaster用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CpInventoryMaster_5ItemKey')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CpInventoryMaster_5ItemKey] ON [dbo].[CpInventoryMaster]
    (
        [ProductCode] ASC,
        [GradeCode] ASC,
        [ClassCode] ASC,
        [ShippingMarkCode] ASC,
        [ManualShippingMark] ASC
    )
    INCLUDE ([JobDate], [DataSetId], [DailyStock], [DailyStockAmount])
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
    ON [PRIMARY];
    
    PRINT 'CpInventoryMaster用5項目キー複合インデックスを作成しました。';
END
ELSE
BEGIN
    PRINT 'CpInventoryMaster用5項目キー複合インデックスは既に存在します。';
END
GO

-- 4. 完了メッセージ
PRINT '=== データベース修正が完了しました ===';
PRINT '次のステップ:';
PRINT '1. ストアドプロシージャファイルを実行してください:';
PRINT '   sqlcmd -S localhost\SQLEXPRESS -d InventoryManagementDB -E -i database\procedures\sp_CreateCpInventoryFromInventoryMasterCumulative.sql';
PRINT '2. アンマッチリスト処理を実行してください:';
PRINT '   dotnet run unmatch-list 2025-06-01';
GO