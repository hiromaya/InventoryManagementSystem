# アンマッチリスト0件時のPDF出力修正

## 修正概要
アンマッチリストでアンマッチが0件の場合でも、仕様通りPDFを生成するように修正しました。

## 修正内容

### 1. Program.cs
- `ExecuteUnmatchListAsync`メソッドの修正
- 0件チェックの条件文を削除し、常にPDF生成処理を実行するように変更
- 0件の場合は「アンマッチ件数が0件です。0件のPDFを生成します」とメッセージを表示

### 2. UnmatchListReport.frx（FastReportテンプレート）
- PageHeaderBandにBeforePrintEventを追加
- C#スクリプトを追加して、0件時の表示制御を実装
- 0件時は以下を非表示に：
  - 表ヘッダー（Header1〜Header18）
  - ページ番号

## 0件時のPDF表示内容
仕様書（帳票レイアウトアンマッチリスト.pdf 2ページ目）に基づき、以下の要素のみ表示：

```
作成日：202Y年99月99日99時9分99秒
※　202Y年99月99日　アンマッチリスト　※


アンマッチ件数＝0000
```

## 技術的な詳細

### FastReportでの条件付き表示
```csharp
private void PageHeader1_BeforePrint(object sender, EventArgs e)
{
    // データ件数をチェック
    bool hasData = ((DataTable)Report.GetDataSource("UnmatchItems")).Rows.Count > 0;
    
    // ヘッダー項目の表示/非表示制御
    ((TextObject)Report.FindObject("Header1")).Visible = hasData;
    // ... 他のヘッダーも同様
    
    // ページ番号の表示/非表示
    ((TextObject)Report.FindObject("PageNumber")).Visible = hasData;
}
```

### 処理フロー
1. アンマッチリスト処理を実行
2. 結果が0件でもPDF生成処理に進む
3. FastReportで0件時の表示制御を適用
4. PDFを生成・保存
5. Windows環境では自動的にPDFを開く

## 動作確認項目
- [ ] アンマッチ0件時にPDFが生成される
- [ ] PDFに作成日時が表示される
- [ ] PDFにタイトルが中央に表示される
- [ ] PDFに「アンマッチ件数＝0000」が表示される
- [ ] 表ヘッダーが表示されない
- [ ] ページ番号が表示されない
- [ ] PDFが自動的に開かれる

## 変更ファイル
1. `/src/InventorySystem.Console/Program.cs`
2. `/src/InventorySystem.Reports/FastReport/Templates/UnmatchListReport.frx`