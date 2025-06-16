using System.Runtime.InteropServices;

namespace InventorySystem.Reports.Services;

public static class FontHelper
{
    public static string GetJapaneseFontPath()
    {
        // OS判定
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Ubuntu/Debian系 - 利用可能なフォントパス（優先順）
            var fontPaths = new[]
            {
                "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
                "/usr/share/fonts/opentype/noto/NotoSerifCJK-Regular.ttc",
                "/usr/share/fonts/truetype/fonts-japanese-gothic.ttf",
                "/usr/share/fonts/opentype/ipafont-gothic/ipag.ttf",
                "/usr/share/fonts/opentype/ipaexfont-gothic/ipaexg.ttf",
                "/usr/share/fonts/opentype/ipafont-mincho/ipam.ttf",
                "/usr/share/fonts/truetype/fonts-japanese-mincho.ttf",
                "/usr/share/fonts/opentype/ipafont-gothic/ipagp.ttf"
            };
            
            foreach (var path in fontPaths)
            {
                if (File.Exists(path))
                {
                    Console.WriteLine($"日本語フォントを検出: {path}");
                    return path;
                }
            }
            
            throw new FileNotFoundException("日本語フォントが見つかりません。'sudo apt-get install fonts-noto-cjk'を実行してください。");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows用のフォントパス
            var windowsFontPaths = new[]
            {
                "C:\\Windows\\Fonts\\msgothic.ttc",
                "C:\\Windows\\Fonts\\msmincho.ttc",
                "C:\\Windows\\Fonts\\NotoSansCJKjp-Regular.otf",
                "C:\\Windows\\Fonts\\yugothic.ttf"
            };
            
            foreach (var path in windowsFontPaths)
            {
                if (File.Exists(path))
                {
                    Console.WriteLine($"日本語フォントを検出: {path}");
                    return path;
                }
            }
            
            throw new FileNotFoundException("Windows用の日本語フォントが見つかりません。");
        }
        
        throw new NotSupportedException($"サポートされていないOS: {RuntimeInformation.OSDescription}");
    }
    
    /// <summary>
    /// 利用可能な日本語フォントの一覧を取得
    /// </summary>
    public static List<string> GetAvailableJapaneseFonts()
    {
        var availableFonts = new List<string>();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var fontPaths = new[]
            {
                "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
                "/usr/share/fonts/opentype/noto/NotoSerifCJK-Regular.ttc",
                "/usr/share/fonts/truetype/fonts-japanese-gothic.ttf",
                "/usr/share/fonts/opentype/ipafont-gothic/ipag.ttf",
                "/usr/share/fonts/opentype/ipaexfont-gothic/ipaexg.ttf",
                "/usr/share/fonts/opentype/ipafont-mincho/ipam.ttf",
                "/usr/share/fonts/truetype/fonts-japanese-mincho.ttf"
            };
            
            availableFonts.AddRange(fontPaths.Where(File.Exists));
        }
        
        return availableFonts;
    }
}