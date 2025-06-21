using System;
using System.Reflection;

namespace InventorySystem.Reports.Tests
{
    public class FastReportCompatibilityTest
    {
        public static void CheckCompatibility()
        {
            try
            {
                // FastReport.dllのパス
                var dllPath = @"C:\Program Files (x86)\FastReports\FastReport .NET Trial\FastReport.dll";
                
                // アセンブリを読み込む
                var assembly = Assembly.LoadFrom(dllPath);
                
                Console.WriteLine("=== FastReport互換性チェック ===");
                Console.WriteLine($"アセンブリ名: {assembly.GetName().Name}");
                Console.WriteLine($"バージョン: {assembly.GetName().Version}");
                Console.WriteLine($"ランタイムバージョン: {assembly.ImageRuntimeVersion}");
                
                // FastReportの基本的な型が存在するか確認
                var reportType = assembly.GetType("FastReport.Report");
                if (reportType != null)
                {
                    Console.WriteLine("✓ FastReport.Report型が見つかりました");
                    
                    // インスタンスを作成してみる
                    var report = Activator.CreateInstance(reportType);
                    Console.WriteLine("✓ Reportインスタンスを作成できました");
                }
                
                Console.WriteLine("\n互換性チェック完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー: {ex.Message}");
                Console.WriteLine($"詳細: {ex.ToString()}");
            }
        }
    }
}