using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Services;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// CP在庫マスタ作成サービス（在庫マスタの単純コピー）
    /// </summary>
    public class CpInventoryCreationService : ICpInventoryCreationService
    {
        private readonly ICpInventoryRepository _cpInventoryRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly ISalesVoucherRepository _salesVoucherRepository;
        private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
        private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
        private readonly ILogger<CpInventoryCreationService> _logger;

        public CpInventoryCreationService(
            ICpInventoryRepository cpInventoryRepository,
            IInventoryRepository inventoryRepository,
            ISalesVoucherRepository salesVoucherRepository,
            IPurchaseVoucherRepository purchaseVoucherRepository,
            IInventoryAdjustmentRepository inventoryAdjustmentRepository,
            ILogger<CpInventoryCreationService> logger)
        {
            _cpInventoryRepository = cpInventoryRepository;
            _inventoryRepository = inventoryRepository;
            _salesVoucherRepository = salesVoucherRepository;
            _purchaseVoucherRepository = purchaseVoucherRepository;
            _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
            _logger = logger;
        }

        public async Task<CpInventoryCreationResult> CreateCpInventoryFromCarryoverAsync(DateTime jobDate)
        {
            var result = new CpInventoryCreationResult
            {
                JobDate = jobDate,
                DataSetId = "DISPOSABLE_TABLE" // 仮テーブル設計のため固定値
            };

            try
            {
                _logger.LogInformation("CP在庫マスタ作成開始（Carryoverソース）: JobDate={JobDate} (仮テーブル設計)", jobDate);

                // CP在庫マスタの削除を保留（日次終了処理まで保持）
                // Phase 1改修: 削除タイミングを日次終了処理後に変更
                _logger.LogInformation("CP在庫マスタは仮テーブル設計で管理します");
                result.DeletedCount = 0; // 削除はスキップ
                
                /*
                // 1. 既存CP在庫マスタの削除
                result.DeletedCount = await _cpInventoryRepository.DeleteAllAsync() // 仮テーブル設計：全削除;
                _logger.LogInformation("既存CP在庫マスタ削除: {Count}件", result.DeletedCount);
                */

                // 2. Carryoverからのコピー
                result.CopiedCount = await _cpInventoryRepository.CreateCpInventoryFromCarryoverAsync(jobDate);
                _logger.LogInformation("Carryoverからコピー: {Count}件", result.CopiedCount);

                result.Success = true;

                // 3. 在庫マスタ不足の警告チェック
                var missingResult = await DetectMissingProductsAsync(jobDate);
                if (missingResult.HasWarnings)
                {
                    result.Warnings.Add($"売上伝票の商品種類（{missingResult.SalesProductCount}件）が在庫マスタ（{missingResult.InventoryMasterCount}件）より多いです");
                    result.Warnings.Add($"仕入伝票の商品種類（{missingResult.PurchaseProductCount}件）が在庫マスタ（{missingResult.InventoryMasterCount}件）より多いです");
                    result.Warnings.Add($"在庫調整の商品種類（{missingResult.AdjustmentProductCount}件）が在庫マスタ（{missingResult.InventoryMasterCount}件）より多いです");
                    result.Warnings.Add($"合計{missingResult.MissingProducts.Count}件の商品が在庫マスタに未登録です");

                    _logger.LogWarning("在庫マスタ不足警告: 未登録商品{Count}件", missingResult.MissingProducts.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "CP在庫マスタ作成エラー");
                return result;
            }
        }

        public async Task<MissingProductsResult> DetectMissingProductsAsync(DateTime jobDate)
        {
            var result = new MissingProductsResult();

            try
            {
                // 在庫マスタの商品を取得
                var inventoryMasters = await _inventoryRepository.GetByJobDateAsync(jobDate);
                var inventoryProducts = inventoryMasters.Select(im => new
                {
                    im.Key.ProductCode,
                    im.Key.GradeCode,
                    im.Key.ClassCode,
                    im.Key.ShippingMarkCode,
                    im.Key.ManualShippingMark
                }).ToHashSet();

                result.InventoryMasterCount = inventoryProducts.Count;

                // 売上伝票の商品種類を取得
                var salesVouchers = await _salesVoucherRepository.GetByJobDateAsync(jobDate);
                var salesProducts = salesVouchers
                    .Where(s => (s.VoucherType == "51" || s.VoucherType == "52") &&
                               (s.DetailType == "1" || s.DetailType == "2" || s.DetailType == "3" || s.DetailType == "4") &&
                               s.Quantity != 0)
                    .Select(s => new
                    {
                        s.ProductCode,
                        s.GradeCode,
                        s.ClassCode,
                        s.ShippingMarkCode,
                        s.ManualShippingMark
                    })
                    .Distinct()
                    .ToList();

                result.SalesProductCount = salesProducts.Count;

                // 仕入伝票の商品種類を取得
                var purchaseVouchers = await _purchaseVoucherRepository.GetByJobDateAsync(jobDate);
                var purchaseProducts = purchaseVouchers
                    .Where(p => (p.VoucherType == "11" || p.VoucherType == "12") &&
                               (p.DetailType == "1" || p.DetailType == "2" || p.DetailType == "3" || p.DetailType == "4") &&
                               p.Quantity != 0)
                    .Select(p => new
                    {
                        p.ProductCode,
                        p.GradeCode,
                        p.ClassCode,
                        p.ShippingMarkCode,
                        p.ManualShippingMark
                    })
                    .Distinct()
                    .ToList();

                result.PurchaseProductCount = purchaseProducts.Count;

                // 在庫調整の商品種類を取得
                var adjustmentVouchers = await _inventoryAdjustmentRepository.GetByJobDateAsync(jobDate);
                var adjustmentProducts = adjustmentVouchers
                    .Where(a => (a.VoucherType == "71" || a.VoucherType == "72") &&
                               a.DetailType == "1" &&
                               a.Quantity != 0)
                    .Select(a => new
                    {
                        a.ProductCode,
                        a.GradeCode,
                        a.ClassCode,
                        a.ShippingMarkCode,
                        a.ManualShippingMark
                    })
                    .Distinct()
                    .ToList();

                result.AdjustmentProductCount = adjustmentProducts.Count;

                // 在庫マスタに存在しない商品を検出
                var missingProducts = new List<MissingProduct>();

                // 売上伝票の未登録商品
                foreach (var salesProduct in salesProducts)
                {
                    if (!inventoryProducts.Contains(salesProduct))
                    {
                        missingProducts.Add(new MissingProduct
                        {
                            ProductCode = salesProduct.ProductCode,
                            GradeCode = salesProduct.GradeCode,
                            ClassCode = salesProduct.ClassCode,
                            ShippingMarkCode = salesProduct.ShippingMarkCode,
                            ManualShippingMark = salesProduct.ManualShippingMark,
                            FoundInVoucherType = "売上伝票",
                            VoucherCount = 1
                        });
                    }
                }

                // 仕入伝票の未登録商品
                foreach (var purchaseProduct in purchaseProducts)
                {
                    if (!inventoryProducts.Contains(purchaseProduct))
                    {
                        var existing = missingProducts.FirstOrDefault(mp =>
                            mp.ProductCode == purchaseProduct.ProductCode &&
                            mp.GradeCode == purchaseProduct.GradeCode &&
                            mp.ClassCode == purchaseProduct.ClassCode &&
                            mp.ShippingMarkCode == purchaseProduct.ShippingMarkCode &&
                            mp.ManualShippingMark == purchaseProduct.ManualShippingMark);

                        if (existing != null)
                        {
                            existing.FoundInVoucherType += ", 仕入伝票";
                            existing.VoucherCount++;
                        }
                        else
                        {
                            missingProducts.Add(new MissingProduct
                            {
                                ProductCode = purchaseProduct.ProductCode,
                                GradeCode = purchaseProduct.GradeCode,
                                ClassCode = purchaseProduct.ClassCode,
                                ShippingMarkCode = purchaseProduct.ShippingMarkCode,
                                ManualShippingMark = purchaseProduct.ManualShippingMark,
                                FoundInVoucherType = "仕入伝票",
                                VoucherCount = 1
                            });
                        }
                    }
                }

                // 在庫調整の未登録商品
                foreach (var adjustmentProduct in adjustmentProducts)
                {
                    if (!inventoryProducts.Contains(adjustmentProduct))
                    {
                        var existing = missingProducts.FirstOrDefault(mp =>
                            mp.ProductCode == adjustmentProduct.ProductCode &&
                            mp.GradeCode == adjustmentProduct.GradeCode &&
                            mp.ClassCode == adjustmentProduct.ClassCode &&
                            mp.ShippingMarkCode == adjustmentProduct.ShippingMarkCode &&
                            mp.ManualShippingMark == adjustmentProduct.ManualShippingMark);

                        if (existing != null)
                        {
                            existing.FoundInVoucherType += ", 在庫調整";
                            existing.VoucherCount++;
                        }
                        else
                        {
                            missingProducts.Add(new MissingProduct
                            {
                                ProductCode = adjustmentProduct.ProductCode,
                                GradeCode = adjustmentProduct.GradeCode,
                                ClassCode = adjustmentProduct.ClassCode,
                                ShippingMarkCode = adjustmentProduct.ShippingMarkCode,
                                ManualShippingMark = adjustmentProduct.ManualShippingMark,
                                FoundInVoucherType = "在庫調整",
                                VoucherCount = 1
                            });
                        }
                    }
                }

                result.MissingProducts = missingProducts;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "未登録商品検出エラー");
                throw;
            }
        }
    }
}
