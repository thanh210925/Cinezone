-- ========================================================
-- SCRIPT TẠO BẢNG CHATMessages CHO CƠ SỞ DỮ LIỆU CINEZONE
-- ========================================================

-- Sử dụng đúng Database của dự án (Nếu chạy trên SSMS, hãy chắc chắn chọn đúng database)
-- USE CinezoneDB;
-- GO

IF OBJECT_ID('dbo.ChatMessages', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessages] (
        [ChatMessageId] INT IDENTITY(1,1) NOT NULL,
        [CustomerId] INT NOT NULL,
        [MessageText] NVARCHAR(MAX) NOT NULL,
        [IsFromCustomer] BIT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [IsRead] BIT NOT NULL DEFAULT 0,
        
        -- Khóa chính
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([ChatMessageId]),
        
        -- Khóa ngoại liên kết tới bảng Customers
        CONSTRAINT [FK_ChatMessages_Customers_CustomerId] 
            FOREIGN KEY ([CustomerId]) 
            REFERENCES [dbo].[Customers] ([CustomerId]) 
            ON DELETE CASCADE
    );
    
    PRINT 'Đã tạo bảng dbo.ChatMessages thành công!';
END
ELSE
BEGIN
    PRINT 'Bảng dbo.ChatMessages đã tồn tại trong cơ sở dữ liệu.';
END
GO
