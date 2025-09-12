namespace InventorySystem.Reports.Models
{
    /// <summary>
    /// 営業日報の1ページ分のデータコンテナ
    /// </summary>
    public class BusinessDailyReportPage
    {
        /// <summary>
        /// ページ番号（1-4）
        /// </summary>
        public int PageNumber { get; set; }
        
        /// <summary>
        /// ページタイトル（"営業日報（１）"など）
        /// </summary>
        public string PageTitle { get; set; } = string.Empty;
        
        /// <summary>
        /// 行データのリスト
        /// </summary>
        public List<BusinessDailyReportRow> Rows { get; set; } = new();
        
        /// <summary>
        /// 得意先分類名（8個）
        /// </summary>
        public List<string> CustomerClassNames { get; set; } = new();
        
        /// <summary>
        /// 仕入先分類名（8個）
        /// </summary>
        public List<string> SupplierClassNames { get; set; } = new();
    }

    /// <summary>
    /// 営業日報の1行分のデータ（全分類の値を含む）
    /// </summary>
    public class BusinessDailyReportRow
    {
        /// <summary>
        /// セクション名（"【日計】", "【月計】", "【年計】"）
        /// </summary>
        public string SectionName { get; set; } = string.Empty;
        
        /// <summary>
        /// 項目名（"現金売上"など）
        /// </summary>
        public string ItemName { get; set; } = string.Empty;
        
        /// <summary>
        /// 合計列の値
        /// </summary>
        public string Total { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類01の値
        /// </summary>
        public string Class01 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類02の値
        /// </summary>
        public string Class02 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類03の値
        /// </summary>
        public string Class03 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類04の値
        /// </summary>
        public string Class04 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類05の値
        /// </summary>
        public string Class05 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類06の値
        /// </summary>
        public string Class06 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類07の値
        /// </summary>
        public string Class07 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類08の値
        /// </summary>
        public string Class08 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類09の値
        /// </summary>
        public string Class09 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類10の値
        /// </summary>
        public string Class10 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類11の値
        /// </summary>
        public string Class11 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類12の値
        /// </summary>
        public string Class12 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類13の値
        /// </summary>
        public string Class13 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類14の値
        /// </summary>
        public string Class14 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類15の値
        /// </summary>
        public string Class15 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類16の値
        /// </summary>
        public string Class16 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類17の値
        /// </summary>
        public string Class17 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類18の値
        /// </summary>
        public string Class18 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類19の値
        /// </summary>
        public string Class19 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類20の値
        /// </summary>
        public string Class20 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類21の値
        /// </summary>
        public string Class21 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類22の値
        /// </summary>
        public string Class22 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類23の値
        /// </summary>
        public string Class23 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類24の値
        /// </summary>
        public string Class24 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類25の値
        /// </summary>
        public string Class25 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類26の値
        /// </summary>
        public string Class26 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類27の値
        /// </summary>
        public string Class27 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類28の値
        /// </summary>
        public string Class28 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類29の値
        /// </summary>
        public string Class29 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類30の値
        /// </summary>
        public string Class30 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類31の値
        /// </summary>
        public string Class31 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類32の値
        /// </summary>
        public string Class32 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類33の値
        /// </summary>
        public string Class33 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類34の値
        /// </summary>
        public string Class34 { get; set; } = string.Empty;
        
        /// <summary>
        /// 分類35の値
        /// </summary>
        public string Class35 { get; set; } = string.Empty;
        
        /// <summary>
        /// 合計行フラグ（＊売上計＊などの合計行かどうか）
        /// </summary>
        public bool IsSummaryRow { get; set; }
    }
}