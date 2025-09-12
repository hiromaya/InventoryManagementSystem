using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;

namespace InventorySystem.Data.Services
{
    /// <summary>
    /// CP在庫マスタの検証・校正サービス
    /// </summary>
    public class CpInventoryValidationService : ICpInventoryValidationService
    {
        private readonly string _connectionString;
        private readonly ILogger<CpInventoryValidationService> _logger;

        private const decimal AmountTolerance = 0.01m;
        private const decimal UnitPriceTolerance = 0.01m;

        public CpInventoryValidationService(
            string connectionString,
            ILogger<CpInventoryValidationService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<CpInventoryValidationResult> ValidateAsync(DateTime jobDate, string? departmentCode = null)
        {
            var result = new CpInventoryValidationResult
            {
                JobDate = jobDate,
                ValidatedAt = DateTime.Now
            };

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // 総レコード数
            result.TotalRecords = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM CpInventoryMaster WHERE JobDate = @JobDate",
                new { JobDate = jobDate });

            // 1) 単価同値（在庫単価と売上単価が同一）
            var samePriceSql = @"
                SELECT DISTINCT cp.ProductCode, cp.GradeCode, cp.ClassCode, cp.ShippingMarkCode, cp.ManualShippingMark,
                       cp.DailyUnitPrice, sv.UnitPrice AS SalesUnitPrice
                FROM CpInventoryMaster cp
                INNER JOIN SalesVouchers sv ON cp.ProductCode = sv.ProductCode
                    AND cp.GradeCode = sv.GradeCode
                    AND cp.ClassCode = sv.ClassCode
                    AND cp.ShippingMarkCode = sv.ShippingMarkCode
                    AND cp.ManualShippingMark = sv.ManualShippingMark
                    AND sv.JobDate = @JobDate
                    AND sv.VoucherType IN ('51','52')
                    AND sv.DetailType IN ('1','2','3')
                WHERE cp.JobDate = @JobDate
                  AND ABS(cp.DailyUnitPrice - sv.UnitPrice) < @UnitPriceTolerance";

            var samePriceRows = await connection.QueryAsync(samePriceSql, new { JobDate = jobDate, UnitPriceTolerance });
            foreach (var r in samePriceRows)
            {
                AddIssue(result, new ValidationIssue
                {
                    IssueType = "単価同値",
                    Severity = "Error",
                    ProductCode = r.ProductCode,
                    GradeCode = r.GradeCode,
                    ClassCode = r.ClassCode,
                    ShippingMarkCode = r.ShippingMarkCode,
                    ManualShippingMark = r.ManualShippingMark,
                    ExpectedValue = (decimal?)r.DailyUnitPrice,
                    ActualValue = (decimal?)r.SalesUnitPrice,
                    Difference = Math.Abs(((decimal)r.DailyUnitPrice) - ((decimal)r.SalesUnitPrice)),
                    Description = "在庫単価が売上単価と同一です"
                });
            }

            // 2) 在庫金額の整合（在庫金額 ≈ 数量×単価）
            var amountMismatchSql = @"
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                       DailyStock, DailyUnitPrice, DailyStockAmount,
                       ROUND(DailyStock * DailyUnitPrice, 4) AS ExpectedAmount
                FROM CpInventoryMaster
                WHERE JobDate = @JobDate
                  AND ABS(DailyStockAmount - ROUND(DailyStock * DailyUnitPrice, 4)) > @AmountTolerance
                  AND DailyStock <> 0";

            var amountMismatchRows = await connection.QueryAsync(amountMismatchSql, new { JobDate = jobDate, AmountTolerance });
            foreach (var r in amountMismatchRows)
            {
                AddIssue(result, new ValidationIssue
                {
                    IssueType = "在庫金額不整合",
                    Severity = "Error",
                    ProductCode = r.ProductCode,
                    GradeCode = r.GradeCode,
                    ClassCode = r.ClassCode,
                    ShippingMarkCode = r.ShippingMarkCode,
                    ManualShippingMark = r.ManualShippingMark,
                    ExpectedValue = (decimal?)r.ExpectedAmount,
                    ActualValue = (decimal?)r.DailyStockAmount,
                    Difference = Math.Abs(((decimal)r.DailyStockAmount) - ((decimal)r.ExpectedAmount)),
                    Description = "在庫金額が数量×単価と一致しません"
                });
            }

            // 2-b) ゼロ不整合（数量0で金額残、数量ありで金額0）
            var zeroAnomalySql = @"
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                       DailyStock, DailyUnitPrice, DailyStockAmount
                FROM CpInventoryMaster
                WHERE JobDate = @JobDate AND (
                    (DailyStock = 0 AND DailyStockAmount <> 0) OR
                    (DailyStock <> 0 AND DailyStockAmount = 0)
                )";

            var zeroAnomalyRows = await connection.QueryAsync(zeroAnomalySql, new { JobDate = jobDate });
            foreach (var r in zeroAnomalyRows)
            {
                AddIssue(result, new ValidationIssue
                {
                    IssueType = "在庫金額ゼロ不整合",
                    Severity = "Error",
                    ProductCode = r.ProductCode,
                    GradeCode = r.GradeCode,
                    ClassCode = r.ClassCode,
                    ShippingMarkCode = r.ShippingMarkCode,
                    ManualShippingMark = r.ManualShippingMark,
                    ExpectedValue = (decimal?)((decimal)r.DailyStock * (decimal)r.DailyUnitPrice),
                    ActualValue = (decimal?)r.DailyStockAmount,
                    Difference = null,
                    Description = "数量と金額のゼロ整合に問題があります"
                });
            }

            // 3) 明細・集計突合（空集計行）
            var emptySummarySql = @"
                WITH DetailCounts AS (
                    SELECT cp.ProductCode, cp.GradeCode, cp.ClassCode, cp.ShippingMarkCode, cp.ManualShippingMark,
                           ISNULL(s.cnt,0) + ISNULL(p.cnt,0) + ISNULL(a.cnt,0) AS DetailCount
                    FROM CpInventoryMaster cp
                    LEFT JOIN (
                        SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, COUNT(*) cnt
                        FROM SalesVouchers
                        WHERE JobDate = @JobDate AND VoucherType IN ('51','52') AND DetailType IN ('1','2','3')
                        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                    ) s ON cp.ProductCode=s.ProductCode AND cp.GradeCode=s.GradeCode AND cp.ClassCode=s.ClassCode AND cp.ShippingMarkCode=s.ShippingMarkCode AND cp.ManualShippingMark=s.ManualShippingMark
                    LEFT JOIN (
                        SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, COUNT(*) cnt
                        FROM PurchaseVouchers
                        WHERE JobDate = @JobDate AND VoucherType IN ('11','12') AND DetailType IN ('1','2')
                        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                    ) p ON cp.ProductCode=p.ProductCode AND cp.GradeCode=p.GradeCode AND cp.ClassCode=p.ClassCode AND cp.ShippingMarkCode=p.ShippingMarkCode AND cp.ManualShippingMark=p.ManualShippingMark
                    LEFT JOIN (
                        SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, COUNT(*) cnt
                        FROM InventoryAdjustments
                        WHERE JobDate = @JobDate AND VoucherType IN ('71','72') AND DetailType='1'
                        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                    ) a ON cp.ProductCode=a.ProductCode AND cp.GradeCode=a.GradeCode AND cp.ClassCode=a.ClassCode AND cp.ShippingMarkCode=a.ShippingMarkCode AND cp.ManualShippingMark=a.ManualShippingMark
                    WHERE cp.JobDate = @JobDate
                )
                SELECT cp.ProductCode, cp.GradeCode, cp.ClassCode, cp.ShippingMarkCode, cp.ManualShippingMark,
                       dc.DetailCount,
                       (cp.DailySalesQuantity + cp.DailyPurchaseQuantity + cp.DailyInventoryAdjustmentQuantity + cp.DailyProcessingQuantity + cp.DailyTransferQuantity) AS DailyMovements
                FROM CpInventoryMaster cp
                INNER JOIN DetailCounts dc ON cp.ProductCode=dc.ProductCode AND cp.GradeCode=dc.GradeCode AND cp.ClassCode=dc.ClassCode AND cp.ShippingMarkCode=dc.ShippingMarkCode AND cp.ManualShippingMark=dc.ManualShippingMark
                WHERE cp.JobDate=@JobDate
                  AND dc.DetailCount = 0
                  AND (cp.DailySalesQuantity <> 0 OR cp.DailyPurchaseQuantity <> 0 OR cp.DailyInventoryAdjustmentQuantity <> 0 OR cp.DailyProcessingQuantity <> 0 OR cp.DailyTransferQuantity <> 0 OR cp.DailyStockAmount <> 0)";

            var emptySummaryRows = await connection.QueryAsync(emptySummarySql, new { JobDate = jobDate });
            foreach (var r in emptySummaryRows)
            {
                AddIssue(result, new ValidationIssue
                {
                    IssueType = "空集計行",
                    Severity = "Error",
                    ProductCode = r.ProductCode,
                    GradeCode = r.GradeCode,
                    ClassCode = r.ClassCode,
                    ShippingMarkCode = r.ShippingMarkCode,
                    ManualShippingMark = r.ManualShippingMark,
                    Description = "明細0件にもかかわらず集計値が存在します（削除対象）"
                });
            }

            // 4) 異常値検出（マイナス在庫、粗利率）
            var negativeStockSql = @"
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, DailyStock
                FROM CpInventoryMaster
                WHERE JobDate = @JobDate AND DailyStock < 0";
            var negativeRows = await connection.QueryAsync(negativeStockSql, new { JobDate = jobDate });
            foreach (var r in negativeRows)
            {
                AddIssue(result, new ValidationIssue
                {
                    IssueType = "マイナス在庫",
                    Severity = "Error",
                    ProductCode = r.ProductCode,
                    GradeCode = r.GradeCode,
                    ClassCode = r.ClassCode,
                    ShippingMarkCode = r.ShippingMarkCode,
                    ManualShippingMark = r.ManualShippingMark,
                    ActualValue = (decimal?)r.DailyStock,
                    Description = "在庫数量が負数です"
                });
            }

            var grossRateSql = @"
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                       DailyGrossProfit, DailySalesAmount,
                       CASE WHEN DailySalesAmount = 0 THEN NULL
                            ELSE ROUND((DailyGrossProfit / DailySalesAmount) * 100, 2)
                       END AS GrossRate
                FROM CpInventoryMaster
                WHERE JobDate = @JobDate";
            var rateRows = await connection.QueryAsync(grossRateSql, new { JobDate = jobDate });
            foreach (var r in rateRows)
            {
                if (r.GrossRate == null) continue;
                decimal rate = (decimal)r.GrossRate;
                if (rate < 0 || rate > 50)
                {
                    AddIssue(result, new ValidationIssue
                    {
                        IssueType = "粗利率異常",
                        Severity = "Error",
                        ProductCode = r.ProductCode,
                        GradeCode = r.GradeCode,
                        ClassCode = r.ClassCode,
                        ShippingMarkCode = r.ShippingMarkCode,
                        ManualShippingMark = r.ManualShippingMark,
                        ActualValue = rate,
                        Description = "粗利率が閾値外（<0% または >50%）"
                    });
                }
                else if (rate > 50 && rate <= 80)
                {
                    AddIssue(result, new ValidationIssue
                    {
                        IssueType = "粗利率高め",
                        Severity = "Warning",
                        ProductCode = r.ProductCode,
                        GradeCode = r.GradeCode,
                        ClassCode = r.ClassCode,
                        ShippingMarkCode = r.ShippingMarkCode,
                        ManualShippingMark = r.ManualShippingMark,
                        ActualValue = rate,
                        Description = "粗利率が50-80%の範囲にあります"
                    });
                }
            }

            // 5) 前日連続性（前日の当日在庫 = 当日の前日在庫）
            var continuitySql = @"
                SELECT cur.ProductCode, cur.GradeCode, cur.ClassCode, cur.ShippingMarkCode, cur.ManualShippingMark,
                       cur.PreviousDayStock, cur.PreviousDayStockAmount, cur.PreviousDayUnitPrice,
                       prev.DailyStock AS PrevDailyStock, prev.DailyStockAmount AS PrevDailyStockAmount, prev.DailyUnitPrice AS PrevDailyUnitPrice
                FROM CpInventoryMaster cur
                INNER JOIN CpInventoryMaster prev ON prev.JobDate = DATEADD(day, -1, cur.JobDate)
                    AND prev.ProductCode=cur.ProductCode AND prev.GradeCode=cur.GradeCode AND prev.ClassCode=cur.ClassCode
                    AND prev.ShippingMarkCode=cur.ShippingMarkCode AND prev.ManualShippingMark=cur.ManualShippingMark
                WHERE cur.JobDate = @JobDate
                  AND (cur.PreviousDayStock <> prev.DailyStock OR cur.PreviousDayStockAmount <> prev.DailyStockAmount OR ABS(cur.PreviousDayUnitPrice - prev.DailyUnitPrice) > @UnitPriceTolerance)";
            var continuityRows = await connection.QueryAsync(continuitySql, new { JobDate = jobDate, UnitPriceTolerance });
            foreach (var r in continuityRows)
            {
                AddIssue(result, new ValidationIssue
                {
                    IssueType = "前日連続性不整合",
                    Severity = "Error",
                    ProductCode = r.ProductCode,
                    GradeCode = r.GradeCode,
                    ClassCode = r.ClassCode,
                    ShippingMarkCode = r.ShippingMarkCode,
                    ManualShippingMark = r.ManualShippingMark,
                    Description = "前日の当日在庫と当日の前日在庫が一致しません"
                });
            }

            _logger.LogInformation("CP在庫検証完了: {Errors}件のError, {Warnings}件のWarning", result.ErrorCount, result.WarningCount);
            return result;
        }

        public async Task<int> ApplyCorrectionsAsync(DateTime jobDate, CpInventoryValidationResult result, SqlTransaction? transaction = null)
        {
            if (result == null || result.Issues.Count == 0) return 0;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = transaction ?? (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                int corrected = 0;

                // 1) 在庫金額不整合・ゼロ不整合 → 金額再計算
                var amountIssues = result.Issues.Where(i => i.Severity == "Error" && (i.IssueType == "在庫金額不整合" || i.IssueType == "在庫金額ゼロ不整合")).ToList();
                foreach (var g in GroupKeys(amountIssues))
                {
                    var sql = @"
                        UPDATE CpInventoryMaster
                        SET DailyStockAmount = ROUND(DailyStock * DailyUnitPrice, 4), UpdatedDate = GETDATE()
                        WHERE JobDate = @JobDate AND ProductCode=@ProductCode AND GradeCode=@GradeCode AND ClassCode=@ClassCode AND ShippingMarkCode=@ShippingMarkCode AND ManualShippingMark=@ManualShippingMark";
                    corrected += await connection.ExecuteAsync(sql, new { JobDate = jobDate, g.ProductCode, g.GradeCode, g.ClassCode, g.ShippingMarkCode, g.ManualShippingMark }, tx);
                }

                // 2) 単価同値 → 前日単価 or 移動平均再計算
                var samePriceIssues = result.Issues.Where(i => i.Severity == "Error" && i.IssueType == "単価同値").ToList();
                foreach (var g in GroupKeys(samePriceIssues))
                {
                    var sql = @"
                        UPDATE CpInventoryMaster
                        SET DailyUnitPrice = CASE WHEN PreviousDayUnitPrice > 0 THEN PreviousDayUnitPrice
                                                  WHEN (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity) = 0 THEN 0
                                                  ELSE ROUND((PreviousDayStockAmount + DailyPurchaseAmount - DailyPurchaseReturnAmount) /
                                                             NULLIF(PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity, 0), 4)
                                             END,
                            DailyStockAmount = ROUND(DailyStock * (CASE WHEN PreviousDayUnitPrice > 0 THEN PreviousDayUnitPrice
                                                  WHEN (PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity) = 0 THEN 0
                                                  ELSE ROUND((PreviousDayStockAmount + DailyPurchaseAmount - DailyPurchaseReturnAmount) /
                                                             NULLIF(PreviousDayStock + DailyPurchaseQuantity - DailyPurchaseReturnQuantity, 0), 4)
                                             END), 4),
                            UpdatedDate = GETDATE()
                        WHERE JobDate = @JobDate AND ProductCode=@ProductCode AND GradeCode=@GradeCode AND ClassCode=@ClassCode AND ShippingMarkCode=@ShippingMarkCode AND ManualShippingMark=@ManualShippingMark";
                    corrected += await connection.ExecuteAsync(sql, new { JobDate = jobDate, g.ProductCode, g.GradeCode, g.ClassCode, g.ShippingMarkCode, g.ManualShippingMark }, tx);
                }

                // 3) 前日連続性 → 前日のDaily*で埋める
                var continuityIssues = result.Issues.Where(i => i.Severity == "Error" && i.IssueType == "前日連続性不整合").ToList();
                foreach (var g in GroupKeys(continuityIssues))
                {
                    var sql = @"
                        UPDATE cur
                        SET cur.PreviousDayStock = prev.DailyStock,
                            cur.PreviousDayStockAmount = prev.DailyStockAmount,
                            cur.PreviousDayUnitPrice = prev.DailyUnitPrice,
                            cur.UpdatedDate = GETDATE()
                        FROM CpInventoryMaster cur
                        INNER JOIN CpInventoryMaster prev ON prev.JobDate = DATEADD(day, -1, cur.JobDate)
                            AND prev.ProductCode=cur.ProductCode AND prev.GradeCode=cur.GradeCode AND prev.ClassCode=cur.ClassCode
                            AND prev.ShippingMarkCode=cur.ShippingMarkCode AND prev.ManualShippingMark=cur.ManualShippingMark
                        WHERE cur.JobDate = @JobDate AND cur.ProductCode=@ProductCode AND cur.GradeCode=@GradeCode AND cur.ClassCode=@ClassCode AND cur.ShippingMarkCode=@ShippingMarkCode AND cur.ManualShippingMark=@ManualShippingMark";
                    corrected += await connection.ExecuteAsync(sql, new { JobDate = jobDate, g.ProductCode, g.GradeCode, g.ClassCode, g.ShippingMarkCode, g.ManualShippingMark }, tx);
                }

                // 4) 空集計行 → レコード削除
                var emptyIssues = result.Issues.Where(i => i.Severity == "Error" && i.IssueType == "空集計行").ToList();
                foreach (var g in GroupKeys(emptyIssues))
                {
                    var sql = @"
                        DELETE FROM CpInventoryMaster
                        WHERE JobDate = @JobDate AND ProductCode=@ProductCode AND GradeCode=@GradeCode AND ClassCode=@ClassCode AND ShippingMarkCode=@ShippingMarkCode AND ManualShippingMark=@ManualShippingMark";
                    corrected += await connection.ExecuteAsync(sql, new { JobDate = jobDate, g.ProductCode, g.GradeCode, g.ClassCode, g.ShippingMarkCode, g.ManualShippingMark }, tx);
                }

                // 粗利率異常は校正対象外（記録のみ）

                if (transaction == null)
                {
                    await tx.CommitAsync();
                }
                _logger.LogInformation("CP在庫校正完了: {Count}件修正", corrected);
                return corrected;
            }
            catch (Exception ex)
            {
                if (transaction == null)
                {
                    await tx.RollbackAsync();
                }
                _logger.LogError(ex, "CP在庫校正中にエラーが発生しました");
                throw;
            }
        }

        private static void AddIssue(CpInventoryValidationResult result, ValidationIssue issue)
        {
            result.Issues.Add(issue);
            if (issue.Severity == "Error") result.ErrorCount++;
            else if (issue.Severity == "Warning") result.WarningCount++;
        }

        private static IEnumerable<(string ProductCode, string GradeCode, string ClassCode, string ShippingMarkCode, string ManualShippingMark)> GroupKeys(IEnumerable<ValidationIssue> issues)
        {
            return issues
                .GroupBy(i => new { i.ProductCode, i.GradeCode, i.ClassCode, i.ShippingMarkCode, i.ManualShippingMark })
                .Select(g => (g.Key.ProductCode, g.Key.GradeCode, g.Key.ClassCode, g.Key.ShippingMarkCode, g.Key.ManualShippingMark));
        }
    }
}

