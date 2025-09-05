-- Add Location field to Products table for enforcing agent location constraints
-- OnPremises agents can only process OnPremises databases
-- Azure agents can only process Azure databases

-- Step 1: Add Location column to Products table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'Location')
BEGIN
    ALTER TABLE Products
    ADD Location NVARCHAR(50) NOT NULL DEFAULT 'OnPremises';
    
    PRINT 'Added Location column to Products table';
END
ELSE
BEGIN
    PRINT 'Location column already exists in Products table';
END
GO

-- Step 2: Update existing products based on connection string patterns
UPDATE Products
SET Location = 'Azure'
WHERE ConnectionString LIKE '%.database.windows.net%';

PRINT 'Updated products with Azure connection strings';
GO

-- Step 3: Show the distribution
SELECT 
    Location,
    COUNT(*) as ProductCount
FROM Products
GROUP BY Location;
GO

-- Step 4: Add check constraint to ensure valid locations
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Products_Location')
BEGIN
    ALTER TABLE Products
    ADD CONSTRAINT CK_Products_Location 
    CHECK (Location IN ('OnPremises', 'Azure'));
    
    PRINT 'Added location check constraint';
END
GO

-- Step 5: Create index for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_Location')
BEGIN
    CREATE INDEX IX_Products_Location ON Products(Location);
    PRINT 'Created index on Location column';
END
GO

PRINT 'Product location migration completed successfully';
GO