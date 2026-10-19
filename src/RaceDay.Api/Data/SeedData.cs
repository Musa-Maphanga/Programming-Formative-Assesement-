using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// The same sample data as the INSERT statements in docs/RaceDay_Schema.sql.
/// The only difference is that the placeholder password hashes from Part 1 are replaced
/// with a real BCrypt hash, so the sample accounts can log in.
/// Password for every sample account: Password123!
/// </summary>
public static class SeedData
{
    public const string SamplePassword = "Password123!";

    // BCrypt hash (work factor 11) of "Password123!"
    private const string SampleHash = "$2a$11$nCXbXkzb8esPda9FD1pjDOmne6wJJ3MInzMWe1DZKq0RnFvEQcZF.";

    private static readonly DateTime SeededAt = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = RoleNames.Organiser },
            new Role { RoleId = 2, RoleName = RoleNames.Participant });

        modelBuilder.Entity<EventType>().HasData(
            new EventType { EventTypeId = 1, TypeName = "Run" },
            new EventType { EventTypeId = 2, TypeName = "Walk" },
            new EventType { EventTypeId = 3, TypeName = "Cycle" });

        // 2 organisers and 3 participants
        modelBuilder.Entity<User>().HasData(
            new User { UserId = 1, RoleId = 1, FirstName = "Thandiwe", LastName = "Nkosi", Email = "thandiwe.nkosi@raceday.co.za", PasswordHash = SampleHash, PhoneNumber = "0821234567", DateOfBirth = new DateOnly(1985, 4, 12), Gender = "Female", City = "Johannesburg", CreatedAt = SeededAt },
            new User { UserId = 2, RoleId = 1, FirstName = "Pieter", LastName = "van Wyk", Email = "pieter.vanwyk@raceday.co.za", PasswordHash = SampleHash, PhoneNumber = "0839876543", DateOfBirth = new DateOnly(1979, 9, 3), Gender = "Male", City = "Cape Town", CreatedAt = SeededAt },
            new User { UserId = 3, RoleId = 2, FirstName = "Sipho", LastName = "Mahlangu", Email = "sipho.mahlangu@gmail.com", PasswordHash = SampleHash, PhoneNumber = "0761112233", DateOfBirth = new DateOnly(1998, 1, 25), Gender = "Male", City = "Soweto", CreatedAt = SeededAt },
            new User { UserId = 4, RoleId = 2, FirstName = "Aisha", LastName = "Pillay", Email = "aisha.pillay@gmail.com", PasswordHash = SampleHash, PhoneNumber = "0724455667", DateOfBirth = new DateOnly(2008, 7, 14), Gender = "Female", City = "Durban", CreatedAt = SeededAt },
            new User { UserId = 5, RoleId = 2, FirstName = "Lerato", LastName = "Mokoena", Email = "lerato.mokoena@outlook.com", PasswordHash = SampleHash, PhoneNumber = "0817788990", DateOfBirth = new DateOnly(1991, 11, 30), Gender = "Female", City = "Pretoria", CreatedAt = SeededAt });

        // One event that has already happened (so there are results) and two coming up
        modelBuilder.Entity<Event>().HasData(
            new Event
            {
                EventId = 1, OrganiserId = 1, EventTypeId = 1, Name = "Jozi Spring Fun Run",
                Description = "A community run through Soweto celebrating the start of spring. Water points every 2.5km.",
                EventDate = new DateTime(2026, 8, 30, 7, 0, 0), Location = "Orlando Stadium", City = "Johannesburg", Province = "Gauteng",
                Latitude = -26.234900m, Longitude = 27.908200m, DistanceKm = 21.10m, MaxParticipants = 2000,
                Status = EventStatuses.Completed, CreatedAt = SeededAt
            },
            new Event
            {
                EventId = 2, OrganiserId = 1, EventTypeId = 1, Name = "Durban Beachfront Half Marathon",
                Description = "Flat, fast out-and-back route along the Golden Mile. Ideal for a personal best.",
                EventDate = new DateTime(2026, 10, 18, 6, 0, 0), Location = "uShaka Marine World", City = "Durban", Province = "KwaZulu-Natal",
                Latitude = -29.867800m, Longitude = 31.045500m, DistanceKm = 21.10m, MaxParticipants = 3000,
                Status = EventStatuses.Upcoming, CreatedAt = SeededAt
            },
            new Event
            {
                EventId = 3, OrganiserId = 2, EventTypeId = 3, Name = "Cape Winelands Cycle Classic",
                Description = "Scenic road cycling tour through Stellenbosch and Franschhoek wine country.",
                EventDate = new DateTime(2026, 11, 8, 6, 30, 0), Location = "Stellenbosch Town Hall", City = "Stellenbosch", Province = "Western Cape",
                Latitude = -33.932100m, Longitude = 18.860200m, DistanceKm = 65.00m, MaxParticipants = 1500,
                Status = EventStatuses.Upcoming, CreatedAt = SeededAt
            });

        modelBuilder.Entity<Category>().HasData(
            // Event 1: Jozi Spring Fun Run
            new Category { CategoryId = 1, EventId = 1, Name = "5km Fun Run", DistanceKm = 5.00m, MinAge = null, MaxAge = null, EntryFee = 80.00m },
            new Category { CategoryId = 2, EventId = 1, Name = "10km Open", DistanceKm = 10.00m, MinAge = 16, MaxAge = null, EntryFee = 150.00m },
            new Category { CategoryId = 3, EventId = 1, Name = "21km Senior", DistanceKm = 21.10m, MinAge = 18, MaxAge = 39, EntryFee = 250.00m },
            // Event 2: Durban Beachfront
            new Category { CategoryId = 4, EventId = 2, Name = "10km Under 20", DistanceKm = 10.00m, MinAge = 14, MaxAge = 19, EntryFee = 120.00m },
            new Category { CategoryId = 5, EventId = 2, Name = "21km Senior", DistanceKm = 21.10m, MinAge = 18, MaxAge = 39, EntryFee = 280.00m },
            new Category { CategoryId = 6, EventId = 2, Name = "21km Veteran", DistanceKm = 21.10m, MinAge = 40, MaxAge = null, EntryFee = 280.00m },
            // Event 3: Cape Winelands
            new Category { CategoryId = 7, EventId = 3, Name = "35km Social Ride", DistanceKm = 35.00m, MinAge = 12, MaxAge = null, EntryFee = 350.00m },
            new Category { CategoryId = 8, EventId = 3, Name = "65km Challenge", DistanceKm = 65.00m, MinAge = 18, MaxAge = null, EntryFee = 550.00m });

        modelBuilder.Entity<Enrolment>().HasData(
            new Enrolment { EnrolmentId = 1, ParticipantId = 3, EventId = 1, CategoryId = 3, RaceNumber = "JS1001", Status = EnrolmentStatuses.Confirmed, EnrolledAt = SeededAt },
            new Enrolment { EnrolmentId = 2, ParticipantId = 5, EventId = 1, CategoryId = 2, RaceNumber = "JS2001", Status = EnrolmentStatuses.Confirmed, EnrolledAt = SeededAt },
            new Enrolment { EnrolmentId = 3, ParticipantId = 4, EventId = 1, CategoryId = 1, RaceNumber = "JS3001", Status = EnrolmentStatuses.Confirmed, EnrolledAt = SeededAt },
            new Enrolment { EnrolmentId = 4, ParticipantId = 4, EventId = 2, CategoryId = 4, RaceNumber = "DB1001", Status = EnrolmentStatuses.Confirmed, EnrolledAt = SeededAt },
            new Enrolment { EnrolmentId = 5, ParticipantId = 3, EventId = 2, CategoryId = 5, RaceNumber = "DB2001", Status = EnrolmentStatuses.Confirmed, EnrolledAt = SeededAt },
            new Enrolment { EnrolmentId = 6, ParticipantId = 5, EventId = 3, CategoryId = 8, RaceNumber = "CW1001", Status = EnrolmentStatuses.Confirmed, EnrolledAt = SeededAt });

        // Results only for the completed event, captured by its organiser (UserId 1)
        modelBuilder.Entity<Result>().HasData(
            new Result { ResultId = 1, EnrolmentId = 1, FinishTime = new TimeSpan(1, 38, 42), Position = 12, RecordedBy = 1, RecordedAt = SeededAt },
            new Result { ResultId = 2, EnrolmentId = 2, FinishTime = new TimeSpan(0, 52, 15), Position = 7, RecordedBy = 1, RecordedAt = SeededAt },
            new Result { ResultId = 3, EnrolmentId = 3, FinishTime = new TimeSpan(0, 31, 8), Position = 23, RecordedBy = 1, RecordedAt = SeededAt });
    }
}
