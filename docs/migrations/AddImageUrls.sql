/* ============================================================================
   RaceDay - add image URL columns (Part 3)

   Part 3 stores an event banner and a profile picture in Azure Blob Storage.
   The images themselves live in Azure; the database only keeps the URL.

   PREFERRED ROUTE: let Entity Framework do it, so the model and the database
   stay in step and a migration file is recorded in the project:

       cd src/RaceDay.Api
       dotnet ef migrations add AddImageUrls
       dotnet ef database update

   The API also calls Database.Migrate() on startup, so once the migration
   exists it is applied automatically the next time the API runs.

   THIS SCRIPT is the fallback for a database that has to be changed by hand,
   for example one restored from the Part 1 script. It is safe to run twice.
   ============================================================================ */

USE RaceDayApiDb;
GO

IF COL_LENGTH('dbo.Events', 'BannerImageUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Events ADD BannerImageUrl NVARCHAR(500) NULL;
    PRINT 'Added Events.BannerImageUrl';
END
ELSE
    PRINT 'Events.BannerImageUrl already exists';
GO

IF COL_LENGTH('dbo.Users', 'ProfilePictureUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD ProfilePictureUrl NVARCHAR(500) NULL;
    PRINT 'Added Users.ProfilePictureUrl';
END
ELSE
    PRINT 'Users.ProfilePictureUrl already exists';
GO

SELECT
    (SELECT COL_LENGTH('dbo.Events', 'BannerImageUrl')) AS EventsBannerImageUrl,
    (SELECT COL_LENGTH('dbo.Users', 'ProfilePictureUrl')) AS UsersProfilePictureUrl;
GO
