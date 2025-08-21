const fs = require('fs');

const filePath = 'BusinessDailyReport_fix.frx';
const backupPath = filePath + '.backup_moveup_' + new Date().toISOString().replace(/[:.]/g, '-');

// バックアップ
const content = fs.readFileSync(filePath, 'utf8');
fs.writeFileSync(backupPath, content, 'utf8');
console.log(`✓ バックアップ: ${backupPath}`);

let lines = content.split('\n');
let newLines = [];
let moveCount = 0;

for (let i = 0; i < lines.length; i++) {
    let line = lines[i];
    
    // YearlySeparatorは除外
    if (line.includes('Name="YearlySeparator"')) {
        newLines.push(line);
        continue;
    }
    
    // 年計セクションの要素を15ピクセル上へ（Top値から-15）
    // Y_Sec, YLine*, Y_Item*, Y_R*を対象
    if ((line.includes('Name="Y_Sec"') || 
         line.includes('Name="YLine') || 
         line.includes('Name="Y_Item') || 
         line.includes('Name="Y_R')) && 
        line.includes('Top="')) {
        
        line = line.replace(/Top="(\d+(?:\.\d+)?)"/, (match, topVal) => {
            const newTop = (parseFloat(topVal) - 15).toFixed(2);
            moveCount++;
            return `Top="${newTop}"`;
        });
    }
    
    newLines.push(line);
}

// 保存
fs.writeFileSync(filePath, newLines.join('\n'), 'utf8');

console.log('\n=== 年計セクション移動完了 ===');
console.log(`移動した要素: ${moveCount}個`);
console.log(`（15ピクセル上へ移動）`);
console.log(`\n修正ファイル: ${filePath}`);
console.log(`バックアップ: ${backupPath}`);