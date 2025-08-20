using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace InventorySystem.Reports.Tools
{
    /// <summary>
    /// FastReport BusinessDailyReport.frx の重なり問題を修正するパッチユーティリティ
    /// 
    /// 修正内容：
    /// 1. PageHeaderBand の高さを 37.8 に統一（重なり解消）
    /// 2. DataBand.Top を適切に再計算
    /// 3. Page2/3 の 9列目パラメータを辞書に追加
    /// 4. 列X座標補正（Page2～4、列1..9）
    /// </summary>
    public static class FastReportPatcher
    {
        /// <summary>
        /// 指定された FRX ファイルにパッチを適用
        /// </summary>
        /// <param name="frxPath">修正対象の .frx ファイルパス</param>
        public static void Patch(string frxPath)
        {
            Console.WriteLine($"=== FastReport パッチ開始: {frxPath} ===");
            
            var doc = XDocument.Load(frxPath);
            var ns = (XNamespace)""; // FastReport frx は無名NS

            var modifications = 0;

            // 1) PageHeader 高さを 37.8 に統一
            Console.WriteLine("1. PageHeaderBand 高さ修正...");
            var pageHeaderBands = doc.Descendants("PageHeaderBand").ToList();
            foreach (var ph in pageHeaderBands)
            {
                var currentHeight = ph.Attribute("Height")?.Value;
                if (currentHeight != "37.8")
                {
                    ph.SetAttributeValue("Height", "37.8");
                    modifications++;
                    Console.WriteLine($"   PageHeader高さ: {currentHeight} → 37.8");
                }
            }

            // 2) DataBand.Top を RT+PH に再計算
            Console.WriteLine("2. DataBand.Top 再計算...");
            foreach (var page in doc.Descendants("ReportPage"))
            {
                var pageName = page.Attribute("Name")?.Value;
                var rt = page.Element("ReportTitleBand");
                var ph = page.Element("PageHeaderBand");
                var db = page.Element("DataBand");
                
                if (rt != null && ph != null && db != null)
                {
                    var rtHeight = ToDecimal(rt.Attribute("Height"));
                    var phHeight = ToDecimal(ph.Attribute("Height"));
                    var expectedTop = rtHeight + phHeight;
                    var currentTop = ToDecimal(db.Attribute("Top"));
                    
                    if (Math.Abs(currentTop - expectedTop) > 0.1m)
                    {
                        db.SetAttributeValue("Top", expectedTop.ToString(CultureInfo.InvariantCulture));
                        modifications++;
                        Console.WriteLine($"   {pageName} DataBand.Top: {currentTop} → {expectedTop}");
                    }
                }
            }

            // 3) Page2/3 の 9列目パラメータを辞書に追加
            Console.WriteLine("3. 9列目パラメータ追加...");
            var dict = doc.Root?.Element("Dictionary");
            if (dict != null)
            {
                var paramsToAdd = new[]
                {
                    "Page2_CustomerName9",
                    "Page2_SupplierName9", 
                    "Page3_CustomerName9",
                    "Page3_SupplierName9"
                };

                foreach (var paramName in paramsToAdd)
                {
                    if (EnsureParameter(dict, paramName))
                    {
                        modifications++;
                        Console.WriteLine($"   パラメータ追加: {paramName}");
                    }
                }
            }

            // 4) 列X座標補正（Page2～4、列1..9）
            Console.WriteLine("4. 列座標補正...");
            var targetColumns = new decimal[] { 70, 215, 360, 505, 650, 795, 940, 1085, 1230 };

            foreach (var page in doc.Descendants("ReportPage"))
            {
                var pageName = page.Attribute("Name")?.Value;
                if (pageName is "Page2" or "Page3" or "Page4")
                {
                    Console.WriteLine($"   {pageName} 処理中...");
                    
                    // PageHeader の Header_C*, Header_S* を補正
                    var ph = page.Element("PageHeaderBand");
                    if (ph != null)
                    {
                        var headerCFixed = FixHeaderColumns(ph, "Header_C", targetColumns, 0m, pageName);
                        var headerSFixed = FixHeaderColumns(ph, "Header_S", targetColumns, 18.9m, pageName);
                        modifications += headerCFixed + headerSFixed;
                        
                        if (headerCFixed > 0 || headerSFixed > 0)
                        {
                            Console.WriteLine($"     ヘッダー修正: Header_C={headerCFixed}, Header_S={headerSFixed}");
                        }
                    }

                    // DataBand の *_C1..C9 を補正（Daily/Monthly/Yearly 全行）
                    var db = page.Element("DataBand");
                    if (db != null)
                    {
                        var dataFixed = FixDataColumns(db, targetColumns);
                        modifications += dataFixed;
                        
                        if (dataFixed > 0)
                        {
                            Console.WriteLine($"     データ列修正: {dataFixed}箇所");
                        }
                    }
                }
            }

            // ファイル保存
            doc.Save(frxPath);
            
            Console.WriteLine($"=== FastReport パッチ完了: 総修正数 {modifications}箇所 ===");
        }

        /// <summary>
        /// XAttribute から decimal 値を取得（null の場合は 0）
        /// </summary>
        private static decimal ToDecimal(XAttribute? attribute)
        {
            return attribute == null ? 0m : decimal.Parse(attribute.Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// パラメータが辞書に存在しない場合は追加
        /// </summary>
        /// <returns>追加された場合は true</returns>
        private static bool EnsureParameter(XElement dictionary, string parameterName)
        {
            if (dictionary == null) return false;
            
            var exists = dictionary.Descendants("Parameter")
                                  .Any(p => (string?)p.Attribute("Name") == parameterName);
            if (!exists)
            {
                dictionary.Add(new XElement("Parameter",
                    new XAttribute("Name", parameterName),
                    new XAttribute("DataType", "System.String"),
                    new XAttribute("AsString", "")));
                return true;
            }
            return false;
        }

        /// <summary>
        /// ヘッダー列（Header_C* / Header_S*）の座標修正と9列目追加
        /// </summary>
        private static int FixHeaderColumns(XElement pageHeader, string prefix, decimal[] columnPositions, decimal top, string? pageName)
        {
            var fixedCount = 0;
            
            for (int i = 1; i <= columnPositions.Length; i++)
            {
                var headerName = $"{prefix}{i}";
                var textObject = pageHeader.Elements("TextObject")
                                          .FirstOrDefault(x => (string?)x.Attribute("Name") == headerName);
                
                if (textObject == null && i == 9)
                {
                    // 9列目が無い場合に作成
                    var paramType = prefix == "Header_C" ? "CustomerName" : "SupplierName";
                    var parameterRef = $"[{pageName}_{paramType}{i}]";
                    
                    textObject = new XElement("TextObject",
                        new XAttribute("Name", headerName),
                        new XAttribute("Left", columnPositions[i-1].ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("Top", top.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("Width", "145"),
                        new XAttribute("Height", "18.9"),
                        new XAttribute("CanGrow", "false"),
                        new XAttribute("WordWrap", "false"),
                        new XAttribute("HorzAlign", "Center"),
                        new XAttribute("Font", "ＭＳ ゴシック, 9pt"),
                        parameterRef);
                    
                    pageHeader.Add(textObject);
                    fixedCount++;
                    continue;
                }
                
                if (textObject != null)
                {
                    // 既存オブジェクトの座標とプロパティを修正
                    var currentLeft = ToDecimal(textObject.Attribute("Left"));
                    var expectedLeft = columnPositions[i-1];
                    
                    if (Math.Abs(currentLeft - expectedLeft) > 0.1m)
                    {
                        textObject.SetAttributeValue("Left", expectedLeft.ToString(CultureInfo.InvariantCulture));
                        fixedCount++;
                    }
                    
                    // 高さとプロパティを統一
                    textObject.SetAttributeValue("Top", top.ToString(CultureInfo.InvariantCulture));
                    textObject.SetAttributeValue("Height", "18.9");
                    textObject.SetAttributeValue("CanGrow", "false");
                    textObject.SetAttributeValue("WordWrap", "false");
                }
            }
            
            return fixedCount;
        }

        /// <summary>
        /// データ列（*_C1..C9）の座標修正
        /// </summary>
        private static int FixDataColumns(XElement dataBand, decimal[] columnPositions)
        {
            var fixedCount = 0;
            
            foreach (var textObject in dataBand.Elements("TextObject"))
            {
                var name = textObject.Attribute("Name")?.Value;
                if (name == null) continue;
                
                // 例: D_R1_C3, M_R10_C7, Y_R2_C5 など
                var columnMatch = System.Text.RegularExpressions.Regex.Match(name, @"_C(\d+)$");
                if (!columnMatch.Success) continue;
                
                if (int.TryParse(columnMatch.Groups[1].Value, out int columnNumber) &&
                    columnNumber >= 1 && columnNumber <= columnPositions.Length)
                {
                    var currentLeft = ToDecimal(textObject.Attribute("Left"));
                    var expectedLeft = columnPositions[columnNumber - 1];
                    
                    if (Math.Abs(currentLeft - expectedLeft) > 0.1m)
                    {
                        textObject.SetAttributeValue("Left", expectedLeft.ToString(CultureInfo.InvariantCulture));
                        fixedCount++;
                    }
                    
                    // CanGrow を false に設定（文字列の伸縮防止）
                    textObject.SetAttributeValue("CanGrow", "false");
                }
            }
            
            return fixedCount;
        }
    }
}