-- Migration 007: Add indexes for deactivation feature performance optimization
-- Author: Claude Code
-- Date: 2025-07-12
-- Purpose: Add indexes to optimize zero-stock deactivation queries

BEGIN TRY
    -- トランザクションを使わずに個別に実行
    
    -- 既存のインデックスを削除（存在する場合）
    IF EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('InventoryMaster') 
        AND name = 'IX_InventoryMaster_Deactivation'
    )
    BEGIN
        PRINT '🔧 Dropping existing index IX_InventoryMaster_Deactivation...';
        DROP INDEX IX_InventoryMaster_Deactivation ON InventoryMaster;
        PRINT '✅ Existing index dropped successfully';
    END
    
    -- Create composite index for deactivation queries
    PRINT '🔧 Creating index for zero-stock deactivation performance...';
    
    -- 実際に存在するカラムでインデックスを作成
    CREATE INDEX IX_InventoryMaster_Deactivation 
    ON InventoryMaster(IsActive, CurrentStock, UpdatedDate) 
    INCLUDE (JobDate, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, DailyStock);
    
    PRINT '✅ Index IX_InventoryMaster_Deactivation created successfully';
    
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
    
    -- DataSetIdインデックスは既に存在するのでスキップ
    IF EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE object_id = OBJECT_ID('InventoryMaster') 
        AND name = 'IX_InventoryMaster_DataSetId'
    )
    BEGIN
        PRINT '⚠️ Index IX_InventoryMaster_DataSetId already exists - skipping';
    END
    
    PRINT '🎉 Migration 007 completed successfully - Deactivation indexes added';
    
END TRY
BEGIN CATCH
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    
    PRINT '❌ Migration 007 failed: ' + @ErrorMessage;
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;