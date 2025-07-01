using System;
using System.Collections.Generic;

namespace InventorySystem.Core.Models
{
    /// <summary>
    /// CP在庫マスタ作成結果
    /// </summary>
    public class CpInventoryCreationResult
    {
        /// <summary>
        /// 処理が成功したかどうか
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 処理日付
        /// </summary>
        public DateTime JobDate { get; set; }

        /// <summary>
        /// データセットID
        /// </summary>
        public string DataSetId { get; set; } = string.Empty;

        /// <summary>
        /// 削除された既存レコード数
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        /// 在庫マスタからコピーされたレコード数
        /// </summary>
        public int CopiedCount { get; set; }

        /// <summary>
        /// エラーメッセージ
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 警告メッセージのリスト
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// 在庫マスタ未登録商品の検出結果
    /// </summary>
    public class MissingProductsResult
    {
        /// <summary>
        /// 在庫マスタの商品数
        /// </summary>
        public int InventoryMasterCount { get; set; }

        /// <summary>
        /// 売上伝票の商品種類数
        /// </summary>
        public int SalesProductCount { get; set; }

        /// <summary>
        /// 仕入伝票の商品種類数
        /// </summary>
        public int PurchaseProductCount { get; set; }

        /// <summary>
        /// 在庫調整の商品種類数
        /// </summary>
        public int AdjustmentProductCount { get; set; }

        /// <summary>
        /// 未登録商品のリスト
        /// </summary>
        public List<MissingProduct> MissingProducts { get; set; } = new();

        /// <summary>
        /// 警告が必要かどうか
        /// </summary>
        public bool HasWarnings => MissingProducts.Any();
    }

    /// <summary>
    /// 未登録商品情報
    /// </summary>
    public class MissingProduct
    {
        /// <summary>
        /// 商品コード
        /// </summary>
        public string ProductCode { get; set; } = string.Empty;

        /// <summary>
        /// 等級コード
        /// </summary>
        public string GradeCode { get; set; } = string.Empty;

        /// <summary>
        /// 階級コード
        /// </summary>
        public string ClassCode { get; set; } = string.Empty;

        /// <summary>
        /// 荷印コード
        /// </summary>
        public string ShippingMarkCode { get; set; } = string.Empty;

        /// <summary>
        /// 荷印名
        /// </summary>
        public string ShippingMarkName { get; set; } = string.Empty;

        /// <summary>
        /// どの伝票種別で見つかったか
        /// </summary>
        public string FoundInVoucherType { get; set; } = string.Empty;

        /// <summary>
        /// 伝票での件数
        /// </summary>
        public int VoucherCount { get; set; }
    }
}