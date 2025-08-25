-- =============================================
-- 既存のCpInventoryMasterデータの等級名・階級名を更新
-- SE3: 商品勘定・在庫表担当（恒久的解決策）
-- 作成日: 2025-08-25
-- =============================================

USE InventoryManagementDB;
GO

-- 実行前のデータ状況確認
DECLARE @BeforeCount INT;
SELECT @BeforeCount = COUNT(*) FROM CpInventoryMaster WHERE GradeName = '' OR ClassName = '';

IF @BeforeCount > 0
BEGIN
    PRINT '更新対象レコード数: ' + CAST(@BeforeCount AS VARCHAR(10)) + '件';
    
    -- 既存データの更新
    UPDATE cp
    SET 
        GradeName = ISNULL(gm.GradeName, 
            CASE 
                WHEN cp.GradeCode = '000' THEN '未分類'
                WHEN cp.GradeCode IS NULL OR cp.GradeCode = '' THEN ''
                ELSE 'Grade-' + cp.GradeCode
            END),
        ClassName = ISNULL(cm.ClassName, 
            CASE 
                WHEN cp.ClassCode = '000' THEN '未分類'
                WHEN cp.ClassCode IS NULL OR cp.ClassCode = '' THEN ''
                ELSE 'Class-' + cp.ClassCode
            END),
        UpdatedDate = GETDATE()
    FROM CpInventoryMaster cp
    LEFT JOIN GradeMaster gm ON cp.GradeCode = gm.GradeCode
    LEFT JOIN ClassMaster cm ON cp.ClassCode = cm.ClassCode
    WHERE cp.GradeName = '' OR cp.ClassName = '';

    PRINT '更新された行数: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + '件';
END
ELSE
BEGIN
    PRINT '更新対象のデータがありません。';
END

-- 実行後のデータ状況確認
DECLARE @AfterCount INT;
SELECT @AfterCount = COUNT(*) FROM CpInventoryMaster WHERE GradeName = '' OR ClassName = '';

PRINT '更新後の空白レコード数: ' + CAST(@AfterCount AS VARCHAR(10)) + '件';

-- サンプルデータの確認
PRINT '=== 更新後のサンプルデータ（上位10件） ===';
SELECT TOP 10 
    ProductCode, 
    GradeCode, 
    GradeName, 
    ClassCode, 
    ClassName,
    JobDate
FROM CpInventoryMaster
ORDER BY ProductCode;

PRINT '062_UpdateCpInventoryMasterNames.sql実行完了';
GO