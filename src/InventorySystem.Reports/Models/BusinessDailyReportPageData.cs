using System.Data;

namespace InventorySystem.Reports.Models
{
    /// <summary>
    /// 営業日報の1ページ分のデータモデル
    /// </summary>
    public class BusinessDailyReportPageData
    {
        /// <summary>
        /// ページ番号（1-4）
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// 日計データ（18行固定）
        /// </summary>
        public DataTable DailyData { get; set; }

        /// <summary>
        /// 月計データ（18行固定）
        /// </summary>
        public DataTable MonthlyData { get; set; }

        /// <summary>
        /// 年計データ（4行固定）
        /// </summary>
        public DataTable YearlyData { get; set; }

        /// <summary>
        /// 列ヘッダー（得意先分類名）
        /// </summary>
        public string[] ColumnHeaders { get; set; }

        /// <summary>
        /// サブヘッダー（仕入先分類名）
        /// </summary>
        public string[] SubHeaders { get; set; }

        /// <summary>
        /// 対象日付（表示用）
        /// </summary>
        public DateTime JobDate { get; set; }

        /// <summary>
        /// ページタイトル
        /// </summary>
        public string PageTitle => $"※ {JobDate:yyyy年MM月dd日} 営業日報（{PageNumber}）※";

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public BusinessDailyReportPageData()
        {
            DailyData = new DataTable("DailyData");
            MonthlyData = new DataTable("MonthlyData");
            YearlyData = new DataTable("YearlyData");
            ColumnHeaders = new string[9];
            SubHeaders = new string[9];
        }
    }
}