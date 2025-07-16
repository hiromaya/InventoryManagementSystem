using System;

namespace TestShippingMarkValidation
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 荷印名・荷印コード検証テスト ===");

            // テストデータ
            string[] testShippingMarkNames = {
                "        ",  // 空白8文字
                " ｺｳ     ",  // 有効な荷印名
                " 3X12   ",  // 有効な荷印名
                "",          // 空文字列
                null!        // null
            };

            string[] testShippingMarkCodes = {
                "    ",      // 空白4文字
                "0001",      // 有効な荷印コード
                "8001",      // 有効な荷印コード
                "",          // 空文字列
                null!        // null
            };

            Console.WriteLine("--- 荷印名テスト ---");
            foreach (var name in testShippingMarkNames)
            {
                bool isValidByIsNullOrEmpty = string.IsNullOrEmpty(name);
                bool isValidByIsNullOrWhiteSpace = string.IsNullOrWhiteSpace(name);
                
                Console.WriteLine($"荷印名: '{name}' (null表示)");
                Console.WriteLine($"  IsNullOrEmpty: {isValidByIsNullOrEmpty} (true=無効)");
                Console.WriteLine($"  IsNullOrWhiteSpace: {isValidByIsNullOrWhiteSpace} (true=無効)");
                Console.WriteLine($"  現在の実装(IsNullOrEmpty): {(isValidByIsNullOrEmpty ? "エラー" : "有効")}");
                Console.WriteLine();
            }

            Console.WriteLine("--- 荷印コードテスト ---");
            foreach (var code in testShippingMarkCodes)
            {
                bool isValidByIsNullOrEmpty = string.IsNullOrEmpty(code);
                bool isValidByIsNullOrWhiteSpace = string.IsNullOrWhiteSpace(code);
                bool isValidByNullCheck = code == null;
                
                Console.WriteLine($"荷印コード: '{code}' (null表示)");
                Console.WriteLine($"  IsNullOrEmpty: {isValidByIsNullOrEmpty} (true=無効)");
                Console.WriteLine($"  IsNullOrWhiteSpace: {isValidByIsNullOrWhiteSpace} (true=無効)");
                Console.WriteLine($"  == null: {isValidByNullCheck} (true=無効)");
                Console.WriteLine($"  現在の実装(== null): {(isValidByNullCheck ? "エラー" : "有効")}");
                Console.WriteLine();
            }

            Console.WriteLine("=== 結論 ===");
            Console.WriteLine("荷印名（8文字）:");
            Console.WriteLine("  空白8文字「        」→ 有効 (IsNullOrEmpty=false)");
            Console.WriteLine("  空文字列「\"\"」→ 無効 (IsNullOrEmpty=true)");
            Console.WriteLine("  null → 無効 (IsNullOrEmpty=true)");
            Console.WriteLine();
            Console.WriteLine("荷印コード（4文字）:");
            Console.WriteLine("  空白4文字「    」→ 有効 (== null=false)");
            Console.WriteLine("  空文字列「\"\"」→ 有効 (== null=false)");
            Console.WriteLine("  null → 無効 (== null=true)");
        }
    }
}