-- ===================================================
-- 在庫管理システム テストデータ投入スクリプト
-- ===================================================

USE InventoryManagementDB;
GO

-- ===================================================
-- 1. InventoryMaster テストデータ
-- ===================================================
PRINT '=== InventoryMaster テストデータ投入開始 ===';

-- 既存データ削除（テスト用）
DELETE FROM InventoryMaster WHERE ProductCode LIKE '0000%';

-- テスト用在庫マスタデータ
INSERT INTO InventoryMaster (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
    ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
    JobDate, CurrentStock, CurrentStockAmount
) VALUES
-- 商品分類1='1'のデータ
('00001', '001', '001', '1001', 'テスト荷印A1', 'りんご（青森県産）', 'KG', 1000.00, '1', '01', '2025-06-19', 100.00, 100000.00),
('00002', '002', '001', '1001', 'テスト荷印A1', 'りんご（青森県産・特級）', 'KG', 1200.00, '1', '01', '2025-06-19', 80.00, 96000.00),
('00003', '001', '002', '1001', 'テスト荷印A1', 'りんご（青森県産・大玉）', 'KG', 1100.00, '1', '01', '2025-06-19', 60.00, 66000.00),

-- 商品分類1='2'のデータ
('00011', '001', '001', '2001', 'テスト荷印B1', 'みかん（愛媛県産）', 'KG', 800.00, '2', '02', '2025-06-19', 200.00, 160000.00),
('00012', '002', '001', '2001', 'テスト荷印B1', 'みかん（愛媛県産・特級）', 'KG', 900.00, '2', '02', '2025-06-19', 150.00, 135000.00),

-- 商品分類1='3'のデータ
('00021', '001', '001', '3001', 'テスト荷印C1', 'バナナ（フィリピン産）', 'KG', 600.00, '3', '03', '2025-06-19', 300.00, 180000.00),
('00022', '001', '002', '3001', 'テスト荷印C1', 'バナナ（フィリピン産・大房）', 'KG', 650.00, '3', '03', '2025-06-19', 250.00, 162500.00),

-- 除外対象データ（アンマッチテスト用）
('99001', '001', '001', '9900', 'EXIT_TEST1', '除外テスト商品1', 'KG', 100.00, '9', '99', '2025-06-19', 10.00, 1000.00),
('99002', '001', '001', '9910', 'EXIT_TEST2', '除外テスト商品2', 'KG', 100.00, '9', '99', '2025-06-19', 10.00, 1000.00),
('99003', '001', '001', '1353', 'EXIT_TEST3', '除外テスト商品3', 'KG', 100.00, '9', '99', '2025-06-19', 10.00, 1000.00),

-- 特殊分類変更データ
('90001', '001', '001', '4001', '9aaa_特殊商品1', '特殊商品（9aaa）', 'KG', 1500.00, '7', '04', '2025-06-19', 50.00, 75000.00),
('10001', '001', '001', '5001', '1aaa_特殊商品2', '特殊商品（1aaa）', 'KG', 1300.00, '5', '05', '2025-06-19', 40.00, 52000.00),
('00991', '001', '001', '6001', '0999_特殊商品3', '特殊商品（0999）', 'KG', 1400.00, '4', '06', '2025-06-19', 30.00, 42000.00);

PRINT 'InventoryMaster テストデータを投入しました（13件）';

-- ===================================================
-- 2. SalesVouchers テストデータ
-- ===================================================
PRINT '=== SalesVouchers テストデータ投入開始 ===';

-- 既存データ削除（テスト用）
DELETE FROM SalesVouchers WHERE VoucherId LIKE 'TEST%';

-- テスト用売上伝票データ
INSERT INTO SalesVouchers (
    VoucherId, LineNumber, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
    VoucherType, DetailType, VoucherNumber, VoucherDate, JobDate,
    CustomerCode, CustomerName, Quantity, UnitPrice, Amount, InventoryUnitPrice
) VALUES
-- 2025-06-19の売上データ
('TEST_SALES_001', 1, '00001', '001', '001', '1001', 'テスト荷印A1', '11', '1', 'S20250619001', '2025-06-19', '2025-06-19', 'C001', 'テスト得意先A', 20.00, 1100.00, 22000.00, 1000.00),
('TEST_SALES_001', 2, '00002', '002', '001', '1001', 'テスト荷印A1', '11', '1', 'S20250619001', '2025-06-19', '2025-06-19', 'C001', 'テスト得意先A', 15.00, 1300.00, 19500.00, 1200.00),
('TEST_SALES_002', 1, '00011', '001', '001', '2001', 'テスト荷印B1', '11', '1', 'S20250619002', '2025-06-19', '2025-06-19', 'C002', 'テスト得意先B', 50.00, 900.00, 45000.00, 800.00),
('TEST_SALES_003', 1, '00021', '001', '001', '3001', 'テスト荷印C1', '11', '1', 'S20250619003', '2025-06-19', '2025-06-19', 'C003', 'テスト得意先C', 100.00, 700.00, 70000.00, 600.00),

-- 売上返品データ
('TEST_SALES_004', 1, '00001', '001', '001', '1001', 'テスト荷印A1', '12', '1', 'SR20250619001', '2025-06-19', '2025-06-19', 'C001', 'テスト得意先A', 5.00, 1100.00, 5500.00, 1000.00);

PRINT 'SalesVouchers テストデータを投入しました（5件）';

-- ===================================================
-- 3. PurchaseVouchers テストデータ
-- ===================================================
PRINT '=== PurchaseVouchers テストデータ投入開始 ===';

-- 既存データ削除（テスト用）
DELETE FROM PurchaseVouchers WHERE VoucherId LIKE 'TEST%';

-- テスト用仕入伝票データ
INSERT INTO PurchaseVouchers (
    VoucherId, LineNumber, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
    VoucherType, DetailType, VoucherNumber, VoucherDate, JobDate,
    SupplierCode, SupplierName, Quantity, UnitPrice, Amount
) VALUES
-- 2025-06-19の仕入データ
('TEST_PURCHASE_001', 1, '00001', '001', '001', '1001', 'テスト荷印A1', '21', '1', 'P20250619001', '2025-06-19', '2025-06-19', 'S001', 'テスト仕入先A', 150.00, 950.00, 142500.00),
('TEST_PURCHASE_001', 2, '00002', '002', '001', '1001', 'テスト荷印A1', '21', '1', 'P20250619001', '2025-06-19', '2025-06-19', 'S001', 'テスト仕入先A', 100.00, 1150.00, 115000.00),
('TEST_PURCHASE_002', 1, '00011', '001', '001', '2001', 'テスト荷印B1', '21', '1', 'P20250619002', '2025-06-19', '2025-06-19', 'S002', 'テスト仕入先B', 200.00, 750.00, 150000.00),

-- 仕入返品データ
('TEST_PURCHASE_003', 1, '00021', '001', '001', '3001', 'テスト荷印C1', '22', '1', 'PR20250619001', '2025-06-19', '2025-06-19', 'S003', 'テスト仕入先C', 10.00, 550.00, 5500.00);

PRINT 'PurchaseVouchers テストデータを投入しました（4件）';

-- ===================================================
-- 4. InventoryAdjustments テストデータ
-- ===================================================
PRINT '=== InventoryAdjustments テストデータ投入開始 ===';

-- 既存データ削除（テスト用）
DELETE FROM InventoryAdjustments WHERE VoucherId LIKE 'TEST%';

-- テスト用在庫調整データ
INSERT INTO InventoryAdjustments (
    VoucherId, LineNumber, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
    VoucherType, DetailType, VoucherNumber, VoucherDate, JobDate,
    CustomerCode, CustomerName, CategoryCode, Quantity, UnitPrice, Amount
) VALUES
-- 在庫調整データ（カテゴリ1:ロス）
('TEST_ADJ_001', 1, '00001', '001', '001', '1001', 'テスト荷印A1', '71', '1', 'A20250619001', '2025-06-19', '2025-06-19', NULL, NULL, 1, 5.00, 1000.00, 5000.00),

-- 在庫調整データ（カテゴリ4:振替）
('TEST_ADJ_002', 1, '00011', '001', '001', '2001', 'テスト荷印B1', '71', '1', 'A20250619002', '2025-06-19', '2025-06-19', NULL, NULL, 4, 10.00, 800.00, 8000.00),

-- 在庫調整データ（カテゴリ6:調整）
('TEST_ADJ_003', 1, '00021', '001', '001', '3001', 'テスト荷印C1', '72', '1', 'A20250619003', '2025-06-19', '2025-06-19', NULL, NULL, 6, 15.00, 600.00, 9000.00),

-- 除外対象データ（カテゴリ2:経費、カテゴリ5:加工）
('TEST_ADJ_004', 1, '00001', '001', '001', '1001', 'テスト荷印A1', '71', '1', 'A20250619004', '2025-06-19', '2025-06-19', NULL, NULL, 2, 3.00, 1000.00, 3000.00),
('TEST_ADJ_005', 1, '00011', '001', '001', '2001', 'テスト荷印B1', '72', '1', 'A20250619005', '2025-06-19', '2025-06-19', NULL, NULL, 5, 8.00, 800.00, 6400.00);

PRINT 'InventoryAdjustments テストデータを投入しました（5件）';

-- ===================================================
-- 5. DataSets テストデータ
-- ===================================================
PRINT '=== DataSets テストデータ投入開始 ===';

-- 既存データ削除（テスト用）
DELETE FROM DataSets WHERE Id LIKE 'TEST%';

-- テスト用データセット
INSERT INTO DataSets (
    Id, Name, Description, ProcessType, Status, JobDate
) VALUES
('TEST_DATASET_001', 'テスト用データセット1', '在庫管理システムのテスト用データセット', 'UnmatchList', 'Completed', '2025-06-19'),
('TEST_DATASET_002', 'テスト用データセット2', '商品日報テスト用データセット', 'DailyReport', 'Processing', '2025-06-19'),
('TEST_DATASET_003', 'テスト用データセット3', '在庫表テスト用データセット', 'InventoryList', 'Created', '2025-06-19');

PRINT 'DataSets テストデータを投入しました（3件）';

-- ===================================================
-- データ投入結果確認
-- ===================================================
PRINT '=== データ投入結果確認 ===';

SELECT 'InventoryMaster' AS TableName, COUNT(*) AS RecordCount FROM InventoryMaster
UNION ALL
SELECT 'SalesVouchers' AS TableName, COUNT(*) AS RecordCount FROM SalesVouchers
UNION ALL
SELECT 'PurchaseVouchers' AS TableName, COUNT(*) AS RecordCount FROM PurchaseVouchers
UNION ALL
SELECT 'InventoryAdjustments' AS TableName, COUNT(*) AS RecordCount FROM InventoryAdjustments
UNION ALL
SELECT 'DataSets' AS TableName, COUNT(*) AS RecordCount FROM DataSets;

PRINT '=== テストデータ投入完了 ===';