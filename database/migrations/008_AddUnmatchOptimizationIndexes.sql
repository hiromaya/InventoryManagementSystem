-- Migration 008: Add indexes for unmatch check performance optimization
-- Author: Claude Code with Gemini CLI consultation
-- Date: 2025-07-12
-- Purpose: Add indexes to optimize date-filtered unmatch check queries

BEGIN TRY
    BEGIN TRANSACTION;
    
    PRINT '🔧 Creating indexes for unmatch check performance optimization...';
    
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
        INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, CurrentStock, CurrentStockAmount);
        
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
        INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, Quantity);
        
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
        INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, Quantity);
        
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
        INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, Quantity);
        
        PRINT '✅ InventoryAdjustments index for JobDate/CategoryCode created';
    END
    ELSE
    BEGIN
        PRINT '⚠️ InventoryAdjustments JobDate/CategoryCode index already exists';
    END
    
    -- 5. 5項目キー検索用の複合インデックス（CpInventoryMaster）
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('CpInventoryMaster') 
        AND name = 'IX_CpInventoryMaster_5ItemKey_DataSetId'
    )
    BEGIN
        CREATE INDEX IX_CpInventoryMaster_5ItemKey_DataSetId 
        ON CpInventoryMaster(DataSetId, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);
        
        PRINT '✅ CpInventoryMaster 5-item key index created';
    END
    ELSE
    BEGIN
        PRINT '⚠️ CpInventoryMaster 5-item key index already exists';
    END
    
    -- 6. UPDATE STATISTICS: 新しいインデックスの統計情報を更新
    PRINT '📊 Updating statistics for new indexes...';
    UPDATE STATISTICS InventoryMaster;
    UPDATE STATISTICS SalesVouchers;
    UPDATE STATISTICS PurchaseVouchers;
    UPDATE STATISTICS InventoryAdjustments;
    UPDATE STATISTICS CpInventoryMaster;
    
    COMMIT TRANSACTION;
    PRINT '🎉 Migration 008 completed successfully - Unmatch optimization indexes added';
    
    -- パフォーマンス検証用のクエリ例を出力
    PRINT '';
    PRINT '📋 Performance verification queries:';
    PRINT '-- Test active inventory filtering:';
    PRINT 'SELECT COUNT(*) FROM InventoryMaster WHERE IsActive = 1 AND JobDate <= ''2025-06-30'';';
    PRINT '-- Test sales voucher filtering:';
    PRINT 'SELECT COUNT(*) FROM SalesVouchers WHERE JobDate <= ''2025-06-30'' AND VoucherType IN (''51'', ''52'') AND DetailType IN (''1'', ''2'');';
    
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