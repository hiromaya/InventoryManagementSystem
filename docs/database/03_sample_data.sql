-- ==================================================
-- サンプルデータ作成スクリプト
-- ==================================================

USE InventoryManagementDB;
GO

-- サンプルのジョブデート
DECLARE @JobDate DATE = '2025-06-16';
DECLARE @DataSetId NVARCHAR(50) = 'SAMPLE_' + FORMAT(@JobDate, 'yyyyMMdd') + '_001';

-- ==================================================
-- 在庫マスタのサンプルデータ
-- ==================================================
INSERT INTO InventoryMaster (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
    JobDate, CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
    DailyFlag, DataSetId
) VALUES 
-- 通常の商品
('APPLE001', 'A', 'L', 'MK001', '青森産', 'りんご 特級 Lサイズ', '箱', 1500.00, '1', 'A1', @JobDate, 50.0, 75000.00, 50.0, 75000.00, '0', @DataSetId),
('APPLE001', 'A', 'M', 'MK001', '青森産', 'りんご 特級 Mサイズ', '箱', 1200.00, '1', 'A1', @JobDate, 80.0, 96000.00, 80.0, 96000.00, '0', @DataSetId),
('ORANGE001', 'B', 'L', 'MK002', '愛媛産', 'みかん 優級 Lサイズ', '箱', 800.00, '2', 'B1', @JobDate, 120.0, 96000.00, 120.0, 96000.00, '0', @DataSetId),

-- 除外対象の商品（EXITで始まる荷印名）
('TEST001', 'X', 'X', '9900', 'EXIT_TEST', 'テスト商品', '個', 100.00, '9', 'X1', @JobDate, 10.0, 1000.00, 10.0, 1000.00, '0', @DataSetId),

-- 特殊処理ルール対象（9aaaで始まる荷印名）
('SPECIAL001', 'S', 'S', 'MK003', '9aaa特殊商品', '特殊商品A', '個', 2000.00, '1', 'S1', @JobDate, 5.0, 10000.00, 5.0, 10000.00, '0', @DataSetId);

-- ==================================================
-- 売上伝票のサンプルデータ
-- ==================================================
INSERT INTO SalesVoucher (
    VoucherId, LineNumber, VoucherDate, JobDate,
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    Quantity, SalesUnitPrice, SalesAmount, InventoryUnitPrice, DataSetId
) VALUES 
(1001, 1, @JobDate, @JobDate, 'APPLE001', 'A', 'L', 'MK001', '青森産', 10.0, 1800.00, 18000.00, 1500.00, @DataSetId),
(1001, 2, @JobDate, @JobDate, 'APPLE001', 'A', 'M', 'MK001', '青森産', 15.0, 1400.00, 21000.00, 1200.00, @DataSetId),
(1002, 1, @JobDate, @JobDate, 'ORANGE001', 'B', 'L', 'MK002', '愛媛産', 20.0, 1000.00, 20000.00, 800.00, @DataSetId);

-- ==================================================
-- 仕入伝票のサンプルデータ
-- ==================================================
INSERT INTO PurchaseVoucher (
    VoucherId, LineNumber, VoucherDate, JobDate,
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    Quantity, PurchaseUnitPrice, PurchaseAmount, DataSetId
) VALUES 
(2001, 1, @JobDate, @JobDate, 'APPLE001', 'A', 'L', 'MK001', '青森産', 30.0, 1400.00, 42000.00, @DataSetId),
(2001, 2, @JobDate, @JobDate, 'APPLE001', 'A', 'M', 'MK001', '青森産', 40.0, 1100.00, 44000.00, @DataSetId),
(2002, 1, @JobDate, @JobDate, 'ORANGE001', 'B', 'L', 'MK002', '愛媛産', 50.0, 700.00, 35000.00, @DataSetId);

-- ==================================================
-- 処理履歴のサンプルデータ
-- ==================================================
INSERT INTO ProcessingHistory (
    DataSetId, ProcessType, JobDate, ProcessedAt, ProcessedBy, Status, ProcessedRecords, Note
) VALUES 
(@DataSetId, 'DAILY_CLEAR', @JobDate, GETDATE(), 'SYSTEM', 'SUCCESS', 5, '当日エリアクリア処理'),
(@DataSetId, 'SALES_IMPORT', @JobDate, GETDATE(), 'SYSTEM', 'SUCCESS', 3, '売上データ取込処理'),
(@DataSetId, 'PURCHASE_IMPORT', @JobDate, GETDATE(), 'SYSTEM', 'SUCCESS', 3, '仕入データ取込処理'),
(@DataSetId, 'INVENTORY_CALC', @JobDate, GETDATE(), 'SYSTEM', 'SUCCESS', 5, '在庫計算処理'),
(@DataSetId, 'GROSS_PROFIT_CALC', @JobDate, GETDATE(), 'SYSTEM', 'SUCCESS', 5, '粗利計算処理');

GO

PRINT 'サンプルデータの作成が完了しました。';
PRINT 'ジョブデート: ' + CAST(@JobDate AS NVARCHAR(10));
PRINT 'データセットID: ' + @DataSetId;
GO