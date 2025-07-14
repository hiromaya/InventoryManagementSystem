# 手動修正手順

## エラー修正手順

### 1. データベース初期化
```powershell
cd C:\Development\InventoryManagementSystem\src\InventorySystem.Console
dotnet run init-database --force
```

### 2. ストアドプロシージャ作成
```powershell
cd C:\Development\InventoryManagementSystem
sqlcmd -S localhost -d InventoryDB -E -i database\procedures\sp_CreateCpInventoryFromInventoryMasterCumulative.sql
```

### 3. インデックス作成
```powershell
sqlcmd -S localhost -d InventoryDB -E -i database\indexes\create_inventory_composite_index.sql
```

### 4. アンマッチリスト処理を再実行
```powershell
cd C:\Development\InventoryManagementSystem\src\InventorySystem.Console
dotnet run unmatch-list 2025-06-01
```