# SE2 仕様（商品日報 + Process 2-5）

この文書は、`se2/CLAUDE.md` の内容を元にした正式な取り込み版です。SE2領域（商品日報・粗利/歩引き計算）に関する仕様のソース・オブ・トゥルースとして参照してください。原本: `../../se2/CLAUDE.md`

---

## 役割と担当範囲
- ロール: SE2（System Engineer 2）
- 担当機能:
  - 商品日報の生成（FastReport.NETでPDF出力）
  - Process 2-5（粗利・歩引き金計算）
  - Process 2-6（奨励金計算）の準備・連携

---

## アーキテクチャと実行順序

- CP在庫マスタ（`CpInventoryMaster`）
  - 作成/削除責任はSE3、SE2は読み取り専用で利用
  - 商品日報生成前に、当日集計領域（売上/仕入/調整/加工/振替/粗利等）を更新

- 実行フロー（概要）
  1. import-folder → unmatch-list（アンマッチ0件確認）
  2. daily-report 実行（内部でProcess 2-5を実行）
  3. 商品日報PDFを出力

---

## Process 2-5 仕様（粗利・歩引き）

- 対象: 売上伝票1行ごと（`SalesVouchers`）
- 在庫単価取得: 5項目キー（商品コード/等級/階級/荷印コード/手入力荷印）で `CpInventoryMaster.DailyUnitPrice` を引く
- 粗利計算:
  - 基本: `(売上単価 - 在庫単価) × 数量` を四捨五入（小数4桁）
  - 売上単価=0 かつ 金額≠0 のとき: `売上単価 = 金額 / 数量`（小数4桁、四捨五入）
- 歩引き金計算:
  - 得意先マスタの歩引き率（汎用数値1相当、`CustomerMaster.WalkingRate`）
  - `歩引き金 = 丸め(金額 × 率/100, 2桁)`
- 書き込み先（本リポジトリ実装に準拠）:
  - `SalesVouchers.GrossProfit`（粗利益）
  - `SalesVouchers.WalkingDiscount`（歩引き金）

注意: 仕様原文の「汎用数値1/2（GenericNumeric1/2）」は本実装では専用カラム（`GrossProfit` / `WalkingDiscount`）に置き換えています。

---

## 奨励金（Process 2-6）
- 仕入先分類1が `01` の場合、`奨励金 = 丸め(仕入金額 × 1%, 0)` を当日奨励金に加算（`CpInventoryMaster.DailyIncentiveAmount`）
- 実装状況は準拠チェックを参照

---

## 商品日報 仕様

- レイアウト: A3横向き、フォント MS ゴシック、日計・月計の左右並列
- 表示項目（左→右）:
  - 日計: 商品名／売上数量／売上金額／仕入値引／在庫調整／加工費／振替／奨励金／1粗利益／1粗利率／2粗利益／2粗利率
  - 月計: 売上金額／1粗利益／1粗利率／2粗利益／2粗利率
- 特殊表示:
  - 負値は「▲」を数値末尾に付ける（例: `1,234▲`）
  - 率は小数2桁表示、負の場合は末尾に「-」または「▲%」表記（テンプレート仕様に準拠）
- グループ制御: 商品分類1（担当者）単位で小計・改ページ

---

## 主要クラス・インターフェース（実装準拠）

- 粗利・歩引き計算（Process 2-5）
  - `InventorySystem.Core.Services.GrossProfitCalculationService`
    - 在庫単価反映、粗利/歩引き計算、売上伝票の一括更新
- 商品日報（データ生成＋PDF）
  - `InventorySystem.Core.Services.DailyReportService`（データ生成・集計）
  - `InventorySystem.Reports.FastReport.Services.DailyReportFastReportService`（PDF生成、Windowsのみ）
  - Linux環境ではプレースホルダ実装を使用
- インターフェース
  - `InventorySystem.Core.Interfaces.IDailyReportService`
  - `InventorySystem.Reports.Interfaces.IDailyReportService`

---

## コマンド（実装準拠）

- `daily-report <YYYY-MM-DD> [--dataset-id ID]`
  - アンマッチ0件確認（`--dataset-id` 指定時）
  - 必要な当日集計を経て商品日報PDFを出力
- `dev-daily-report <YYYY-MM-DD> [--skip-unmatch-check]`
  - 開発用、制限を緩和

補足: 原仕様の「`process-2-5` 単独実行」は、本実装では `daily-report` 内で自動実行されます（単独コマンドは提供していません）。

---

## データ項目（CP在庫マスタ 抜粋）
- 当日エリア: 売上数量/金額、仕入数量/金額、在庫調整、加工、振替、出入荷、粗利益（1/2段階反映先は集計ロジックに準拠）、歩引き、奨励金、在庫数量/金額/単価
- 月計エリア: 売上、仕入、在庫調整、加工、振替、粗利益、歩引き、奨励金

---

## 例外/ビジネスルール（原仕様）
- 商品分類5が `99999` の場合、粗利・歩引きは0として扱う
- Rate計算は分母0のとき0%

注: 一部ルールは実装での適用箇所が異なる可能性があります。準拠状況はチェックリストを参照してください。

---

## 参考
- 元仕様: `se2/CLAUDE.md`
- 実装: `src/InventorySystem.Core/Services/DailyReportService.cs`、`src/InventorySystem.Core/Services/GrossProfitCalculationService.cs`
- PDF: `src/InventorySystem.Reports/FastReport/Services/DailyReportFastReportService.cs`、テンプレート `src/InventorySystem.Reports/FastReport/Templates/DailyReport.frx`

