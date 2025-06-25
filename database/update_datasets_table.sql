-- DataSets�e�[�u���̍X�V�X�N���v�g
-- ���s��: 2025-06-25

USE InventoryManagementDB;
GO

-- 1. ������DataSets�e�[�u���̍\�����m�F
PRINT '=== DataSets�e�[�u���X�V�J�n ===';
GO

-- 2. �K�v�ȃJ������ǉ�
-- DataSetType �J�����̒ǉ�
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'DataSetType')
BEGIN
    ALTER TABLE DataSets ADD DataSetType NVARCHAR(50) NOT NULL DEFAULT 'Unknown';
    PRINT 'DataSetType�J������ǉ����܂���';
END

-- ImportedAt �J�����̒ǉ�
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'ImportedAt')
BEGIN
    ALTER TABLE DataSets ADD ImportedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    PRINT 'ImportedAt�J������ǉ����܂���';
END

-- RecordCount �J�����̒ǉ�
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'RecordCount')
BEGIN
    ALTER TABLE DataSets ADD RecordCount INT NOT NULL DEFAULT 0;
    PRINT 'RecordCount�J������ǉ����܂���';
END

-- FilePath �J�����̒ǉ�
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'FilePath')
BEGIN
    ALTER TABLE DataSets ADD FilePath NVARCHAR(500) NULL;
    PRINT 'FilePath�J������ǉ����܂���';
END

-- CreatedAt �J�����̒ǉ��iCreatedDate���疼�O�ύX�܂��͐V�K�ǉ��j
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'CreatedDate')
BEGIN
    EXEC sp_rename 'DataSets.CreatedDate', 'CreatedAt', 'COLUMN';
    PRINT 'CreatedDate�J������CreatedAt�ɖ��O�ύX���܂���';
END
ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE DataSets ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    PRINT 'CreatedAt�J������ǉ����܂���';
END

-- UpdatedAt �J�����̒ǉ��iUpdatedDate���疼�O�ύX�܂��͐V�K�ǉ��j
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'UpdatedDate')
BEGIN
    EXEC sp_rename 'DataSets.UpdatedDate', 'UpdatedAt', 'COLUMN';
    PRINT 'UpdatedDate�J������UpdatedAt�ɖ��O�ύX���܂���';
END
ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE DataSets ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    PRINT 'UpdatedAt�J������ǉ����܂���';
END

-- 3. �C���f�b�N�X�̒ǉ�
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('DataSets') AND name = 'IX_DataSets_DataSetType')
BEGIN
    CREATE INDEX IX_DataSets_DataSetType ON DataSets(DataSetType);
    PRINT 'DataSetType�C���f�b�N�X���쐬���܂���';
END

PRINT '';
PRINT '=== DataSets�e�[�u���X�V���� ===';
GO

-- �X�V��̍\�����m�F
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'DataSets'
ORDER BY ORDINAL_POSITION;
GO