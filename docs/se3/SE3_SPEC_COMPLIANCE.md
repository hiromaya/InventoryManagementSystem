# SE3仕様 準拠チェックリスト（取り込み状況）

目的: `docs/se3/SE3_SPEC.md` への準拠状況を可視化し、ギャップを早期に解消する。

## ファイル/構成
- [x] FastReport テンプレート配置（`FastReport/Templates`）
  - [x] `ProductAccount.frx`
  - [x] `InventoryList.frx`
- [x] FastReport サービス実装
  - [x] `ProductAccountFastReportService.cs`
  - [x] `InventoryListFastReportService.cs`
- [x] コンソール コマンド
  - [x] `product-account`
  - [x] `inventory-list`

## CP在庫マスタ関連
- [x] 作成フロー（TRUNCATE→コピー or 集計生成）
- [x] ストアドプロシージャ `sp_CreateCpInventoryFromInventoryMasterCumulative` の管理
- [x] リポジトリ経由の取得 `ICpInventoryRepository.GetAllAsync()`
- [x] 商品勘定で作成→在庫表で利用の連携

## 帳票要件（在庫表）
- [x] 担当者→商品→荷印→等級→階級でソート
- [x] フィルタ: 前日在庫=0除外、当日在庫数量・金額が共に0除外
- [x] 担当者ごとに改ページ、小計・合計行
- [x] 35行ページ制御
- [ ] 滞留マーク（`!/!!/!!!`）
  - 現状: プレースホルダ。判定ロジック未実装（`InventoryListFastReportService` Col9）。

## エラーハンドリング/品質
- [x] CP在庫未作成時の明示エラー
- [x] 0除算対策（計算時は0回避）
- [x] ログ出力とPDF生成失敗時の例外処理

## 次アクション（ギャップ）
- [ ] 在庫表 滞留マーク判定実装（最終入荷日ベースで `!/!!/!!!`）
- [ ] 在庫表 最終入荷日の表示（現在は空欄）
- [ ] 仕様リンクのヘルプ出力連携（`dotnet run --help` 等にドキュメントパスを表示）

参考: 仕様本文 `docs/se3/SE3_SPEC.md`

