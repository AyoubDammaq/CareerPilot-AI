using CareerPilot.Domain.Audit;
using CareerPilot.Domain.Common;
using CareerPilot.Domain.Jobs;
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
    public DbSet<WatchedCompany> WatchedCompanies => Set<WatchedCompany>();
    public DbSet<JobListing> JobListings => Set<JobListing>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<SearchRun> SearchRuns => Set<SearchRun>();

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

        modelBuilder.Entity<WatchedCompany>(b =>
        {
            b.ToTable("watched_companies");
            b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.BoardToken).HasMaxLength(100);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.LastError).HasMaxLength(500);
            b.HasIndex(x => new { x.Kind, x.BoardToken }).IsUnique();
        });

        modelBuilder.Entity<JobListing>(b =>
        {
            b.ToTable("job_listings");
            b.Property(x => x.CanonicalKey).HasMaxLength(600);
            b.Property(x => x.Title).HasMaxLength(300);
            b.Property(x => x.CompanyName).HasMaxLength(100);
            b.Property(x => x.LocationText).HasMaxLength(1000);
            b.Property(x => x.Country).HasMaxLength(100);
            b.Property(x => x.City).HasMaxLength(200);
            b.Property(x => x.EmploymentType).HasMaxLength(100);
            b.Property(x => x.SalaryCurrency).HasMaxLength(10);
            b.Property(x => x.SalaryPeriod).HasMaxLength(50);
            b.Property(x => x.SalaryMin).HasPrecision(14, 2);
            b.Property(x => x.SalaryMax).HasPrecision(14, 2);
            b.Property(x => x.Language).HasMaxLength(10);
            b.Property(x => x.GeoNote).HasMaxLength(1000);
            b.Property(x => x.MatchJson).HasColumnType("text");
            b.Property(x => x.AiAnalysisJson).HasColumnType("text");
            b.Property(x => x.WorkMode).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Seniority).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.GeoFit).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
            b.HasMany(x => x.Postings).WithOne().HasForeignKey(x => x.JobListingId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.CanonicalKey);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.PublishedAt);
            b.HasIndex(x => x.DiscoveredAt);
        });

        modelBuilder.Entity<JobPosting>(b =>
        {
            b.ToTable("job_postings");
            b.Property(x => x.SourceJobId).HasMaxLength(200);
            b.Property(x => x.SourceUrl).HasMaxLength(2000);
            b.Property(x => x.ApplyUrl).HasMaxLength(2000);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            // A deleted watchlist entry keeps its listings; they simply stop being re-verified.
            b.HasOne<WatchedCompany>().WithMany().HasForeignKey(x => x.WatchedCompanyId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.WatchedCompanyId, x.SourceJobId }).IsUnique();
        });

        modelBuilder.Entity<SearchRun>(b =>
        {
            b.ToTable("search_runs");
            b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(x => x.CreatedAt);
        });

        // Ids are generated in code (time-ordered GUIDs). Without this, EF treats a new child with
        // a pre-set key as an existing row (UPDATE) instead of an INSERT.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)).ToList())
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
        }
    }
}
