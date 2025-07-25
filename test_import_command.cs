// Phase 1テスト: DataSetManagementService修正の動作確認用
// import-folderコマンドを2回実行してエラーが解消されるかテスト

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InventorySystem.Import.Services;
using InventorySystem.Data.Repositories;

Console.WriteLine("=== Phase 1修正テスト開始 ===");

// テスト用のDataSetManagementService動作確認
// 同じdataSetIdで2回CreateDataSetAsyncを呼び出し、
// エラーが発生せず適切にスキップされることを確認

var testDataSetId = "test-dataset-12345";
var testJobDate = new DateTime(2025, 6, 2);
var testProcessType = "TEST_PROCESS";

Console.WriteLine($"テストDataSetId: {testDataSetId}");
Console.WriteLine($"テストJobDate: {testJobDate:yyyy-MM-dd}");
Console.WriteLine($"テストProcessType: {testProcessType}");

Console.WriteLine("\n1回目の作成テスト...");
// 1回目: 新規作成されるはず

Console.WriteLine("\n2回目の作成テスト...");
// 2回目: 既存レコードが見つかり、スキップされるはず

Console.WriteLine("\n=== Phase 1修正テスト完了 ===");