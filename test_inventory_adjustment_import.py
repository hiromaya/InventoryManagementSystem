#!/usr/bin/env python3
import csv
import json

# Test inventory adjustment import
csv_path = "data/InventoryImport/DeptA/Import/受注伝票.csv"

print("Testing inventory adjustment CSV import...")
print("=" * 80)

# Read and analyze the CSV
with open(csv_path, 'r', encoding='utf-8-sig') as f:
    reader = csv.DictReader(f)
    rows = list(reader)
    
print(f"Total rows: {len(rows)}")
print()

# Check a few sample rows
for i, row in enumerate(rows[:5]):
    print(f"Row {i+1}:")
    print(f"  伝票区分: {row['伝票区分(71:在庫調整)']}")
    print(f"  商品コード: {row['商品コード']}")
    print(f"  等級コード: {row['等級コード']}")
    print(f"  階級コード: {row['階級コード']}")
    print(f"  荷印コード: {row['荷印コード']}")
    print(f"  等級名: {row['等級名']}")
    print(f"  階級名: {row['階級名']}")
    print(f"  荷印名: {row['荷印名']}")
    print(f"  数量: {row['数量']}")
    print(f"  区分: {row['区分(1:ﾛｽ,4:振替,6:調整)']}")
    print()

# Analyze validation patterns
print("Validation Analysis:")
print("-" * 40)

# Count by voucher type
voucher_types = {}
for row in rows:
    vtype = row['伝票区分(71:在庫調整)']
    voucher_types[vtype] = voucher_types.get(vtype, 0) + 1

print("Voucher types:")
for vtype, count in voucher_types.items():
    print(f"  {vtype}: {count} rows")
print()

# Check for CategoryCode "00" 
category_codes = set()
for row in rows:
    if '区分コード' in row:
        category_codes.add(row['区分コード'])
        
print(f"Category codes found: {sorted(category_codes)}")
print()

# Check code patterns
grade_codes = set()
class_codes = set()
shipping_mark_codes = set()

for row in rows:
    grade_codes.add(row['等級コード'])
    class_codes.add(row['階級コード'])
    shipping_mark_codes.add(row['荷印コード'])

print(f"Grade codes: {sorted(grade_codes)}")
print(f"Class codes: {sorted(class_codes)}")
print(f"Shipping mark codes: {sorted(shipping_mark_codes)}")
print()

# Check name patterns
print("Sample name values:")
for i, row in enumerate(rows[:10]):
    if row['等級名'] or row['階級名'] or row['荷印名']:
        print(f"Row {i+1}: 等級名='{row['等級名']}', 階級名='{row['階級名']}', 荷印名='{row['荷印名']}'")