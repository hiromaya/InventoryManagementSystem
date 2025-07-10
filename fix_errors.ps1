# 在庫管理システムエラー修正スクリプト
# 実行日: 2025-07-10

Write-Host "=== 在庫管理システム エラー修正スクリプト ===" -ForegroundColor Cyan
Write-Host ""

# 1. データベース初期化（PreviousMonthInventoryテーブルを作成）
Write-Host "1. データベースを初期化します（PreviousMonthInventoryテーブルを含む）" -ForegroundColor Yellow
Set-Location -Path "C:\Development\InventoryManagementSystem\src\InventorySystem.Console"
dotnet run init-database --force

Write-Host ""
Write-Host "2. ストアドプロシージャを作成します" -ForegroundColor Yellow

# 2. ストアドプロシージャの作成
$sqlCmd = @"
-- 累積管理対応版ストアドプロシージャの作成
USE InventoryDB;
GO

-- 既存のストアドプロシージャを削除
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative')
    DROP PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative;
GO

CREATE PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative
    @DataSetId NVARCHAR(50),
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- CP在庫マスタに在庫マスタのデータを挿入
        -- 当日の伝票に含まれる5項目キーのレコードのみ抽出
        INSERT INTO CpInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ProductName, ProductCategory1, ProductCategory2, Unit, StandardPrice,
            JobDate, DataSetId,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice, DailyFlag,
            DailySalesQuantity, DailySalesAmount, DailySalesReturnQuantity, DailySalesReturnAmount,
            DailyPurchaseQuantity, DailyPurchaseAmount, DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            DailyProcessingQuantity, DailyProcessingAmount,
            DailyTransferQuantity, DailyTransferAmount,
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount,
            DailyStock, DailyStockAmount, DailyUnitPrice,
            MonthlySalesQuantity, MonthlySalesAmount, MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount, MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            CreatedDate, UpdatedDate
        )
        SELECT 
            im.ProductCode, 
            im.GradeCode, 
            im.ClassCode, 
            im.ShippingMarkCode, 
            im.ShippingMarkName,
            im.ProductName, 
            -- 特殊処理ルール: 荷印名による商品分類1の変更
            CASE 
                WHEN LEFT(im.ShippingMarkName, 4) = '9aaa' THEN '8'
                WHEN LEFT(im.ShippingMarkName, 4) = '1aaa' THEN '6'
                WHEN LEFT(im.ShippingMarkName, 4) = '0999' THEN '6'
                ELSE ISNULL(pm.ProductCategory1, '00')
            END AS ProductCategory1,
            ISNULL(pm.ProductCategory2, '00') AS ProductCategory2,
            im.Unit, 
            im.StandardPrice,
            @JobDate, -- 処理対象日
            @DataSetId,
            -- 在庫マスタの現在在庫を前日在庫として設定
            im.CurrentStock AS PreviousDayStock, 
            im.CurrentStockAmount AS PreviousDayStockAmount, 
            CASE 
                WHEN im.CurrentStock = 0 THEN 0 
                ELSE im.CurrentStockAmount / im.CurrentStock 
            END AS PreviousDayUnitPrice,
            '9' AS DailyFlag,
            0, 0, 0, 0,  -- Sales
            0, 0, 0, 0,  -- Purchase
            0, 0,        -- Adjustment
            0, 0,        -- Processing
            0, 0,        -- Transfer
            0, 0,        -- Receipt/Shipment
            0, 0, 0, 0,  -- Profit/Walking/Incentive/Discount
            -- 初期値として前日在庫と同じ値を設定
            im.CurrentStock, im.CurrentStockAmount, 
            CASE 
                WHEN im.CurrentStock = 0 THEN 0 
                ELSE im.CurrentStockAmount / im.CurrentStock 
            END,
            0, 0, 0, 0,  -- Monthly Sales
            0, 0, 0, 0,  -- Monthly Purchase
            0, 0,        -- Monthly Adjustment
            0, 0,        -- Monthly Processing
            0, 0,        -- Monthly Transfer
            0, 0, 0,     -- Monthly Profit
            GETDATE(), 
            GETDATE()
        FROM InventoryMaster im
        LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode
        WHERE 
            -- 当日の伝票に含まれる5項目キーのみ抽出
            EXISTS (
                SELECT 1 FROM SalesVouchers sv
                WHERE sv.JobDate = @JobDate
                    AND sv.ProductCode = im.ProductCode
                    AND sv.GradeCode = im.GradeCode
                    AND sv.ClassCode = im.ClassCode
                    AND sv.ShippingMarkCode = im.ShippingMarkCode
                    AND sv.ShippingMarkName COLLATE Japanese_CI_AS = im.ShippingMarkName COLLATE Japanese_CI_AS
            )
            OR EXISTS (
                SELECT 1 FROM PurchaseVouchers pv
                WHERE pv.JobDate = @JobDate
                    AND pv.ProductCode = im.ProductCode
                    AND pv.GradeCode = im.GradeCode
                    AND pv.ClassCode = im.ClassCode
                    AND pv.ShippingMarkCode = im.ShippingMarkCode
                    AND pv.ShippingMarkName COLLATE Japanese_CI_AS = im.ShippingMarkName COLLATE Japanese_CI_AS
            )
            OR EXISTS (
                SELECT 1 FROM InventoryAdjustments ia
                WHERE ia.JobDate = @JobDate
                    AND ia.ProductCode = im.ProductCode
                    AND ia.GradeCode = im.GradeCode
                    AND ia.ClassCode = im.ClassCode
                    AND ia.ShippingMarkCode = im.ShippingMarkCode
                    AND ia.ShippingMarkName COLLATE Japanese_CI_AS = im.ShippingMarkName COLLATE Japanese_CI_AS
            );
        
        -- 作成件数を返す
        SELECT @@ROWCOUNT AS CreatedCount;
        
        COMMIT TRANSACTION;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

PRINT 'ストアドプロシージャ sp_CreateCpInventoryFromInventoryMasterCumulative を作成しました。';
GO
"@

# SQL実行
$sqlCmd | sqlcmd -S localhost -d InventoryDB -E

Write-Host ""
Write-Host "3. 5項目キー複合インデックスを作成します" -ForegroundColor Yellow

# 3. インデックス作成
$indexSql = Get-Content "C:\Development\InventoryManagementSystem\database\indexes\create_inventory_composite_index.sql" -Raw
$indexSql | sqlcmd -S localhost -d InventoryDB -E

Write-Host ""
Write-Host "=== 修正が完了しました ===" -ForegroundColor Green
Write-Host ""
Write-Host "以下のコマンドでアンマッチリスト処理を再実行してください:" -ForegroundColor Cyan
Write-Host "dotnet run unmatch-list 2025-06-01" -ForegroundColor White