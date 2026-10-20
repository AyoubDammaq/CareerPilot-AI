using CareerPilot.Domain.Audit;
using CareerPilot.Domain.Common;
using CareerPilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Infrastructure.Persistence;

public class CareerPilotDbContext(DbContextOptions<CareerPilotDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<Education> Education => Set<Education>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProfileLanguage> Languages => Set<ProfileLanguage>();
    public DbSet<JobPreference> JobPreferences => Set<JobPreference>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(b =>
        {
            b.ToTable("user_profiles");
            b.Property(x => x.FullName).HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.Headline).HasMaxLength(200);
            b.Property(x => x.Summary).HasMaxLength(4000);
            b.Property(x => x.Country).HasMaxLength(100);
            b.Property(x => x.City).HasMaxLength(100);
            b.Property(x => x.GithubUrl).HasMaxLength(500);
            b.Property(x => x.LinkedinUrl).HasMaxLength(500);
            b.Property(x => x.WebsiteUrl).HasMaxLength(500);

            // Database-level optimistic concurrency, in addition to the explicit check in ProfileService.
            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasMany(x => x.WorkExperiences).WithOne().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Education).WithOne().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Skills).WithOne().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Projects).WithOne().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Languages).WithOne().HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Preferences).WithOne().HasForeignKey<JobPreference>(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkExperience>(b =>
        {
            b.ToTable("work_experiences");
            b.Property(x => x.Company).HasMaxLength(200);
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Location).HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.HasIndex(x => new { x.UserProfileId, x.Position });
        });

        modelBuilder.Entity<Education>(b =>
        {
            b.ToTable("education");
            b.Property(x => x.Institution).HasMaxLength(200);
            b.Property(x => x.Degree).HasMaxLength(200);
            b.Property(x => x.Field).HasMaxLength(200);
            b.HasIndex(x => new { x.UserProfileId, x.Position });
        });

        modelBuilder.Entity<Skill>(b =>
        {
            b.ToTable("skills");
            b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.Category).HasMaxLength(100);
            b.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(x => new { x.UserProfileId, x.Position });
        });

        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("projects");
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Url).HasMaxLength(500);
            b.HasIndex(x => new { x.UserProfileId, x.Position });
        });

        modelBuilder.Entity<ProfileLanguage>(b =>
        {
            b.ToTable("languages");
            b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.Level).HasMaxLength(50);
            b.HasIndex(x => new { x.UserProfileId, x.Position });
        });

        modelBuilder.Entity<JobPreference>(b =>
        {
            b.ToTable("job_preferences");
            b.Property(x => x.ExperienceLevel).HasMaxLength(100);
            b.Property(x => x.SalaryCurrency).HasMaxLength(3);
            b.Property(x => x.MinSalary).HasPrecision(12, 2);
            b.Property(x => x.PreferredSalary).HasPrecision(12, 2);
            b.HasIndex(x => x.UserProfileId).IsUnique();
        });

        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.ToTable("audit_events");
            b.Property(x => x.Actor).HasMaxLength(100);
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.EntityType).HasMaxLength(100);
            b.Property(x => x.Details).HasMaxLength(1000);
            b.HasIndex(x => x.OccurredAt);
        });

        // Ids are generated in code (time-ordered GUIDs). Without this, EF treats a new child with
        // a pre-set key as an existing row (UPDATE) instead of an INSERT.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)).ToList())
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
        }
    }
}
