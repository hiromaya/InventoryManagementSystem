using System;

namespace InventorySystem.Core.Entities
{
    /// <summary>
    /// UN在庫マスタ（アンマッチチェック専用・使い捨てテーブル設計）
    /// 数量のみを管理し、単価・金額は含まない
    /// DataSetId管理を廃止し、TRUNCATEによる高速リセットを採用
    /// </summary>
    public class UnInventoryMaster
    {
        /// <summary>
        /// 5項目複合キー
        /// </summary>
        public InventoryKey Key { get; set; } = new InventoryKey();

        /// <summary>
        /// 商品コード（5項目キーの一部）
        /// </summary>
        public string ProductCode
        {
            get => Key.ProductCode;
            set => Key.ProductCode = value;
        }

        /// <summary>
        /// 等級コード（5項目キーの一部）
        /// </summary>
        public string GradeCode
        {
            get => Key.GradeCode;
            set => Key.GradeCode = value;
        }

        /// <summary>
        /// 階級コード（5項目キーの一部）
        /// </summary>
        public string ClassCode
        {
            get => Key.ClassCode;
            set => Key.ClassCode = value;
        }

        /// <summary>
        /// 荷印コード（5項目キーの一部）
        /// </summary>
        public string ShippingMarkCode
        {
            get => Key.ShippingMarkCode;
            set => Key.ShippingMarkCode = value;
        }

        /// <summary>
        /// 荷印名（5項目キーの一部）
        /// </summary>
        public string ManualShippingMark
        {
            get => Key.ManualShippingMark;
            set => Key.ManualShippingMark = value;
        }

        /// <summary>
        /// 荷印名（荷印マスタから取得した表示用名称）
        /// </summary>
        public string ShippingMarkName { get; set; } = string.Empty;

        // DataSetIdプロパティを削除（使い捨てテーブル設計によりDataSetId管理を廃止）

        /// <summary>
        /// 前日在庫数量
        /// </summary>
        public decimal PreviousDayStock { get; set; }

        /// <summary>
        /// 当日在庫数量
        /// </summary>
        public decimal DailyStock { get; set; }

        /// <summary>
        /// 当日発生フラグ（'9':未処理、'0':処理済み）
        /// </summary>
        public char DailyFlag { get; set; } = '9';

        /// <summary>
        /// ジョブ日付
        /// </summary>
        public DateTime? JobDate { get; set; }

        /// <summary>
        /// 作成日時
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新日時
        /// </summary>
        public DateTime UpdatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 5項目複合キーの文字列表現
        /// </summary>
        public string CompositeKey => Key.ToString();

        /// <summary>
        /// デバッグ用文字列表現
        /// </summary>
        public override string ToString()
        {
            return $"UN在庫: {CompositeKey} - 前日:{PreviousDayStock}, 当日:{DailyStock}, フラグ:{DailyFlag}";
        }
    }
}