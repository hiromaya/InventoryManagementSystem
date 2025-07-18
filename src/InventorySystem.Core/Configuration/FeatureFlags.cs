namespace InventorySystem.Core.Configuration
{
    /// <summary>
    /// フィーチャーフラグの設定
    /// </summary>
    public class FeatureFlags
    {
        /// <summary>
        /// DataSetManagementテーブルのみを使用するかどうか
        /// </summary>
        public bool UseDataSetManagementOnly { get; set; }
        
        /// <summary>
        /// DataSets移行ログを有効にするかどうか
        /// </summary>
        public bool EnableDataSetsMigrationLog { get; set; }
    }
}