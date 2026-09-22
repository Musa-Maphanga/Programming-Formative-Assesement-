IF DB_ID('RaceDay') IS NULL
    CREATE DATABASE RaceDay;
GO
USE RaceDay;
GO

IF OBJECT_ID('dbo.Results', 'U')     IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U')  IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U')  IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U')      IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.EventTypes', 'U')  IS NOT NULL DROP TABLE dbo.EventTypes;
IF OBJECT_ID('dbo.Users', 'U')       IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Roles', 'U')       IS NOT NULL DROP TABLE dbo.Roles;
GO

-- Roles: the two types of users (Organiser and Participant)

CREATE TABLE dbo.Roles (
    RoleId      INT IDENTITY(1,1) NOT NULL,
    RoleName    NVARCHAR(20)      NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);

-- Users: everyone who uses the system. RoleId says if they are an organiser or participant
CREATE TABLE dbo.Users (
    UserId        INT IDENTITY(1,1) NOT NULL,
    RoleId        INT               NOT NULL,
    FirstName     NVARCHAR(50)      NOT NULL,
    LastName      NVARCHAR(50)      NOT NULL,
    Email         NVARCHAR(255)     NOT NULL,
    PasswordHash  NVARCHAR(255)     NOT NULL,
    PhoneNumber   NVARCHAR(20)      NULL,
    DateOfBirth   DATE              NULL,
    Gender        NVARCHAR(10)      NULL,
    City          NVARCHAR(100)     NULL,
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId),
    CONSTRAINT CK_Users_Gender CHECK (Gender IS NULL OR Gender IN ('Male','Female','Other'))
);

-- EventTypes: Run, Walk or Cycle.
CREATE TABLE dbo.EventTypes (
    EventTypeId INT IDENTITY(1,1) NOT NULL,
    TypeName    NVARCHAR(20)      NOT NULL,
    CONSTRAINT PK_EventTypes PRIMARY KEY (EventTypeId),
    CONSTRAINT UQ_EventTypes_TypeName UNIQUE (TypeName)
);

-- Events: the races that organisers create
CREATE TABLE dbo.Events (
    EventId          INT IDENTITY(1,1) NOT NULL,
    OrganiserId      INT               NOT NULL,
    EventTypeId      INT               NOT NULL,
    Name             NVARCHAR(150)     NOT NULL,
    Description      NVARCHAR(2000)    NULL,
    EventDate        DATETIME2(0)      NOT NULL,
    Location         NVARCHAR(200)     NOT NULL,
    City             NVARCHAR(100)     NOT NULL,
    Province         NVARCHAR(50)      NOT NULL,
    Latitude         DECIMAL(9,6)      NULL,  
    Longitude        DECIMAL(9,6)      NULL,
    DistanceKm       DECIMAL(6,2)      NOT NULL,
    MaxParticipants  INT               NULL,
    Status           NVARCHAR(20)      NOT NULL CONSTRAINT DF_Events_Status DEFAULT 'Upcoming',
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Events_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Events_EventTypes FOREIGN KEY (EventTypeId) REFERENCES dbo.EventTypes(EventTypeId),
    CONSTRAINT CK_Events_Distance CHECK (DistanceKm > 0),
    CONSTRAINT CK_Events_MaxParticipants CHECK (MaxParticipants IS NULL OR MaxParticipants > 0),
    CONSTRAINT CK_Events_Status CHECK (Status IN ('Upcoming','Completed','Cancelled'))
);

-- Categories: the age or distance groups for each event (e.g. Under 20, 10km, 21km)
CREATE TABLE dbo.Categories (
    CategoryId  INT IDENTITY(1,1) NOT NULL,
    EventId     INT               NOT NULL,
    Name        NVARCHAR(50)      NOT NULL,
    DistanceKm  DECIMAL(6,2)      NOT NULL,
    MinAge      INT               NULL,
    MaxAge      INT               NULL,
    EntryFee    DECIMAL(8,2)      NOT NULL CONSTRAINT DF_Categories_EntryFee DEFAULT 0,
    CONSTRAINT PK_Categories PRIMARY KEY (CategoryId),
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId),
    CONSTRAINT UQ_Categories_Event_Name UNIQUE (EventId, Name),
    -- Needed so the Enrolments table can check that the category is from the same event
    CONSTRAINT UQ_Categories_Id_Event UNIQUE (CategoryId, EventId),
    CONSTRAINT CK_Categories_Distance CHECK (DistanceKm > 0),
    CONSTRAINT CK_Categories_Ages CHECK (MinAge IS NULL OR MaxAge IS NULL OR MinAge <= MaxAge),
    CONSTRAINT CK_Categories_Fee CHECK (EntryFee >= 0)
);

-- Enrolments: links a participant to an event and the category they picked.
CREATE TABLE dbo.Enrolments (
    EnrolmentId    INT IDENTITY(1,1) NOT NULL,
    ParticipantId  INT               NOT NULL,
    EventId        INT               NOT NULL,
    CategoryId     INT               NOT NULL,
    RaceNumber     NVARCHAR(10)      NULL,
    Status         NVARCHAR(20)      NOT NULL CONSTRAINT DF_Enrolments_Status DEFAULT 'Confirmed',
    EnrolledAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Enrolments_EnrolledAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Enrolments PRIMARY KEY (EnrolmentId),
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId),
    CONSTRAINT FK_Enrolments_Category_Event FOREIGN KEY (CategoryId, EventId)
        REFERENCES dbo.Categories(CategoryId, EventId),
    CONSTRAINT UQ_Enrolments_Participant_Event UNIQUE (ParticipantId, EventId), -- a participant can only enter an event once
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Confirmed','Withdrawn'))
);

-- Results: the finish time and position for an enrolment, captured by the organiser
CREATE TABLE dbo.Results (
    ResultId     INT IDENTITY(1,1) NOT NULL,
    EnrolmentId  INT               NOT NULL,
    FinishTime   TIME(0)           NOT NULL,
    Position     INT               NOT NULL,
    RecordedBy   INT               NOT NULL,
    RecordedAt   DATETIME2(0)      NOT NULL CONSTRAINT DF_Results_RecordedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Results PRIMARY KEY (ResultId),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments(EnrolmentId),
    CONSTRAINT FK_Results_RecordedBy FOREIGN KEY (RecordedBy) REFERENCES dbo.Users(UserId),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentId),  -- each enrolment can only have one result
    CONSTRAINT CK_Results_Position CHECK (Position > 0)
);
GO

/* ============= SEED DATA =============== */
/* The password for all the sample users is: Password123!
   The passwords are stored as BCrypt hashes (not plain text) so they
   will work with BCrypt.Net-Next when the API is built in Part 2. */

INSERT INTO dbo.Roles (RoleName) VALUES ('Organiser'), ('Participant');

INSERT INTO dbo.EventTypes (TypeName) VALUES ('Run'), ('Walk'), ('Cycle');

-- 2 organisers (Thandiwe and Pieter) and 3 participants
INSERT INTO dbo.Users (RoleId, FirstName, LastName, Email, PasswordHash, PhoneNumber, DateOfBirth, Gender, City)
VALUES
 (1, 'Thandiwe', 'Nkosi',     'thandiwe.nkosi@raceday.co.za', '$2a$11$9lTUnoWGFsyQW65J4xmnfeJC09ZnPevwA8D2gHVI6Ic55czaw4kFq', '0821234567', '1985-04-12', 'Female', 'Johannesburg'),
 (1, 'Pieter',   'van Wyk',   'pieter.vanwyk@raceday.co.za',  '$2a$11$wkbnZ8GB2IpOP2qnQUFO5euF0FPEzhpqleOTSIMZFec.pQjkK0qNq', '0839876543', '1979-09-03', 'Male',   'Cape Town'),
 (2, 'Sipho',    'Mahlangu',  'sipho.mahlangu@gmail.com',     '$2a$11$Z8XBQGYRJwuBJO0BB0VzcOpI5lQ1zkoAjnjHZvO8iqxuYGZ6G2NNa', '0761112233', '1998-01-25', 'Male',   'Soweto'),
 (2, 'Aisha',    'Pillay',    'aisha.pillay@gmail.com',       '$2a$11$f3XHLuUOV9MAhQbOYbcC4eku2ltlWvxrhXthlN4vBnWao5tmPRo6W', '0724455667', '2008-07-14', 'Female', 'Durban'),
 (2, 'Lerato',   'Mokoena',   'lerato.mokoena@outlook.com',   '$2a$11$cHWQmmAFRKKy/pdLqwIiM.V/kXDH.W8yPQscLlD5dIWDPPxORQ4ym', '0817788990', '1991-11-30', 'Female', 'Pretoria');

-- 3 events: one that already happened and two coming up
INSERT INTO dbo.Events (OrganiserId, EventTypeId, Name, Description, EventDate, Location, City, Province, Latitude, Longitude, DistanceKm, MaxParticipants, Status)
VALUES
 (1, 1, 'Jozi Spring Fun Run',
  'A community run through Soweto celebrating the start of spring. Water points every 2.5km.',
  '2026-08-30 07:00', 'Orlando Stadium', 'Johannesburg', 'Gauteng', -26.234900, 27.908200, 21.10, 2000, 'Completed'),
 (1, 1, 'Durban Beachfront Half Marathon',
  'Flat, fast out-and-back route along the Golden Mile. Ideal for a personal best.',
  '2026-10-18 06:00', 'uShaka Marine World', 'Durban', 'KwaZulu-Natal', -29.867800, 31.045500, 21.10, 3000, 'Upcoming'),
 (2, 3, 'Cape Winelands Cycle Classic',
  'Scenic road cycling tour through Stellenbosch and Franschhoek wine country.',
  '2026-11-08 06:30', 'Stellenbosch Town Hall', 'Stellenbosch', 'Western Cape', -33.932100, 18.860200, 65.00, 1500, 'Upcoming');

-- Categories for each event
INSERT INTO dbo.Categories (EventId, Name, DistanceKm, MinAge, MaxAge, EntryFee)
VALUES
 -- Event 1: Jozi Spring Fun Run  (CategoryId 1-3)
 (1, '5km Fun Run',      5.00,  NULL, NULL,  80.00),
 (1, '10km Open',       10.00,  16,   NULL, 150.00),
 (1, '21km Senior',     21.10,  18,   39,   250.00),
 -- Event 2: Durban Beachfront (CategoryId 4-6)
 (2, '10km Under 20',   10.00,  14,   19,   120.00),
 (2, '21km Senior',     21.10,  18,   39,   280.00),
 (2, '21km Veteran',    21.10,  40,   NULL, 280.00),
 -- Event 3: Cape Winelands (CategoryId 7-8)
 (3, '35km Social Ride',35.00,  12,   NULL, 350.00),
 (3, '65km Challenge',  65.00,  18,   NULL, 550.00);

-- Sample enrolments (the ages of each participant fit the category they entered)
INSERT INTO dbo.Enrolments (ParticipantId, EventId, CategoryId, RaceNumber, Status)
VALUES
 (3, 1, 3, 'JS1001', 'Confirmed'),  -- Sipho   -> Jozi 21km   (EnrolmentId 1)
 (5, 1, 2, 'JS2001', 'Confirmed'),  -- Lerato  -> Jozi 10km   (EnrolmentId 2)
 (4, 1, 1, 'JS3001', 'Confirmed'),  -- Aisha   -> Jozi 5km    (EnrolmentId 3)
 (4, 2, 4, 'DB1001', 'Confirmed'),  -- Aisha   -> Durban U20  (EnrolmentId 4)
 (3, 2, 5, 'DB2001', 'Confirmed'),  -- Sipho   -> Durban 21km (EnrolmentId 5)
 (5, 3, 8, 'CW1001', 'Confirmed');  -- Lerato  -> Winelands 65km (EnrolmentId 6)

-- Results are only added for the Jozi Spring Fun Run because it is the only one that has happened.
INSERT INTO dbo.Results (EnrolmentId, FinishTime, Position, RecordedBy)
VALUES
 (1, '01:38:42', 12, 1),
 (2, '00:52:15',  7, 1),
 (3, '00:31:08', 23, 1);
GO

SELECT 'Roles' AS TableName, COUNT(*) AS TotalRows FROM dbo.Roles
UNION ALL SELECT 'Users', COUNT(*) FROM dbo.Users
UNION ALL SELECT 'EventTypes', COUNT(*) FROM dbo.EventTypes
UNION ALL SELECT 'Events', COUNT(*) FROM dbo.Events
UNION ALL SELECT 'Categories', COUNT(*) FROM dbo.Categories
UNION ALL SELECT 'Enrolments', COUNT(*) FROM dbo.Enrolments
UNION ALL SELECT 'Results', COUNT(*) FROM dbo.Results;
GO
