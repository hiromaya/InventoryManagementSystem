-- DepartmentCodeカラムのサイズを拡張
ALTER TABLE CpInventoryMaster 
ALTER COLUMN DepartmentCode NVARCHAR(50);

-- 確認クエリ
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'CpInventoryMaster'
AND COLUMN_NAME = 'DepartmentCode';