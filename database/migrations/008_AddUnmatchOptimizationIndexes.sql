-- =============================================
-- Migration 008 (修正版): アンマッチチェック用インデックス最適化
-- 作成日: 2025-08-26
-- 修正内容: CpInventoryMasterのDataSetIdを削除し、実際の構造に合わせる
-- =============================================

BEGIN TRY
    BEGIN TRANSACTION;
    
    PRINT '🔧 Creating indexes for unmatch check performance optimization...';
    PRINT '';
    
    -- 1. InventoryMaster: アクティブフラグと日付によるフィルタリング最適化
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('InventoryMaster') 
        AND name = 'IX_InventoryMaster_IsActive_JobDate'
    )
    BEGIN
        CREATE INDEX IX_InventoryMaster_IsActive_JobDate 
        ON InventoryMaster(IsActive, JobDate) 
        INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, CurrentStock, CurrentStockAmount);
        
        PRINT '✅ InventoryMaster index for IsActive/JobDate created';
    END
    ELSE
    BEGIN
        PRINT '⚠️ InventoryMaster IsActive/JobDate index already exists';
    END
    
    -- 2. SalesVouchers: 日付フィルタリング最適化
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('SalesVouchers') 
        AND name = 'IX_SalesVouchers_JobDate_VoucherType'
    )
    BEGIN
        CREATE INDEX IX_SalesVouchers_JobDate_VoucherType 
        ON SalesVouchers(JobDate, VoucherType, DetailType) 
        INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, Quantity);
        
        PRINT '✅ SalesVouchers index for JobDate/VoucherType created';
    END
    ELSE
    BEGIN
        PRINT '⚠️ SalesVouchers JobDate/VoucherType index already exists';
    END
    
    -- 3. PurchaseVouchers: 日付フィルタリング最適化
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('PurchaseVouchers') 
        AND name = 'IX_PurchaseVouchers_JobDate_VoucherType'
    )
    BEGIN
        CREATE INDEX IX_PurchaseVouchers_JobDate_VoucherType 
        ON PurchaseVouchers(JobDate, VoucherType, DetailType) 
        INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, Quantity);
        
        PRINT '✅ PurchaseVouchers index for JobDate/VoucherType created';
    END
    ELSE
    BEGIN
        PRINT '⚠️ PurchaseVouchers JobDate/VoucherType index already exists';
    END
    
    -- 4. InventoryAdjustments: 日付フィルタリング最適化
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('InventoryAdjustments') 
        AND name = 'IX_InventoryAdjustments_JobDate_CategoryCode'
    )
    BEGIN
        CREATE INDEX IX_InventoryAdjustments_JobDate_CategoryCode 
        ON InventoryAdjustments(JobDate, VoucherType, DetailType, CategoryCode) 
        INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, Quantity);
        
        PRINT '✅ InventoryAdjustments index for JobDate/CategoryCode created';
    END
    ELSE
    BEGIN
        PRINT '⚠️ InventoryAdjustments JobDate/CategoryCode index already exists';
    END
    
    -- 5. CpInventoryMaster: 5項目キー検索用の複合インデックス
    -- 注意: CpInventoryMasterは日次処理で作成・削除される一時テーブルですが、
    --      処理中は複数の帳票で使用されるため、パフォーマンス向上のためインデックスを作成
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'CpInventoryMaster')
    BEGIN
        IF NOT EXISTS (
            SELECT 1 
            FROM sys.indexes 
            WHERE object_id = OBJECT_ID('CpInventoryMaster') 
            AND name = 'IX_CpInventoryMaster_5ItemKey_JobDate'
        )
        BEGIN
            CREATE INDEX IX_CpInventoryMaster_5ItemKey_JobDate 
            ON CpInventoryMaster(JobDate, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark)
            INCLUDE (DailyStock, DailyStockAmount, PreviousDayStock, PreviousDayStockAmount);
            
            PRINT '✅ CpInventoryMaster 5-item key index created';
        END
        ELSE
        BEGIN
            PRINT '⚠️ CpInventoryMaster 5-item key index already exists';
        END
        
        -- 追加: CP在庫マスタの高速検索用インデックス
        IF NOT EXISTS (
            SELECT 1 
            FROM sys.indexes 
            WHERE object_id = OBJECT_ID('CpInventoryMaster') 
            AND name = 'IX_CpInventoryMaster_ProductCode'
        )
        BEGIN
            CREATE NONCLUSTERED INDEX IX_CpInventoryMaster_ProductCode
            ON CpInventoryMaster(ProductCode, GradeCode, ClassCode)
            INCLUDE (ShippingMarkCode, ManualShippingMark, JobDate, DailyStock);
            
            PRINT '✅ CpInventoryMaster ProductCode index created';
        END
    END
    ELSE
    BEGIN
        PRINT '⚠️ CpInventoryMaster table does not exist (will be created during daily processing)';
    END
    
    -- 6. UPDATE STATISTICS: 新しいインデックスの統計情報を更新
    PRINT '';
    PRINT '📊 Updating statistics for indexes...';
    
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InventoryMaster')
        UPDATE STATISTICS InventoryMaster;
        
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SalesVouchers')
        UPDATE STATISTICS SalesVouchers;
        
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseVouchers')
        UPDATE STATISTICS PurchaseVouchers;
        
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InventoryAdjustments')
        UPDATE STATISTICS InventoryAdjustments;
        
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'CpInventoryMaster')
        UPDATE STATISTICS CpInventoryMaster;
    
    COMMIT TRANSACTION;
    PRINT '';
    PRINT '🎉 Migration 008 completed successfully - Unmatch optimization indexes added';
    
    -- パフォーマンス検証用のクエリ例を出力
    PRINT '';
    PRINT '📋 Performance verification queries:';
    PRINT '-- Test active inventory filtering:';
    PRINT 'SELECT COUNT(*) FROM InventoryMaster WHERE IsActive = 1 AND JobDate = ''2025-06-30'';';
    PRINT '-- Test sales voucher filtering:';
    PRINT 'SELECT COUNT(*) FROM SalesVouchers WHERE JobDate = ''2025-06-30'' AND VoucherType IN (''51'', ''52'') AND DetailType IN (''1'', ''2'');';
    PRINT '-- Test CP inventory master:';
    PRINT 'SELECT COUNT(*) FROM CpInventoryMaster WHERE JobDate = ''2025-06-30'' AND ProductCode = ''10001'';';
    
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    
    PRINT '❌ Migration 008 failed: ' + @ErrorMessage;
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
GO