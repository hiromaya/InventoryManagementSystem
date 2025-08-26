-- 在庫管理システム テストデータ投入
-- 作成日: 2025年6月16日

USE InventoryManagementDB;
GO

-- テストデータをクリア
DELETE FROM SalesVoucher;
DELETE FROM PurchaseVoucher;
DELETE FROM InventoryAdjustment;
DELETE FROM CpInventoryMaster;
DELETE FROM InventoryMaster;
DELETE FROM ProcessingHistory;

-- ===================================================================
-- 1. 在庫マスタ テストデータ
-- ===================================================================
INSERT INTO InventoryMaster (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
    JobDate, CreatedDate, UpdatedDate,
    CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount,
    DailyFlag
) VALUES
-- 通常商品
('00001', '001', '001', '1001', 'テスト荷印1', 'テスト商品A', 'KG', 1000.00, '1', '01', '2025-06-16', GETDATE(), GETDATE(), 100.00, 100000.00, 0.00, 0.00, '9'),
('00002', '001', '001', '1002', 'テスト荷印2', 'テスト商品B', 'KG', 1500.00, '2', '01', '2025-06-16', GETDATE(), GETDATE(), 200.00, 300000.00, 0.00, 0.00, '9'),
('00003', '002', '001', '1003', 'テスト荷印3', 'テスト商品C', 'KG', 800.00, '1', '02', '2025-06-16', GETDATE(), GETDATE(), 50.00, 40000.00, 0.00, 0.00, '9'),

-- 除外対象商品（EXIT）
('00004', '001', '001', '9900', 'EXIT荷印', 'テスト商品D', 'KG', 1200.00, '3', '01', '2025-06-16', GETDATE(), GETDATE(), 30.00, 36000.00, 0.00, 0.00, '9'),
('00005', '001', '001', '9910', 'テスト荷印5', 'テスト商品E', 'KG', 900.00, '1', '01', '2025-06-16', GETDATE(), GETDATE(), 80.00, 72000.00, 0.00, 0.00, '9'),

-- 特殊処理対象商品
('00006', '001', '001', '1006', '9aaa特殊1', 'テスト商品F', 'KG', 2000.00, '5', '01', '2025-06-16', GETDATE(), GETDATE(), 60.00, 120000.00, 0.00, 0.00, '9'),
('00007', '001', '001', '1007', '1aaa特殊2', 'テスト商品G', 'KG', 1800.00, '4', '01', '2025-06-16', GETDATE(), GETDATE(), 40.00, 72000.00, 0.00, 0.00, '9'),

-- 在庫0商品（アンマッチテスト用）
('00008', '001', '001', '1008', 'テスト荷印8', 'テスト商品H', 'KG', 1100.00, '2', '01', '2025-06-16', GETDATE(), GETDATE(), 0.00, 0.00, 0.00, 0.00, '9');

-- ===================================================================
-- 2. 売上伝票 テストデータ
-- ===================================================================
INSERT INTO SalesVoucher (
    VoucherNumber, VoucherType, DetailType, LineNumber, VoucherDate, JobDate,
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    CustomerCode, CustomerName, TransactionType,
    Quantity, UnitPrice, Amount, InventoryUnitPrice
) VALUES
-- 通常売上（在庫あり）
('S0001', '51', '1', 1, '2025-06-16', '2025-06-16', '00001', '001', '001', '1001', 'テスト荷印1', 'C001', '得意先A', '掛売上', -10.00, 1200.00, -12000.00, 1000.00),
('S0002', '51', '1', 1, '2025-06-16', '2025-06-16', '00002', '001', '001', '1002', 'テスト荷印2', 'C002', '得意先B', '掛売上', -15.00, 1800.00, -27000.00, 1500.00),

-- 売上（在庫0エラー対象）
('S0003', '51', '1', 1, '2025-06-16', '2025-06-16', '00008', '001', '001', '1008', 'テスト荷印8', 'C003', '得意先C', '掛売上', -5.00, 1300.00, -6500.00, 1100.00),

-- 売上（該当無エラー対象）
('S0004', '51', '1', 1, '2025-06-16', '2025-06-16', '99999', '999', '999', '9999', '存在しない荷印', 'C004', '得意先D', '掛売上', -8.00, 1000.00, -8000.00, 0.00),

-- 現金売上
('S0005', '52', '1', 1, '2025-06-16', '2025-06-16', '00003', '002', '001', '1003', 'テスト荷印3', 'C005', '得意先E', '現金売上', -20.00, 950.00, -19000.00, 800.00);

-- ===================================================================
-- 3. 仕入伝票 テストデータ
-- ===================================================================
INSERT INTO PurchaseVoucher (
    VoucherNumber, VoucherType, DetailType, LineNumber, VoucherDate, JobDate,
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    SupplierCode, SupplierName, TransactionType,
    Quantity, UnitPrice, Amount
) VALUES
-- 通常仕入
('P0001', '11', '1', 1, '2025-06-16', '2025-06-16', '00001', '001', '001', '1001', 'テスト荷印1', 'S001', '仕入先A', '掛買', 50.00, 950.00, 47500.00),
('P0002', '11', '1', 1, '2025-06-16', '2025-06-16', '00002', '001', '001', '1002', 'テスト荷印2', 'S002', '仕入先B', '掛買', 30.00, 1400.00, 42000.00),

-- 仕入（該当無エラー対象）
('P0003', '11', '1', 1, '2025-06-16', '2025-06-16', '88888', '888', '888', '8888', '存在しない商品', 'S003', '仕入先C', '掛買', 25.00, 1200.00, 30000.00),

-- 現金仕入
('P0004', '12', '1', 1, '2025-06-16', '2025-06-16', '00003', '002', '001', '1003', 'テスト荷印3', 'S004', '仕入先D', '現金買', 40.00, 750.00, 30000.00);

-- ===================================================================
-- 4. 在庫調整 テストデータ
-- ===================================================================
INSERT INTO InventoryAdjustment (
    VoucherNumber, VoucherType, DetailType, LineNumber, VoucherDate, JobDate,
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    Quantity, UnitPrice, Amount, UnitCode, ReasonCode
) VALUES
-- 在庫調整（増加）
('A0001', '71', '1', 1, '2025-06-16', '2025-06-16', '00001', '001', '001', '1001', 'テスト荷印1', 5.00, 1000.00, 5000.00, '01', 'R01'),

-- 在庫調整（減少・在庫0エラー対象）
('A0002', '71', '3', 1, '2025-06-16', '2025-06-16', '00008', '001', '001', '1008', 'テスト荷印8', -2.00, 1100.00, -2200.00, '01', 'R02'),

-- 加工費（除外対象）
('A0003', '71', '4', 1, '2025-06-16', '2025-06-16', '00002', '001', '001', '1002', 'テスト荷印2', 0.00, 0.00, 500.00, '02', 'R03');

PRINT 'テストデータの投入が完了しました。';
PRINT '投入データ数:';
PRINT '- 在庫マスタ: ' + CAST((SELECT COUNT(*) FROM InventoryMaster) AS NVARCHAR(10)) + '件';
PRINT '- 売上伝票: ' + CAST((SELECT COUNT(*) FROM SalesVoucher) AS NVARCHAR(10)) + '件';
PRINT '- 仕入伝票: ' + CAST((SELECT COUNT(*) FROM PurchaseVoucher) AS NVARCHAR(10)) + '件';
PRINT '- 在庫調整: ' + CAST((SELECT COUNT(*) FROM InventoryAdjustment) AS NVARCHAR(10)) + '件';