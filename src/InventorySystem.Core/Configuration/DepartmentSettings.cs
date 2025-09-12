using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Hosting;

namespace InventorySystem.Core.Configuration
{
    public class DepartmentSettings
    {
        public string BasePath { get; set; } = "./data/InventoryImport";
        public string DefaultDepartment { get; set; } = "DeptA";
        public List<DepartmentConfig> Departments { get; set; } = new();
        
        public DepartmentConfig GetActiveDepartment()
        {
            return Departments.FirstOrDefault(d => d.Code == DefaultDepartment && d.IsActive)
                ?? throw new InvalidOperationException("アクティブな部門が設定されていません");
        }
        
        public DepartmentConfig GetDepartment(string code)
        {
            return Departments.FirstOrDefault(d => d.Code == code)
                ?? throw new InvalidOperationException($"部門 {code} が見つかりません");
        }
        
        public List<DepartmentConfig> GetActiveDepartments()
        {
            return Departments.Where(d => d.IsActive).ToList();
        }
    }

    public class DepartmentConfig
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        
        // 環境に応じたパスを取得
        public string GetDepartmentPath(string basePath, IHostEnvironment environment)
        {
            if (environment.IsDevelopment())
            {
                // 開発環境：プロジェクトルートからの相対パス
                return Path.Combine(Directory.GetCurrentDirectory(), basePath, Code);
            }
            // 本番環境：絶対パス
            return Path.Combine(basePath, Code);
        }
        
        public string GetImportPath(string basePath, IHostEnvironment environment) 
            => Path.Combine(GetDepartmentPath(basePath, environment), "Import");
            
        public string GetProcessedPath(string basePath, IHostEnvironment environment) 
            => Path.Combine(GetDepartmentPath(basePath, environment), "Processed");
            
        public string GetErrorPath(string basePath, IHostEnvironment environment) 
            => Path.Combine(GetDepartmentPath(basePath, environment), "Error");
    }
}