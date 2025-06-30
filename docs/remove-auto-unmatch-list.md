# import-folderコマンドからアンマッチリスト自動実行を削除

## 問題の概要
`import-folder`コマンドがCSVファイルのインポート後に自動的にアンマッチリスト処理を実行していたため、以下の問題が発生していた：

1. **処理時間の増加**
   - CSVインポートのみを行いたい場合でも、必ずアンマッチリスト処理が実行される
   
2. **エラーの発生**
   - PreviousMonthInventoryテーブルが存在しない環境でエラー
   - FastReportテンプレートファイルが読み込めない環境でエラー

3. **責務の混在**
   - import-folderコマンドの本来の責務（CSVインポート）以外の処理が含まれている

## 修正内容
`Program.cs`の`ExecuteImportFromFolderAsync`メソッドから、アンマッチリスト処理の自動実行をコメントアウト：

```csharp
// ========== アンマッチリスト処理 ==========
// 注意：アンマッチリスト処理は別途 create-unmatch-list コマンドで実行してください
// await ExecuteUnmatchListAfterImport(scopedServices, jobDate, logger);
```

## 使用方法の変更

### 修正前
```bash
# CSVインポートとアンマッチリスト処理が自動実行される
dotnet run -- import-folder DeptA 2025-06-30
```

### 修正後
```bash
# CSVインポートのみ実行
dotnet run -- import-folder DeptA 2025-06-30

# 必要に応じて別途アンマッチリスト処理を実行
dotnet run -- create-unmatch-list 2025-06-30
```

## メリット

1. **処理の分離**
   - 各コマンドの責務が明確になる
   - 必要な処理のみを選択的に実行できる

2. **エラーの削減**
   - 環境依存のエラーが減少
   - CSVインポートのみの実行が可能

3. **パフォーマンスの向上**
   - 不要な処理をスキップできる
   - 処理時間が短縮される

## 注意事項

- CSVインポート後にアンマッチリストを確認したい場合は、別途`create-unmatch-list`コマンドを実行する必要がある
- `ExecuteUnmatchListAfterImport`メソッドは将来の使用のために残されている

## 変更ファイル
- `/src/InventorySystem.Console/Program.cs`