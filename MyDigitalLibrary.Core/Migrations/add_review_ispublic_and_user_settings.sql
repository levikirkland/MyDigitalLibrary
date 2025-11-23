-- Add missing columns created by recent code changes
-- Run this against your SQL Server database used by the app.
-- It will add: reviews.IsPublic (bit), users.DisplayName (nvarchar(max)), users.ShareReviews (bit)

IF COL_LENGTH('dbo.reviews', 'IsPublic') IS NULL
BEGIN
    ALTER TABLE dbo.reviews ADD IsPublic bit NOT NULL CONSTRAINT DF_reviews_IsPublic DEFAULT (0);
END

IF COL_LENGTH('dbo.users', 'DisplayName') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD DisplayName nvarchar(max) NULL;
END

IF COL_LENGTH('dbo.users', 'ShareReviews') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD ShareReviews bit NOT NULL CONSTRAINT DF_users_ShareReviews DEFAULT (0);
END
