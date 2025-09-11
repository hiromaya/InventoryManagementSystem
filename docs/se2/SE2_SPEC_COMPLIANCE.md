# SE2仕様 準拠チェックリスト（取り込み状況）

目的: `docs/se2/SE2_SPEC.md` への準拠状況を可視化し、ギャップを早期に解消する。

## ファイル/構成
- [x] FastReport テンプレート配置（`FastReport/Templates`）
  - [x] `DailyReport.frx`
- [x] FastReport サービス実装
  - [x] `DailyReportFastReportService.cs`（Windows限定）
  - [x] Linux用プレースホルダ
- [x] コンソール コマンド
  - [x] `daily-report`
  - [x] `dev-daily-report`
  - [ ] `process-2-5` 単独コマンド（原仕様）
    - 備考: 本実装は `daily-report` 実行中に自動実行（単独コマンドは未提供）

## Process 2-5（粗利・歩引き）
- [x] 在庫単価の付与（5項目キーで `CpInventoryMaster.DailyUnitPrice` 参照）
- [x] 粗利計算（売上単価0→金額/数量で補完、四捨五入）
- [x] 歩引き金計算（`CustomerMaster.WalkingRate` 使用、四捨五入）
- [x] 売上伝票への書き込み
  - 実装: `SalesVouchers.GrossProfit` / `SalesVouchers.WalkingDiscount`
  - 注記: 原仕様の `汎用数値1/2` は本実装では専用カラムへ置換済み
- [ ] 商品分類5=99999 例外処理（粗利/歩引き=0）
  - 状況: 明示ロジック未確認。必要なら `GrossProfitCalculationService` へ追加要検討

## 奨励金（Process 2-6）
- [ ] 仕入先分類1='01' の奨励金計算（1%）と集計
  - 状況: 当日奨励金カラムは存在（`CpInventoryMaster.DailyIncentiveAmount`）が、計算/集計ロジックは未実装

## 商品日報（PDF）
- [x] レイアウト: A3横、日計/月計の並列
- [x] 項目: 原仕様の順序に対応
- [x] 負値の「▲」表示（2粗利益等）
- [x] 率表示（小数2桁、負号処理）
- [x] 商品分類1（担当者）ごとの小計/改ページ相当

## エラーハンドリング/品質
- [x] アンマッチ0件検証（`--dataset-id` 指定時）
- [x] 0除算対策
- [x] ログと例外処理（PDF生成/データ生成）

## 次アクション（ギャップ）
- [ ] 商品分類5=99999 例外処理の明示実装（Process 2-5）
- [ ] 奨励金（Process 2-6）計算・集計の実装
- [ ] `process-2-5` 単独コマンドが必要な場合のCLI追加
- [ ] `dotnet run help` に仕様ドキュメントパス表示（ヘルプの拡充）

参考: 仕様本文 `docs/se2/SE2_SPEC.md`
