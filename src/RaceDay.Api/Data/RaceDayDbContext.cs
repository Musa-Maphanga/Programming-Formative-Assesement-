using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Code-first model for RaceDay. Everything here mirrors docs/RaceDay_Schema.sql from Part 1:
/// same tables, column names, sizes, defaults, unique keys, check constraints and foreign keys.
/// No relationship cascades on delete – the API returns 409 Conflict instead (see the endpoint plan).
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQL Server uses SYSUTCDATETIME() like the Part 1 script. The unit tests run on SQLite,
        // which has a different function name, so the default is picked per provider.
        var utcNow = Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer"
            ? "SYSUTCDATETIME()"
            : "CURRENT_TIMESTAMP";

        // ---------------- Roles ----------------
        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("Roles");
            b.HasKey(r => r.RoleId).HasName("PK_Roles");
            b.Property(r => r.RoleName).HasMaxLength(20).IsRequired();
            b.HasIndex(r => r.RoleName).IsUnique().HasDatabaseName("UQ_Roles_RoleName");
        });

        // ---------------- Users ----------------
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users", t =>
                t.HasCheckConstraint("CK_Users_Gender", "[Gender] IS NULL OR [Gender] IN ('Male','Female','Other')"));
            b.HasKey(u => u.UserId).HasName("PK_Users");
            b.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
            b.Property(u => u.LastName).HasMaxLength(50).IsRequired();
            b.Property(u => u.Email).HasMaxLength(255).IsRequired();
            b.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            b.Property(u => u.PhoneNumber).HasMaxLength(20);
            b.Property(u => u.DateOfBirth).HasColumnType("date");
            b.Property(u => u.Gender).HasMaxLength(10);
            b.Property(u => u.City).HasMaxLength(100);
            b.Property(u => u.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql(utcNow);
            b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Users_Email");

            b.HasOne(u => u.Role).WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .HasConstraintName("FK_Users_Roles")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- EventTypes ----------------
        modelBuilder.Entity<EventType>(b =>
        {
            b.ToTable("EventTypes");
            b.HasKey(t => t.EventTypeId).HasName("PK_EventTypes");
            b.Property(t => t.TypeName).HasMaxLength(20).IsRequired();
            b.HasIndex(t => t.TypeName).IsUnique().HasDatabaseName("UQ_EventTypes_TypeName");
        });

        // ---------------- Events ----------------
        modelBuilder.Entity<Event>(b =>
        {
            b.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_Distance", "[DistanceKm] > 0");
                t.HasCheckConstraint("CK_Events_MaxParticipants", "[MaxParticipants] IS NULL OR [MaxParticipants] > 0");
                t.HasCheckConstraint("CK_Events_Status", "[Status] IN ('Upcoming','Completed','Cancelled')");
            });
            b.HasKey(e => e.EventId).HasName("PK_Events");
            b.Property(e => e.Name).HasMaxLength(150).IsRequired();
            b.Property(e => e.Description).HasMaxLength(2000);
            b.Property(e => e.EventDate).HasColumnType("datetime2(0)");
            b.Property(e => e.Location).HasMaxLength(200).IsRequired();
            b.Property(e => e.City).HasMaxLength(100).IsRequired();
            b.Property(e => e.Province).HasMaxLength(50).IsRequired();
            b.Property(e => e.Latitude).HasPrecision(9, 6);
            b.Property(e => e.Longitude).HasPrecision(9, 6);
            b.Property(e => e.DistanceKm).HasPrecision(6, 2);
            b.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue(EventStatuses.Upcoming);
            b.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql(utcNow);

            // OrganiserId points at Users. The database cannot check that this user is an
            // Organiser – that rule is enforced in EventsController (Part 1 feedback).
            b.HasOne(e => e.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(e => e.OrganiserId)
                .HasConstraintName("FK_Events_Organiser")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.EventType).WithMany(t => t.Events)
                .HasForeignKey(e => e.EventTypeId)
                .HasConstraintName("FK_Events_EventTypes")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Categories ----------------
        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("Categories", t =>
            {
                t.HasCheckConstraint("CK_Categories_Distance", "[DistanceKm] > 0");
                t.HasCheckConstraint("CK_Categories_Ages", "[MinAge] IS NULL OR [MaxAge] IS NULL OR [MinAge] <= [MaxAge]");
                t.HasCheckConstraint("CK_Categories_Fee", "[EntryFee] >= 0");
            });
            b.HasKey(c => c.CategoryId).HasName("PK_Categories");
            b.Property(c => c.Name).HasMaxLength(50).IsRequired();
            b.Property(c => c.DistanceKm).HasPrecision(6, 2);
            b.Property(c => c.EntryFee).HasPrecision(8, 2).HasDefaultValue(0m);

            b.HasIndex(c => new { c.EventId, c.Name }).IsUnique().HasDatabaseName("UQ_Categories_Event_Name");

            // (CategoryId, EventId) is unique so Enrolments can point at it with a composite FK.
            // This is what stops a participant picking a category from a different event.
            b.HasAlternateKey(c => new { c.CategoryId, c.EventId }).HasName("UQ_Categories_Id_Event");

            b.HasOne(c => c.Event).WithMany(e => e.Categories)
                .HasForeignKey(c => c.EventId)
                .HasConstraintName("FK_Categories_Events")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Enrolments ----------------
        modelBuilder.Entity<Enrolment>(b =>
        {
            b.ToTable("Enrolments", t =>
                t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Confirmed','Withdrawn')"));
            b.HasKey(e => e.EnrolmentId).HasName("PK_Enrolments");
            b.Property(e => e.RaceNumber).HasMaxLength(10);
            b.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue(EnrolmentStatuses.Confirmed);
            b.Property(e => e.EnrolledAt).HasColumnType("datetime2(0)").HasDefaultValueSql(utcNow);

            // One entry per participant per event
            b.HasIndex(e => new { e.ParticipantId, e.EventId }).IsUnique().HasDatabaseName("UQ_Enrolments_Participant_Event");

            b.HasOne(e => e.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(e => e.ParticipantId)
                .HasConstraintName("FK_Enrolments_Participant")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.Event).WithMany(ev => ev.Enrolments)
                .HasForeignKey(e => e.EventId)
                .HasConstraintName("FK_Enrolments_Events")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(e => new { e.CategoryId, e.EventId })
                .HasPrincipalKey(c => new { c.CategoryId, c.EventId })
                .HasConstraintName("FK_Enrolments_Category_Event")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Results ----------------
        modelBuilder.Entity<Result>(b =>
        {
            b.ToTable("Results", t =>
                t.HasCheckConstraint("CK_Results_Position", "[Position] > 0"));
            b.HasKey(r => r.ResultId).HasName("PK_Results");
            b.Property(r => r.FinishTime).HasColumnType("time(0)");
            b.Property(r => r.RecordedAt).HasColumnType("datetime2(0)").HasDefaultValueSql(utcNow);

            // Enrolment 1 : 0..1 Result (EnrolmentId is unique in Results)
            b.HasIndex(r => r.EnrolmentId).IsUnique().HasDatabaseName("UQ_Results_Enrolment");
            b.HasOne(r => r.Enrolment).WithOne(e => e.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId)
                .HasConstraintName("FK_Results_Enrolments")
                .OnDelete(DeleteBehavior.Restrict);

            // User 1 : many Results. One organiser can record many results,
            // and every result is recorded by exactly one user (Part 1 feedback).
            b.HasOne(r => r.RecordedByUser).WithMany(u => u.RecordedResults)
                .HasForeignKey(r => r.RecordedBy)
                .IsRequired()
                .HasConstraintName("FK_Results_RecordedBy")
                .OnDelete(DeleteBehavior.Restrict);
        });

        SeedData.Apply(modelBuilder);
    }
}
