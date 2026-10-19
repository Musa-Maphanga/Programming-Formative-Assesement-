using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaceDay.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventTypes",
                columns: table => new
                {
                    EventTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTypes", x => x.EventTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.CheckConstraint("CK_Users_Gender", "[Gender] IS NULL OR [Gender] IN ('Male','Female','Other')");
                    table.ForeignKey(
                        name: "FK_Users_Roles",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganiserId = table.Column<int>(type: "int", nullable: false),
                    EventTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EventDate = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    DistanceKm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MaxParticipants = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Upcoming"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventId);
                    table.CheckConstraint("CK_Events_Distance", "[DistanceKm] > 0");
                    table.CheckConstraint("CK_Events_MaxParticipants", "[MaxParticipants] IS NULL OR [MaxParticipants] > 0");
                    table.CheckConstraint("CK_Events_Status", "[Status] IN ('Upcoming','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_Events_EventTypes",
                        column: x => x.EventTypeId,
                        principalTable: "EventTypes",
                        principalColumn: "EventTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Organiser",
                        column: x => x.OrganiserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DistanceKm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    EntryFee = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryId);
                    table.UniqueConstraint("UQ_Categories_Id_Event", x => new { x.CategoryId, x.EventId });
                    table.CheckConstraint("CK_Categories_Ages", "[MinAge] IS NULL OR [MaxAge] IS NULL OR [MinAge] <= [MaxAge]");
                    table.CheckConstraint("CK_Categories_Distance", "[DistanceKm] > 0");
                    table.CheckConstraint("CK_Categories_Fee", "[EntryFee] >= 0");
                    table.ForeignKey(
                        name: "FK_Categories_Events",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrolments",
                columns: table => new
                {
                    EnrolmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParticipantId = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    RaceNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Confirmed"),
                    EnrolledAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrolments", x => x.EnrolmentId);
                    table.CheckConstraint("CK_Enrolments_Status", "[Status] IN ('Confirmed','Withdrawn')");
                    table.ForeignKey(
                        name: "FK_Enrolments_Category_Event",
                        columns: x => new { x.CategoryId, x.EventId },
                        principalTable: "Categories",
                        principalColumns: new[] { "CategoryId", "EventId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrolments_Events",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrolments_Participant",
                        column: x => x.ParticipantId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    ResultId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrolmentId = table.Column<int>(type: "int", nullable: false),
                    FinishTime = table.Column<TimeSpan>(type: "time(0)", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    RecordedBy = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.ResultId);
                    table.CheckConstraint("CK_Results_Position", "[Position] > 0");
                    table.ForeignKey(
                        name: "FK_Results_Enrolments",
                        column: x => x.EnrolmentId,
                        principalTable: "Enrolments",
                        principalColumn: "EnrolmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Results_RecordedBy",
                        column: x => x.RecordedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "EventTypes",
                columns: new[] { "EventTypeId", "TypeName" },
                values: new object[,]
                {
                    { 1, "Run" },
                    { 2, "Walk" },
                    { 3, "Cycle" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "RoleId", "RoleName" },
                values: new object[,]
                {
                    { 1, "Organiser" },
                    { 2, "Participant" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "City", "CreatedAt", "DateOfBirth", "Email", "FirstName", "Gender", "LastName", "PasswordHash", "PhoneNumber", "RoleId" },
                values: new object[,]
                {
                    { 1, "Johannesburg", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), new DateOnly(1985, 4, 12), "thandiwe.nkosi@raceday.co.za", "Thandiwe", "Female", "Nkosi", "$2a$11$nCXbXkzb8esPda9FD1pjDOmne6wJJ3MInzMWe1DZKq0RnFvEQcZF.", "0821234567", 1 },
                    { 2, "Cape Town", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), new DateOnly(1979, 9, 3), "pieter.vanwyk@raceday.co.za", "Pieter", "Male", "van Wyk", "$2a$11$nCXbXkzb8esPda9FD1pjDOmne6wJJ3MInzMWe1DZKq0RnFvEQcZF.", "0839876543", 1 },
                    { 3, "Soweto", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), new DateOnly(1998, 1, 25), "sipho.mahlangu@gmail.com", "Sipho", "Male", "Mahlangu", "$2a$11$nCXbXkzb8esPda9FD1pjDOmne6wJJ3MInzMWe1DZKq0RnFvEQcZF.", "0761112233", 2 },
                    { 4, "Durban", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2008, 7, 14), "aisha.pillay@gmail.com", "Aisha", "Female", "Pillay", "$2a$11$nCXbXkzb8esPda9FD1pjDOmne6wJJ3MInzMWe1DZKq0RnFvEQcZF.", "0724455667", 2 },
                    { 5, "Pretoria", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), new DateOnly(1991, 11, 30), "lerato.mokoena@outlook.com", "Lerato", "Female", "Mokoena", "$2a$11$nCXbXkzb8esPda9FD1pjDOmne6wJJ3MInzMWe1DZKq0RnFvEQcZF.", "0817788990", 2 }
                });

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "EventId", "City", "CreatedAt", "Description", "DistanceKm", "EventDate", "EventTypeId", "Latitude", "Location", "Longitude", "MaxParticipants", "Name", "OrganiserId", "Province", "Status" },
                values: new object[,]
                {
                    { 1, "Johannesburg", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), "A community run through Soweto celebrating the start of spring. Water points every 2.5km.", 21.10m, new DateTime(2026, 8, 30, 7, 0, 0, 0, DateTimeKind.Unspecified), 1, -26.234900m, "Orlando Stadium", 27.908200m, 2000, "Jozi Spring Fun Run", 1, "Gauteng", "Completed" },
                    { 2, "Durban", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), "Flat, fast out-and-back route along the Golden Mile. Ideal for a personal best.", 21.10m, new DateTime(2026, 10, 18, 6, 0, 0, 0, DateTimeKind.Unspecified), 1, -29.867800m, "uShaka Marine World", 31.045500m, 3000, "Durban Beachfront Half Marathon", 1, "KwaZulu-Natal", "Upcoming" },
                    { 3, "Stellenbosch", new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), "Scenic road cycling tour through Stellenbosch and Franschhoek wine country.", 65.00m, new DateTime(2026, 11, 8, 6, 30, 0, 0, DateTimeKind.Unspecified), 3, -33.932100m, "Stellenbosch Town Hall", 18.860200m, 1500, "Cape Winelands Cycle Classic", 2, "Western Cape", "Upcoming" }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "CategoryId", "DistanceKm", "EntryFee", "EventId", "MaxAge", "MinAge", "Name" },
                values: new object[,]
                {
                    { 1, 5.00m, 80.00m, 1, null, null, "5km Fun Run" },
                    { 2, 10.00m, 150.00m, 1, null, 16, "10km Open" },
                    { 3, 21.10m, 250.00m, 1, 39, 18, "21km Senior" },
                    { 4, 10.00m, 120.00m, 2, 19, 14, "10km Under 20" },
                    { 5, 21.10m, 280.00m, 2, 39, 18, "21km Senior" },
                    { 6, 21.10m, 280.00m, 2, null, 40, "21km Veteran" },
                    { 7, 35.00m, 350.00m, 3, null, 12, "35km Social Ride" },
                    { 8, 65.00m, 550.00m, 3, null, 18, "65km Challenge" }
                });

            migrationBuilder.InsertData(
                table: "Enrolments",
                columns: new[] { "EnrolmentId", "CategoryId", "EnrolledAt", "EventId", "ParticipantId", "RaceNumber", "Status" },
                values: new object[,]
                {
                    { 1, 3, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 1, 3, "JS1001", "Confirmed" },
                    { 2, 2, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 1, 5, "JS2001", "Confirmed" },
                    { 3, 1, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 1, 4, "JS3001", "Confirmed" },
                    { 4, 4, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 2, 4, "DB1001", "Confirmed" },
                    { 5, 5, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 2, 3, "DB2001", "Confirmed" },
                    { 6, 8, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 3, 5, "CW1001", "Confirmed" }
                });

            migrationBuilder.InsertData(
                table: "Results",
                columns: new[] { "ResultId", "EnrolmentId", "FinishTime", "Position", "RecordedAt", "RecordedBy" },
                values: new object[,]
                {
                    { 1, 1, new TimeSpan(0, 1, 38, 42, 0), 12, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, 2, new TimeSpan(0, 0, 52, 15, 0), 7, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 3, 3, new TimeSpan(0, 0, 31, 8, 0), 23, new DateTime(2026, 9, 1, 8, 0, 0, 0, DateTimeKind.Utc), 1 }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_Categories_Event_Name",
                table: "Categories",
                columns: new[] { "EventId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_CategoryId_EventId",
                table: "Enrolments",
                columns: new[] { "CategoryId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_EventId",
                table: "Enrolments",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "UQ_Enrolments_Participant_Event",
                table: "Enrolments",
                columns: new[] { "ParticipantId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventTypeId",
                table: "Events",
                column: "EventTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganiserId",
                table: "Events",
                column: "OrganiserId");

            migrationBuilder.CreateIndex(
                name: "UQ_EventTypes_TypeName",
                table: "EventTypes",
                column: "TypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Results_RecordedBy",
                table: "Results",
                column: "RecordedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Results_Enrolment",
                table: "Results",
                column: "EnrolmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Roles_RoleName",
                table: "Roles",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropTable(
                name: "Enrolments");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "EventTypes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
