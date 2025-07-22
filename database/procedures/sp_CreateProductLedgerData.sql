-- =============================================
-- 商品勘定帳票データ生成ストアドプロシージャ
-- Gemini CLI戦略: データ準備とレポート描画の役割分離
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
        -- =============================================
        -- Phase 1: CPInventoryMaster一時テーブル作成とデータ投入
        -- =============================================
        
        -- 一時テーブル作成（仕様書準拠）
        CREATE TABLE #CPInventoryMaster (
            ProductCode NVARCHAR(15) NOT NULL,
            GradeCode NVARCHAR(15) NOT NULL,
            ClassCode NVARCHAR(15) NOT NULL,
            ShippingMarkCode NVARCHAR(15) NOT NULL,
            ShippingMarkName NVARCHAR(50) NOT NULL,
            ProductName NVARCHAR(100) NOT NULL,
            ProductCategory1 NVARCHAR(10) NOT NULL,  -- 担当者コード
            ProductCategory5 NVARCHAR(15),            -- 商品分類5（例外処理用）
            
            -- 前日残高
            PreviousDayStock DECIMAL(18,4) NOT NULL DEFAULT 0,
            PreviousDayStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
            PreviousDayUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
            
            -- 当日在庫
            CurrentStock DECIMAL(18,4) NOT NULL DEFAULT 0,
            CurrentStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
            CurrentUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
            
            -- グループキー（FastReport用）
            GroupKey NVARCHAR(100) NOT NULL,
            SortKey NVARCHAR(150) NOT NULL,
            
            PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName)
        );
        
        -- CpInventoryMasterからデータを取得
        INSERT INTO #CPInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ProductName, ProductCategory1, ProductCategory5,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
            CurrentStock, CurrentStockAmount, CurrentUnitPrice,
            GroupKey, SortKey
        )
        SELECT 
            cp.ProductCode,
            cp.GradeCode,
            cp.ClassCode,
            cp.ShippingMarkCode,
            cp.ShippingMarkName,
            cp.ProductName,
            cp.ProductCategory1,
            ISNULL(pm.ProductCategory5, '') as ProductCategory5,
            cp.PreviousDayStock,
            cp.PreviousDayStockAmount,
            -- 0除算対策: 前日在庫単価計算
            CASE 
                WHEN cp.PreviousDayStock > 0 THEN cp.PreviousDayStockAmount / cp.PreviousDayStock
                ELSE 0 
            END as PreviousDayUnitPrice,
            cp.DailyStock as CurrentStock,
            cp.DailyStockAmount as CurrentStockAmount,
            -- 0除算対策: 当日在庫単価計算
            CASE 
                WHEN cp.DailyStock > 0 THEN cp.DailyStockAmount / cp.DailyStock
                ELSE 0 
            END as CurrentUnitPrice,
            -- グループキー生成
            cp.ProductCode + '_' + cp.ShippingMarkCode + '_' + cp.GradeCode + '_' + cp.ClassCode as GroupKey,
            -- ソートキー生成
            ISNULL(cp.ProductCategory1, '000') + '_' + cp.ProductCode + '_' + cp.ShippingMarkCode + '_' + cp.GradeCode + '_' + cp.ClassCode as SortKey
        FROM CpInventoryMaster cp
        LEFT JOIN ProductMaster pm ON cp.ProductCode = pm.ProductCode
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
                cp.ShippingMarkName as ManualShippingMark,  -- 8文字固定
                cp.GradeCode,
                '' as GradeName,
                cp.ClassCode,
                '' as ClassName,
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
                0.00 as GrossProfit,  -- 前残高は粗利なし
                0.00 as WalkingDiscount,  -- 前残高は歩引きなし
                '' as CustomerSupplierName,
                cp.ProductCategory1,
                cp.ProductCategory5,
                cp.GroupKey,
                cp.SortKey + '_0000_前残',
                1 as SortOrder
            FROM #CPInventoryMaster cp
            WHERE cp.PreviousDayStock <> 0 OR cp.PreviousDayStockAmount <> 0
            
            UNION ALL
            
            -- 売上伝票データ
            SELECT
                s.ProductCode,
                s.ProductName,
                s.ShippingMarkCode,
                s.ShippingMarkName,
                RIGHT('        ' + ISNULL(s.ShippingMarkName, ''), 8) as ManualShippingMark,
                s.GradeCode,
                gm.GradeName,
                s.ClassCode,
                cm.ClassName,
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
                0.00 as RemainingQuantity,  -- 明細では残数量は計算しない
                s.UnitPrice,
                s.Amount,
                -- 粗利計算（商品分類5=99999の例外処理含む）
                CASE 
                    WHEN ISNULL(s.ProductCategory5, '') = '99999' THEN 0.00
                    ELSE (s.Amount - (s.Quantity * s.InventoryUnitPrice))
                END as GrossProfit,
                -- 歩引き金計算（商品分類5=99999の例外処理含む）
                CASE 
                    WHEN ISNULL(s.ProductCategory5, '') = '99999' THEN 0.00
                    ELSE (s.Amount * ISNULL(cust.WalkingRate, 0) / 100)
                END as WalkingDiscount,
                ISNULL(s.CustomerName, '') as CustomerSupplierName,
                s.ProductCategory1,
                s.ProductCategory5,
                cp.GroupKey,
                cp.SortKey + '_' + FORMAT(s.VoucherDate, 'yyyyMMdd') + '_' + s.VoucherNumber as SortKey,
                2 as SortOrder
            FROM SalesVoucher s
            INNER JOIN #CPInventoryMaster cp ON 
                s.ProductCode = cp.ProductCode AND
                s.GradeCode = cp.GradeCode AND
                s.ClassCode = cp.ClassCode AND
                s.ShippingMarkCode = cp.ShippingMarkCode AND
                s.ShippingMarkName = cp.ShippingMarkName
            LEFT JOIN GradeMaster gm ON s.GradeCode = gm.GradeCode
            LEFT JOIN ClassMaster cm ON s.ClassCode = cm.ClassCode
            LEFT JOIN CustomerMaster cust ON s.CustomerCode = cust.CustomerCode
            WHERE s.JobDate = @JobDate
              AND s.IsExcluded = 0
              AND (@DepartmentCode IS NULL OR s.ProductCategory1 = @DepartmentCode)
            
            UNION ALL
            
            -- 仕入伝票データ
            SELECT
                p.ProductCode,
                p.ProductName,
                p.ShippingMarkCode,
                p.ShippingMarkName,
                RIGHT('        ' + ISNULL(p.ShippingMarkName, ''), 8) as ManualShippingMark,
                p.GradeCode,
                gm.GradeName,
                p.ClassCode,
                cm.ClassName,
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
                0.00 as GrossProfit,  -- 仕入は粗利なし
                0.00 as WalkingDiscount,  -- 仕入は歩引きなし
                ISNULL(p.SupplierName, '') as CustomerSupplierName,
                p.ProductCategory1,
                '' as ProductCategory5,  -- 仕入伝票には商品分類5なし
                cp.GroupKey,
                cp.SortKey + '_' + FORMAT(p.VoucherDate, 'yyyyMMdd') + '_' + p.VoucherNumber as SortKey,
                2 as SortOrder
            FROM PurchaseVoucher p
            INNER JOIN #CPInventoryMaster cp ON 
                p.ProductCode = cp.ProductCode AND
                p.GradeCode = cp.GradeCode AND
                p.ClassCode = cp.ClassCode AND
                p.ShippingMarkCode = cp.ShippingMarkCode AND
                p.ShippingMarkName = cp.ShippingMarkName
            LEFT JOIN GradeMaster gm ON p.GradeCode = gm.GradeCode
            LEFT JOIN ClassMaster cm ON p.ClassCode = cm.ClassCode
            WHERE p.JobDate = @JobDate
              AND (@DepartmentCode IS NULL OR p.ProductCategory1 = @DepartmentCode)
            
            UNION ALL
            
            -- 在庫調整データ
            SELECT
                a.ProductCode,
                a.ProductName,
                a.ShippingMarkCode,
                a.ShippingMarkName,
                RIGHT('        ' + ISNULL(a.ShippingMarkName, ''), 8) as ManualShippingMark,
                a.GradeCode,
                gm.GradeName,
                a.ClassCode,
                cm.ClassName,
                a.VoucherNumber,
                CASE a.AdjustmentCategory
                    WHEN '1' THEN 'Loss'
                    WHEN '4' THEN 'Transfer'
                    WHEN '6' THEN 'Adjustment'
                    ELSE 'Other'
                END as RecordType,
                '71' as VoucherCategory,
                CASE a.AdjustmentCategory
                    WHEN '1' THEN 'ロス'
                    WHEN '4' THEN '振替'
                    WHEN '6' THEN '調整'
                    ELSE 'その他'
                END as DisplayCategory,
                a.VoucherDate as TransactionDate,
                CASE WHEN a.Quantity > 0 THEN a.Quantity ELSE 0.00 END as PurchaseQuantity,
                CASE WHEN a.Quantity < 0 THEN ABS(a.Quantity) ELSE 0.00 END as SalesQuantity,
                0.00 as RemainingQuantity,
                a.UnitPrice,
                a.Amount,
                0.00 as GrossProfit,  -- 在庫調整は粗利なし
                0.00 as WalkingDiscount,  -- 在庫調整は歩引きなし
                '' as CustomerSupplierName,
                a.ProductCategory1,
                '' as ProductCategory5,  -- 在庫調整には商品分類5なし
                cp.GroupKey,
                cp.SortKey + '_' + FORMAT(a.VoucherDate, 'yyyyMMdd') + '_' + a.VoucherNumber as SortKey,
                2 as SortOrder
            FROM InventoryAdjustment a
            INNER JOIN #CPInventoryMaster cp ON 
                a.ProductCode = cp.ProductCode AND
                a.GradeCode = cp.GradeCode AND
                a.ClassCode = cp.ClassCode AND
                a.ShippingMarkCode = cp.ShippingMarkCode AND
                a.ShippingMarkName = cp.ShippingMarkName
            LEFT JOIN GradeMaster gm ON a.GradeCode = gm.GradeCode
            LEFT JOIN ClassMaster cm ON a.ClassCode = cm.ClassCode
            WHERE a.JobDate = @JobDate
              AND (@DepartmentCode IS NULL OR a.ProductCategory1 = @DepartmentCode)
        ),
        
        -- =============================================
        -- Phase 3: 移動平均単価と累積残高の計算
        -- =============================================
        RankedData AS (
            SELECT *,
                ROW_NUMBER() OVER (PARTITION BY GroupKey ORDER BY SortKey) as RowNum
            FROM TransactionData
        ),
        
        CalculatedData AS (
            SELECT *,
                -- 累積残高計算（ウィンドウ関数使用）
                SUM(PurchaseQuantity - SalesQuantity) OVER (
                    PARTITION BY GroupKey 
                    ORDER BY SortKey 
                    ROWS UNBOUNDED PRECEDING
                ) as RunningQuantity,
                
                SUM(Amount * CASE WHEN PurchaseQuantity > 0 THEN 1 WHEN SalesQuantity > 0 THEN -1 ELSE 0 END) OVER (
                    PARTITION BY GroupKey 
                    ORDER BY SortKey 
                    ROWS UNBOUNDED PRECEDING
                ) as RunningAmount
                
            FROM RankedData
        )
        
        -- =============================================
        -- Final Result Set (FastReport用)
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
            DisplayCategory,
            FORMAT(TransactionDate, 'MM/dd') as MonthDay,
            PurchaseQuantity,
            SalesQuantity,
            -- 残数量は累積計算結果を使用
            CASE 
                WHEN RecordType = 'Previous' THEN RemainingQuantity
                ELSE RunningQuantity 
            END as RemainingQuantity,
            UnitPrice,
            Amount,
            GrossProfit,
            WalkingDiscount,
            CustomerSupplierName,
            GroupKey,
            ProductCategory1,
            ProductCategory5,
            
            -- 集計用フィールド（グループフッター用）
            FIRST_VALUE(RemainingQuantity) OVER (PARTITION BY GroupKey ORDER BY SortKey) as PreviousBalance,
            SUM(PurchaseQuantity) OVER (PARTITION BY GroupKey) as TotalPurchaseQuantity,
            SUM(SalesQuantity) OVER (PARTITION BY GroupKey) as TotalSalesQuantity,
            LAST_VALUE(RunningQuantity) OVER (PARTITION BY GroupKey ORDER BY SortKey ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) as CurrentBalance,
            -- 在庫単価（当日の最終在庫単価）
            LAST_VALUE(CASE WHEN RecordType <> 'Previous' AND RunningQuantity > 0 THEN RunningAmount / NULLIF(RunningQuantity, 0) ELSE 0 END) 
                OVER (PARTITION BY GroupKey ORDER BY SortKey ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) as InventoryUnitPrice,
            -- 在庫金額
            LAST_VALUE(RunningAmount) OVER (PARTITION BY GroupKey ORDER BY SortKey ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) as InventoryAmount,
            -- 粗利益合計
            SUM(GrossProfit) OVER (PARTITION BY GroupKey) as TotalGrossProfit,
            -- 粗利率（0除算対策）
            CASE 
                WHEN SUM(Amount) OVER (PARTITION BY GroupKey) > 0 
                THEN (SUM(GrossProfit) OVER (PARTITION BY GroupKey) / SUM(Amount) OVER (PARTITION BY GroupKey)) * 100
                ELSE 0 
            END as GrossProfitRate
            
        FROM CalculatedData
        ORDER BY ProductCategory1, GroupKey, SortKey;
        
        -- 一時テーブルは自動的にクリーンアップされる

    END TRY
    BEGIN CATCH
        SELECT @ErrorMessage = ERROR_MESSAGE(),
               @ErrorSeverity = ERROR_SEVERITY(),
               @ErrorState = ERROR_STATE();

        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO