-- =============================================
-- 商品勘定帳票データ生成ストアドプロシージャ
-- 最終修正版：実際のデータ構造に完全準拠
-- =============================================

CREATE OR ALTER PROCEDURE sp_CreateProductLedgerData
    @JobDate DATE,
    @DepartmentCode NVARCHAR(15) = NULL  -- 部門コード（担当者）フィルタ、NULLの場合は全部門
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ErrorMessage NVARCHAR(2000);
    DECLARE @ErrorSeverity INT;
    DECLARE @ErrorState INT;

    BEGIN TRY
        -- 既存の一時テーブルがあれば削除
        IF OBJECT_ID('tempdb..#CPInventoryMaster') IS NOT NULL
            DROP TABLE #CPInventoryMaster;

        -- =============================================
        -- Phase 1: CPInventoryMaster一時テーブル作成とデータ投入
        -- =============================================
        
        -- 一時テーブル作成
        CREATE TABLE #CPInventoryMaster (
            ProductCode NVARCHAR(15) NOT NULL,
            GradeCode NVARCHAR(15) NOT NULL,
            ClassCode NVARCHAR(15) NOT NULL,
            ShippingMarkCode NVARCHAR(15) NOT NULL,
            ShippingMarkName NVARCHAR(50) NOT NULL,
            ProductName NVARCHAR(100) NOT NULL,
            ProductCategory1 NVARCHAR(10) NOT NULL,  -- 担当者コード
            
            -- 前日残高
            PreviousDayStock DECIMAL(18,4) NOT NULL DEFAULT 0,
            PreviousDayStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
            PreviousDayUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
            
            -- 当日在庫
            CurrentStock DECIMAL(18,4) NOT NULL DEFAULT 0,
            CurrentStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
            CurrentUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
            
            -- グループキー（FastReport用）
            GroupKey NVARCHAR(50) NOT NULL,  -- 100から50に縮小
            SortKeyPart1 NVARCHAR(30) NOT NULL,  -- 基本キー部分
            SortKeyPart2 INT NOT NULL,            -- 日付部分（YYYYMMDD形式の数値）
            SortKeyPart3 NVARCHAR(20) NOT NULL,   -- 伝票番号部分
            
            PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName)
        );
        
        -- CpInventoryMasterからデータを取得
        INSERT INTO #CPInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ProductName, ProductCategory1,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
            CurrentStock, CurrentStockAmount, CurrentUnitPrice,
            GroupKey, SortKeyPart1, SortKeyPart2, SortKeyPart3
        )
        SELECT 
            cp.ProductCode,
            cp.GradeCode,
            cp.ClassCode,
            cp.ShippingMarkCode,
            cp.ShippingMarkName,
            cp.ProductName,
            cp.ProductCategory1,
            cp.PreviousDayStock,
            cp.PreviousDayStockAmount,
            cp.PreviousDayUnitPrice,
            cp.DailyStock as CurrentStock,
            cp.DailyStockAmount as CurrentStockAmount,
            cp.DailyUnitPrice as CurrentUnitPrice,
            -- グループキー生成（簡略化）
            cp.ProductCode + '_' + cp.ShippingMarkCode as GroupKey,
            -- ソートキー生成（3分割）
            ISNULL(cp.ProductCategory1, '000') + '_' + cp.ProductCode as SortKeyPart1,
            0 as SortKeyPart2,  -- 前残は0
            '00_前残' as SortKeyPart3  -- 固定値
        FROM CpInventoryMaster cp
        WHERE cp.JobDate = @JobDate
          AND (@DepartmentCode IS NULL OR cp.ProductCategory1 = @DepartmentCode);

        -- =============================================
        -- Phase 2: 取引明細データの取得と計算
        -- =============================================
        
        -- 最終結果セット（商品勘定明細データ）
        WITH TransactionData AS (
            -- 前残高レコード
            SELECT
                cp.ProductCode,
                cp.ProductName,
                cp.ShippingMarkCode,
                cp.ShippingMarkName,
                cp.ShippingMarkName as ManualShippingMark,
                cp.GradeCode,
                ISNULL(gm.GradeName, '') as GradeName,
                cp.ClassCode,
                ISNULL(cm.ClassName, '') as ClassName,
                '' as VoucherNumber,
                'Previous' as RecordType,
                '' as VoucherCategory,
                '前残' as DisplayCategory,
                @JobDate as TransactionDate,
                0.00 as PurchaseQuantity,
                0.00 as SalesQuantity,
                cp.PreviousDayStock as RemainingQuantity,
                cp.PreviousDayUnitPrice as UnitPrice,
                cp.PreviousDayStockAmount as Amount,
                0.00 as GrossProfit,
                0.00 as WalkingDiscount,
                '' as CustomerSupplierName,
                cp.ProductCategory1,
                '' as ProductCategory5,
                cp.GroupKey,
                cp.SortKeyPart1 as SortKeyPart1,
                cp.SortKeyPart2 as SortKeyPart2,
                cp.SortKeyPart3 as SortKeyPart3,
                1 as SortOrder
            FROM #CPInventoryMaster cp
            LEFT JOIN GradeMaster gm ON cp.GradeCode = gm.GradeCode
            LEFT JOIN ClassMaster cm ON cp.ClassCode = cm.ClassCode
            WHERE cp.PreviousDayStock <> 0 OR cp.PreviousDayStockAmount <> 0
            
            UNION ALL
            
            -- 売上伝票データ
            SELECT
                s.ProductCode,
                cp.ProductName,  -- CpInventoryMasterから取得
                s.ShippingMarkCode,
                s.ShippingMarkName,
                RIGHT('        ' + ISNULL(s.ShippingMarkName, ''), 8) as ManualShippingMark,
                s.GradeCode,
                ISNULL(gm.GradeName, '') as GradeName,
                s.ClassCode,
                ISNULL(cm.ClassName, '') as ClassName,
                s.VoucherNumber,
                'Sales' as RecordType,
                s.VoucherType as VoucherCategory,
                CASE s.VoucherType
                    WHEN '51' THEN '掛売'
                    WHEN '52' THEN '現売'
                    ELSE s.VoucherType
                END as DisplayCategory,
                s.VoucherDate as TransactionDate,
                0.00 as PurchaseQuantity,
                s.Quantity as SalesQuantity,
                0.00 as RemainingQuantity,
                s.UnitPrice,
                s.Amount,
                -- 粗利計算（売上金額 - 在庫単価×数量）
                s.Amount - (s.Quantity * ISNULL(s.InventoryUnitPrice, cp.CurrentUnitPrice)) as GrossProfit,
                0.00 as WalkingDiscount,
                ISNULL(s.CustomerName, '') as CustomerSupplierName,
                cp.ProductCategory1,
                '' as ProductCategory5,
                cp.GroupKey,
                cp.SortKeyPart1 as SortKeyPart1,
                CAST(FORMAT(s.VoucherDate, 'yyyyMMdd') as INT) as SortKeyPart2,
                s.VoucherNumber as SortKeyPart3,
                2 as SortOrder
            FROM SalesVouchers s
            INNER JOIN #CPInventoryMaster cp ON 
                s.ProductCode = cp.ProductCode AND
                s.GradeCode = cp.GradeCode AND
                s.ClassCode = cp.ClassCode AND
                s.ShippingMarkCode = cp.ShippingMarkCode AND
                s.ShippingMarkName = cp.ShippingMarkName
            LEFT JOIN GradeMaster gm ON s.GradeCode = gm.GradeCode
            LEFT JOIN ClassMaster cm ON s.ClassCode = cm.ClassCode
            WHERE s.JobDate = @JobDate
              AND (@DepartmentCode IS NULL OR cp.ProductCategory1 = @DepartmentCode)
            
            UNION ALL
            
            -- 仕入伝票データ
            SELECT
                p.ProductCode,
                cp.ProductName,
                p.ShippingMarkCode,
                p.ShippingMarkName,
                RIGHT('        ' + ISNULL(p.ShippingMarkName, ''), 8) as ManualShippingMark,
                p.GradeCode,
                ISNULL(gm.GradeName, '') as GradeName,
                p.ClassCode,
                ISNULL(cm.ClassName, '') as ClassName,
                p.VoucherNumber,
                'Purchase' as RecordType,
                p.VoucherType as VoucherCategory,
                CASE p.VoucherType
                    WHEN '11' THEN '掛仕'
                    WHEN '12' THEN '現仕'
                    ELSE p.VoucherType
                END as DisplayCategory,
                p.VoucherDate as TransactionDate,
                p.Quantity as PurchaseQuantity,
                0.00 as SalesQuantity,
                0.00 as RemainingQuantity,
                p.UnitPrice,
                p.Amount,
                0.00 as GrossProfit,
                0.00 as WalkingDiscount,
                ISNULL(p.SupplierName, '') as CustomerSupplierName,
                cp.ProductCategory1,
                '' as ProductCategory5,
                cp.GroupKey,
                cp.SortKeyPart1 as SortKeyPart1,
                CAST(FORMAT(p.VoucherDate, 'yyyyMMdd') as INT) as SortKeyPart2,
                p.VoucherNumber as SortKeyPart3,
                2 as SortOrder
            FROM PurchaseVouchers p
            INNER JOIN #CPInventoryMaster cp ON 
                p.ProductCode = cp.ProductCode AND
                p.GradeCode = cp.GradeCode AND
                p.ClassCode = cp.ClassCode AND
                p.ShippingMarkCode = cp.ShippingMarkCode AND
                p.ShippingMarkName = cp.ShippingMarkName
            LEFT JOIN GradeMaster gm ON p.GradeCode = gm.GradeCode
            LEFT JOIN ClassMaster cm ON p.ClassCode = cm.ClassCode
            WHERE p.JobDate = @JobDate
              AND (@DepartmentCode IS NULL OR cp.ProductCategory1 = @DepartmentCode)
            
            UNION ALL
            
            -- 在庫調整データ
            SELECT
                a.ProductCode,
                cp.ProductName,
                a.ShippingMarkCode,
                a.ShippingMarkName,
                RIGHT('        ' + ISNULL(a.ShippingMarkName, ''), 8) as ManualShippingMark,
                a.GradeCode,
                ISNULL(gm.GradeName, '') as GradeName,
                a.ClassCode,
                ISNULL(cm.ClassName, '') as ClassName,
                a.VoucherNumber,
                CASE 
                    WHEN a.CategoryCode = 1 THEN 'Loss'
                    WHEN a.CategoryCode = 4 THEN 'Transfer'
                    WHEN a.CategoryCode = 6 THEN 'Adjustment'
                    ELSE 'Other'
                END as RecordType,
                '71' as VoucherCategory,
                CASE a.CategoryCode
                    WHEN 1 THEN 'ロス'
                    WHEN 4 THEN '振替'
                    WHEN 6 THEN '調整'
                    ELSE 'その他'
                END as DisplayCategory,
                a.VoucherDate as TransactionDate,
                CASE WHEN a.Quantity > 0 THEN a.Quantity ELSE 0.00 END as PurchaseQuantity,
                CASE WHEN a.Quantity < 0 THEN ABS(a.Quantity) ELSE 0.00 END as SalesQuantity,
                0.00 as RemainingQuantity,
                a.UnitPrice,
                a.Amount,
                0.00 as GrossProfit,
                0.00 as WalkingDiscount,
                '' as CustomerSupplierName,
                cp.ProductCategory1,
                '' as ProductCategory5,
                cp.GroupKey,
                cp.SortKeyPart1 as SortKeyPart1,
                CAST(FORMAT(a.VoucherDate, 'yyyyMMdd') as INT) as SortKeyPart2,
                a.VoucherNumber as SortKeyPart3,
                2 as SortOrder
            FROM InventoryAdjustments a
            INNER JOIN #CPInventoryMaster cp ON 
                a.ProductCode = cp.ProductCode AND
                a.GradeCode = cp.GradeCode AND
                a.ClassCode = cp.ClassCode AND
                a.ShippingMarkCode = cp.ShippingMarkCode AND
                a.ShippingMarkName = cp.ShippingMarkName
            LEFT JOIN GradeMaster gm ON a.GradeCode = gm.GradeCode
            LEFT JOIN ClassMaster cm ON a.ClassCode = cm.ClassCode
            WHERE a.JobDate = @JobDate
              AND (@DepartmentCode IS NULL OR cp.ProductCategory1 = @DepartmentCode)
        ),
        
        -- =============================================
        -- Phase 3: 移動平均単価と累積残高の計算
        -- =============================================
        RankedData AS (
            SELECT *,
                ROW_NUMBER() OVER (PARTITION BY GroupKey ORDER BY SortKeyPart1, SortKeyPart2, SortKeyPart3) as RowNum
            FROM TransactionData
        ),
        
        CalculatedData AS (
            SELECT *,
                -- 累積残高計算
                SUM(PurchaseQuantity - SalesQuantity) OVER (
                    PARTITION BY GroupKey 
                    ORDER BY SortKeyPart1, SortKeyPart2, SortKeyPart3 
                    ROWS UNBOUNDED PRECEDING
                ) as RunningQuantity,
                
                SUM(Amount * CASE WHEN PurchaseQuantity > 0 THEN 1 WHEN SalesQuantity > 0 THEN -1 ELSE 0 END) OVER (
                    PARTITION BY GroupKey 
                    ORDER BY SortKeyPart1, SortKeyPart2, SortKeyPart3 
                    ROWS UNBOUNDED PRECEDING
                ) as RunningAmount
                
            FROM RankedData
        )
        
        -- =============================================
        -- Final Result Set
        -- =============================================
        SELECT
            ProductCode,
            ProductName,
            ShippingMarkCode,
            ShippingMarkName,
            ManualShippingMark,
            GradeCode,
            GradeName,
            ClassCode,
            ClassName,
            VoucherNumber,
            VoucherCategory as VoucherType,
            DisplayCategory,
            TransactionDate,
            FORMAT(TransactionDate, 'MM/dd') as MonthDay,
            PurchaseQuantity,
            SalesQuantity,
            -- 残数量
            CASE 
                WHEN RecordType = 'Previous' THEN RemainingQuantity
                ELSE RunningQuantity 
            END as RemainingQuantity,
            UnitPrice,
            Amount,
            GrossProfit,
            WalkingDiscount,
            CustomerSupplierName,
            RecordType,  -- 重要：RecordTypeを追加（IndexOutOfRangeException対策）
            GroupKey,
            ProductCategory1,
            ProductCategory5,
            SortKeyPart1,
            SortKeyPart2, 
            SortKeyPart3,
            
            -- 集計用フィールド
            FIRST_VALUE(RemainingQuantity) OVER (PARTITION BY GroupKey ORDER BY SortKeyPart1, SortKeyPart2, SortKeyPart3) as PreviousBalance,
            SUM(PurchaseQuantity) OVER (PARTITION BY GroupKey) as TotalPurchaseQuantity,
            SUM(SalesQuantity) OVER (PARTITION BY GroupKey) as TotalSalesQuantity,
            LAST_VALUE(RunningQuantity) OVER (PARTITION BY GroupKey ORDER BY SortKeyPart1, SortKeyPart2, SortKeyPart3 ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) as CurrentBalance,
            -- 在庫単価
            LAST_VALUE(CASE WHEN RecordType <> 'Previous' AND RunningQuantity > 0 THEN RunningAmount / NULLIF(RunningQuantity, 0) ELSE 0 END) 
                OVER (PARTITION BY GroupKey ORDER BY SortKeyPart1, SortKeyPart2, SortKeyPart3 ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) as InventoryUnitPrice,
            -- 在庫金額
            LAST_VALUE(RunningAmount) OVER (PARTITION BY GroupKey ORDER BY SortKeyPart1, SortKeyPart2, SortKeyPart3 ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) as InventoryAmount,
            -- 粗利益合計
            SUM(GrossProfit) OVER (PARTITION BY GroupKey) as TotalGrossProfit,
            -- 粗利率
            CASE 
                WHEN SUM(Amount * CASE WHEN SalesQuantity > 0 THEN 1 ELSE 0 END) OVER (PARTITION BY GroupKey) > 0 
                THEN (SUM(GrossProfit) OVER (PARTITION BY GroupKey) / SUM(Amount * CASE WHEN SalesQuantity > 0 THEN 1 ELSE 0 END) OVER (PARTITION BY GroupKey)) * 100
                ELSE 0 
            END as GrossProfitRate
            
        FROM CalculatedData
        ORDER BY ProductCategory1, GroupKey, SortKeyPart1, SortKeyPart2, SortKeyPart3;
        
        -- 一時テーブルの削除
        DROP TABLE #CPInventoryMaster;

    END TRY
    BEGIN CATCH
        -- エラー処理
        IF OBJECT_ID('tempdb..#CPInventoryMaster') IS NOT NULL
            DROP TABLE #CPInventoryMaster;
            
        SELECT @ErrorMessage = ERROR_MESSAGE(),
               @ErrorSeverity = ERROR_SEVERITY(),
               @ErrorState = ERROR_STATE();

        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO