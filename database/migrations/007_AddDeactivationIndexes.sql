-- Migration 007: Add indexes for deactivation feature performance optimization
-- Author: Claude Code
-- Date: 2025-07-12
-- Purpose: Add indexes to optimize zero-stock deactivation queries

BEGIN TRY
    BEGIN TRANSACTION;
    
    -- Check if the index already exists
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('InventoryMaster') 
        AND name = 'IX_InventoryMaster_Deactivation'
    )
    BEGIN
        PRINT '🔧 Creating index for zero-stock deactivation performance...';
        
        -- Create composite index for deactivation queries
        CREATE INDEX IX_InventoryMaster_Deactivation 
        ON InventoryMaster(IsActive, CurrentStock, PreviousMonthQuantity, UpdatedDate) 
        INCLUDE (JobDate, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);
        
        PRINT '✅ Index IX_InventoryMaster_Deactivation created successfully';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index IX_InventoryMaster_Deactivation already exists';
    END
    
    -- Optional: Create an additional index for general performance
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('InventoryMaster') 
        AND name = 'IX_InventoryMaster_IsActive_UpdatedDate'
    )
    BEGIN
        PRINT '🔧 Creating general performance index...';
        
        CREATE INDEX IX_InventoryMaster_IsActive_UpdatedDate 
        ON InventoryMaster(IsActive, UpdatedDate);
        
        PRINT '✅ Index IX_InventoryMaster_IsActive_UpdatedDate created successfully';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index IX_InventoryMaster_IsActive_UpdatedDate already exists';
    END
    
    COMMIT TRANSACTION;
    PRINT '🎉 Migration 007 completed successfully - Deactivation indexes added';
    
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    
    PRINT '❌ Migration 007 failed: ' + @ErrorMessage;
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;