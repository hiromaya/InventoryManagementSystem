# Database Schema Updates

## Version 2.0 Schema Update (2025-01-23)

### Overview
This update adds missing columns to support the UnmatchList and DailyReport functionality.

### Changes
1. **InventoryMaster table**:
   - Added: DataSetId (NVARCHAR(50))
   - Added: DailyGrossProfit (DECIMAL(18,4))
   - Added: DailyAdjustmentAmount (DECIMAL(18,4))
   - Added: DailyProcessingCost (DECIMAL(18,4))
   - Added: FinalGrossProfit (DECIMAL(18,4))

2. **InventoryAdjustments table**:
   - Added: CategoryCode (INT)
   - Added: CustomerCode (NVARCHAR(20))
   - Added: CustomerName (NVARCHAR(100))

### Update Instructions

#### For existing databases:
```powershell
# Windows (PowerShell)
sqlcmd -S "(localdb)\MSSQLLocalDB" -i "database\update_schema_v2.sql"

# Linux (if using SQL Server on Linux)
sqlcmd -S localhost -U sa -P YourPassword -i database/update_schema_v2.sql
```

#### For new installations:
```powershell
# Use the updated create_schema.sql which includes all columns
sqlcmd -S "(localdb)\MSSQLLocalDB" -i "database\create_schema.sql"
```

### Verification
To verify the schema update was successful:

```sql
-- Check InventoryMaster columns
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'InventoryMaster' 
ORDER BY ORDINAL_POSITION;

-- Check InventoryAdjustments columns
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'InventoryAdjustments' 
ORDER BY ORDINAL_POSITION;
```

### Rollback
If you need to rollback these changes, use:
```sql
-- Remove added columns from InventoryMaster
ALTER TABLE InventoryMaster DROP COLUMN DataSetId;
ALTER TABLE InventoryMaster DROP COLUMN DailyGrossProfit;
ALTER TABLE InventoryMaster DROP COLUMN DailyAdjustmentAmount;
ALTER TABLE InventoryMaster DROP COLUMN DailyProcessingCost;
ALTER TABLE InventoryMaster DROP COLUMN FinalGrossProfit;

-- Remove added columns from InventoryAdjustments
ALTER TABLE InventoryAdjustments DROP COLUMN CategoryCode;
ALTER TABLE InventoryAdjustments DROP COLUMN CustomerCode;
ALTER TABLE InventoryAdjustments DROP COLUMN CustomerName;
```

### Notes
- The update script is idempotent - it can be run multiple times safely
- All new columns have default values to prevent breaking existing data
- The DataSetId column is indexed for performance