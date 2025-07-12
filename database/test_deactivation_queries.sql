-- Test queries for zero-stock deactivation feature
-- Author: Claude Code
-- Date: 2025-07-12

PRINT '=== Testing Zero-Stock Deactivation Feature ===';

-- 1. Check current inventory state
PRINT '📊 Current inventory state:';
SELECT 
    'Active' as Status,
    COUNT(*) as Count,
    SUM(CASE WHEN CurrentStock = 0 THEN 1 ELSE 0 END) as ZeroStockCount,
    SUM(CASE WHEN CurrentStock = 0 AND ISNULL(PreviousMonthQuantity, 0) = 0 THEN 1 ELSE 0 END) as ZeroStockAndPreviousMonthCount
FROM InventoryMaster 
WHERE IsActive = 1

UNION ALL

SELECT 
    'Inactive' as Status,
    COUNT(*) as Count,
    SUM(CASE WHEN CurrentStock = 0 THEN 1 ELSE 0 END) as ZeroStockCount,
    SUM(CASE WHEN CurrentStock = 0 AND ISNULL(PreviousMonthQuantity, 0) = 0 THEN 1 ELSE 0 END) as ZeroStockAndPreviousMonthCount
FROM InventoryMaster 
WHERE IsActive = 0;

-- 2. Test deactivation target count query (180 days)
PRINT '';
PRINT '🎯 Deactivation targets (180 days):';
SELECT COUNT(*) as TargetCount
FROM InventoryMaster
WHERE CurrentStock = 0
    AND ISNULL(PreviousMonthQuantity, 0) = 0
    AND IsActive = 1
    AND DATEDIFF(DAY, 
        COALESCE(UpdatedDate, JobDate), 
        GETDATE()) >= 180;

-- 3. Test with different day thresholds
PRINT '';
PRINT '📅 Targets by different day thresholds:';
SELECT 
    Days,
    COUNT(*) as TargetCount
FROM (
    SELECT 
        30 as Days,
        CASE 
            WHEN CurrentStock = 0
                AND ISNULL(PreviousMonthQuantity, 0) = 0
                AND IsActive = 1
                AND DATEDIFF(DAY, COALESCE(UpdatedDate, JobDate), GETDATE()) >= 30
            THEN 1 ELSE 0 
        END as IsTarget
    FROM InventoryMaster
    
    UNION ALL
    
    SELECT 
        90 as Days,
        CASE 
            WHEN CurrentStock = 0
                AND ISNULL(PreviousMonthQuantity, 0) = 0
                AND IsActive = 1
                AND DATEDIFF(DAY, COALESCE(UpdatedDate, JobDate), GETDATE()) >= 90
            THEN 1 ELSE 0 
        END as IsTarget
    FROM InventoryMaster
    
    UNION ALL
    
    SELECT 
        180 as Days,
        CASE 
            WHEN CurrentStock = 0
                AND ISNULL(PreviousMonthQuantity, 0) = 0
                AND IsActive = 1
                AND DATEDIFF(DAY, COALESCE(UpdatedDate, JobDate), GETDATE()) >= 180
            THEN 1 ELSE 0 
        END as IsTarget
    FROM InventoryMaster
) t
WHERE IsTarget = 1
GROUP BY Days
ORDER BY Days;

-- 4. Show sample records that would be deactivated
PRINT '';
PRINT '📋 Sample records for deactivation (top 10):';
SELECT TOP 10
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    CurrentStock,
    PreviousMonthQuantity,
    UpdatedDate,
    JobDate,
    DATEDIFF(DAY, COALESCE(UpdatedDate, JobDate), GETDATE()) as DaysSinceUpdate
FROM InventoryMaster
WHERE CurrentStock = 0
    AND ISNULL(PreviousMonthQuantity, 0) = 0
    AND IsActive = 1
    AND DATEDIFF(DAY, COALESCE(UpdatedDate, JobDate), GETDATE()) >= 180
ORDER BY UpdatedDate DESC;

-- 5. Performance test (execution plan)
PRINT '';
PRINT '⚡ Performance test - Count query execution:';
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT COUNT(*) as PerformanceTestCount
FROM InventoryMaster
WHERE CurrentStock = 0
    AND ISNULL(PreviousMonthQuantity, 0) = 0
    AND IsActive = 1
    AND DATEDIFF(DAY, COALESCE(UpdatedDate, JobDate), GETDATE()) >= 180;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;

PRINT '';
PRINT '✅ Deactivation feature test completed';