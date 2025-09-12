-- 営業日報テーブル
CREATE TABLE BusinessDailyReport (
    ClassificationCode NVARCHAR(3) NOT NULL,    -- 分類コード（001～035）
    CustomerClassName NVARCHAR(12),              -- 得意先分類名1（全角6文字）
    SupplierClassName NVARCHAR(12),              -- 仕入先分類名1（全角6文字）
    
    -- 日計項目（16項目）
    DailyCashSales DECIMAL(19,2) DEFAULT 0,               -- 現金売上
    DailyCashSalesTax DECIMAL(19,2) DEFAULT 0,            -- 現売消費税
    DailyCreditSales DECIMAL(19,2) DEFAULT 0,             -- 掛売上＋売上返品
    DailySalesDiscount DECIMAL(19,2) DEFAULT 0,           -- 売上値引
    DailyCreditSalesTax DECIMAL(19,2) DEFAULT 0,          -- 掛売消費税
    DailyCashPurchase DECIMAL(19,2) DEFAULT 0,            -- 現金仕入
    DailyCashPurchaseTax DECIMAL(19,2) DEFAULT 0,         -- 現仕消費税
    DailyCreditPurchase DECIMAL(19,2) DEFAULT 0,          -- 掛仕入＋仕入返品
    DailyPurchaseDiscount DECIMAL(19,2) DEFAULT 0,        -- 仕入値引
    DailyCreditPurchaseTax DECIMAL(19,2) DEFAULT 0,       -- 掛仕入消費税
    DailyCashReceipt DECIMAL(19,2) DEFAULT 0,             -- 現金・小切手・手形入金
    DailyBankReceipt DECIMAL(19,2) DEFAULT 0,             -- 振込入金
    DailyOtherReceipt DECIMAL(19,2) DEFAULT 0,            -- 入金値引・その他入金
    DailyCashPayment DECIMAL(19,2) DEFAULT 0,             -- 現金・小切手・手形支払
    DailyBankPayment DECIMAL(19,2) DEFAULT 0,             -- 振込支払
    DailyOtherPayment DECIMAL(19,2) DEFAULT 0,            -- 支払値引・その他支払
    
    -- 月計項目（16項目）※今回は実装しない
    -- 年計項目（16項目）※今回は実装しない
    
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    UpdatedDate DATETIME2 DEFAULT GETDATE(),
    
    CONSTRAINT PK_BusinessDailyReport PRIMARY KEY (ClassificationCode)
);

-- 初期データ投入（分類001～035 + 合計用の000）
INSERT INTO BusinessDailyReport (ClassificationCode, CustomerClassName, SupplierClassName)
VALUES 
('000', '合計', '合計'),
('001', '', ''),
('002', '', ''),
('003', '', ''),
('004', '', ''),
('005', '', ''),
('006', '', ''),
('007', '', ''),
('008', '', ''),
('009', '', ''),
('010', '', ''),
('011', '', ''),
('012', '', ''),
('013', '', ''),
('014', '', ''),
('015', '', ''),
('016', '', ''),
('017', '', ''),
('018', '', ''),
('019', '', ''),
('020', '', ''),
('021', '', ''),
('022', '', ''),
('023', '', ''),
('024', '', ''),
('025', '', ''),
('026', '', ''),
('027', '', ''),
('028', '', ''),
('029', '', ''),
('030', '', ''),
('031', '', ''),
('032', '', ''),
('033', '', ''),
('034', '', ''),
('035', '', '');