#!/usr/bin/env python3
import pyodbc
import datetime

# Database connection parameters
server = 'localhost'
database = 'InventoryManagementDB'
username = 'sa'
password = 'P@ssw0rd123'

# Create connection string
conn_str = f'DRIVER={{ODBC Driver 18 for SQL Server}};SERVER={server};DATABASE={database};UID={username};PWD={password};TrustServerCertificate=yes'

try:
    # Connect to the database
    conn = pyodbc.connect(conn_str)
    cursor = conn.cursor()

    print("========== InventoryMaster データ分析開始 ==========")
    print()

    # 1. 全レコード数の確認
    print("1. 全レコード数")
    cursor.execute("SELECT COUNT(*) as TotalRecords FROM InventoryMaster")
    total_records = cursor.fetchone()[0]
    print(f"   総レコード数: {total_records}")
    print()

    # 2. JobDate別のレコード数（上位10件）
    print("2. JobDate別のレコード数（上位10件）")
    cursor.execute("""
        SELECT TOP 10
            JobDate,
            COUNT(*) as RecordCount
        FROM InventoryMaster
        GROUP BY JobDate
        ORDER BY JobDate DESC
    """)
    for row in cursor.fetchall():
        print(f"   JobDate: {row.JobDate}, レコード数: {row.RecordCount}")
    print()

    # 3. 5項目キーで見た場合の重複状況
    print("3. 5項目キーで見た場合の重複状況")
    cursor.execute("""
        WITH DuplicateKeys AS (
            SELECT 
                ProductCode, 
                GradeCode, 
                ClassCode, 
                ShippingMarkCode, 
                ShippingMarkName,
                COUNT(DISTINCT JobDate) as JobDateCount,
                COUNT(*) as RecordCount
            FROM InventoryMaster
            GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            HAVING COUNT(*) > 1
        )
        SELECT 
            COUNT(*) as DuplicateKeyCount,
            SUM(RecordCount) as TotalDuplicateRecords,
            MAX(JobDateCount) as MaxJobDatesPerKey,
            AVG(CAST(JobDateCount as FLOAT)) as AvgJobDatesPerKey
        FROM DuplicateKeys
    """)
    
    row = cursor.fetchone()
    if row:
        print(f"   重複キー数: {row.DuplicateKeyCount or 0}")
        print(f"   重複レコード総数: {row.TotalDuplicateRecords or 0}")
        print(f"   最大JobDate数/キー: {row.MaxJobDatesPerKey or 0}")
        print(f"   平均JobDate数/キー: {row.AvgJobDatesPerKey or 0:.2f}")
    print()

    # 4. 重複キーの詳細（上位20件）
    print("4. 重複キーの詳細（上位10件）")
    cursor.execute("""
        SELECT TOP 10
            ProductCode, 
            GradeCode, 
            ClassCode, 
            ShippingMarkCode, 
            LEFT(ShippingMarkName, 20) as ShippingMarkName_Short,
            COUNT(DISTINCT JobDate) as JobDateCount,
            MIN(JobDate) as MinJobDate,
            MAX(JobDate) as MaxJobDate,
            COUNT(*) as RecordCount
        FROM InventoryMaster
        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
        HAVING COUNT(*) > 1
        ORDER BY COUNT(*) DESC, ProductCode
    """)
    
    for row in cursor.fetchall():
        print(f"   商品: {row.ProductCode}, 等級: {row.GradeCode}, 階級: {row.ClassCode}, 荷印: {row.ShippingMarkCode}")
        print(f"       JobDate数: {row.JobDateCount}, 期間: {row.MinJobDate} ～ {row.MaxJobDate}")
    print()

    # 5. 削減見込み
    print("5. 削減見込み")
    cursor.execute("""
        SELECT COUNT(DISTINCT ProductCode + '|' + GradeCode + '|' + ClassCode + '|' + 
                            ShippingMarkCode + '|' + ShippingMarkName) as UniqueKeys
        FROM InventoryMaster
    """)
    unique_keys = cursor.fetchone()[0]
    
    reduction_count = total_records - unique_keys
    reduction_rate = (reduction_count / total_records * 100) if total_records > 0 else 0
    
    print(f"   総レコード数: {total_records}")
    print(f"   ユニークキー数: {unique_keys}")
    print(f"   削減されるレコード数: {reduction_count}")
    print(f"   削減率: {reduction_rate:.1f}%")
    print()

    # 6. 最新JobDateの分布
    print("6. 各5項目キーの最新JobDate分布（上位10件）")
    cursor.execute("""
        WITH LatestJobDates AS (
            SELECT 
                ProductCode, 
                GradeCode, 
                ClassCode, 
                ShippingMarkCode, 
                ShippingMarkName,
                MAX(JobDate) as LatestJobDate
            FROM InventoryMaster
            GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
        )
        SELECT TOP 10
            LatestJobDate,
            COUNT(*) as KeyCount
        FROM LatestJobDates
        GROUP BY LatestJobDate
        ORDER BY LatestJobDate DESC
    """)
    
    for row in cursor.fetchall():
        print(f"   JobDate: {row.LatestJobDate}, キー数: {row.KeyCount}")
    print()

    print("========== 分析完了 ==========")
    print()
    print("【重要】この分析結果を基に、以下を検討してください：")
    print("1. 履歴データの保存が必要かどうか")
    print("2. 最新データのみで業務に影響がないか")
    print("3. 削減されるデータ量が許容範囲内か")

except pyodbc.Error as e:
    print(f"データベースエラー: {e}")
finally:
    if 'conn' in locals():
        conn.close()