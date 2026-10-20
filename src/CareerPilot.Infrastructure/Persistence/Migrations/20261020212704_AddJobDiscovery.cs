using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_listings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalKey = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LocationText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CountryCodes = table.Column<List<string>>(type: "text[]", nullable: false),
                    WorkMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    SalaryCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SalaryPeriod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ExperienceYearsHint = table.Column<int>(type: "integer", nullable: true),
                    Skills = table.Column<List<string>>(type: "text[]", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApplicationDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Seniority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MentionsSponsorship = table.Column<bool>(type: "boolean", nullable: false),
                    MentionsRelocation = table.Column<bool>(type: "boolean", nullable: false),
                    GeoFit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GeoNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRelevant = table.Column<bool>(type: "boolean", nullable: false),
                    FilterReasons = table.Column<List<string>>(type: "text[]", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_listings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "search_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompaniesTotal = table.Column<int>(type: "integer", nullable: false),
                    CompaniesSucceeded = table.Column<int>(type: "integer", nullable: false),
                    JobsFetched = table.Column<int>(type: "integer", nullable: false),
                    JobsNew = table.Column<int>(type: "integer", nullable: false),
                    JobsUpdated = table.Column<int>(type: "integer", nullable: false),
                    JobsClosed = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "watched_companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BoardToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UseEuEndpoint = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastJobCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watched_companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_postings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchedCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceJobId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ApplyUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsPresent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_postings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_postings_job_listings_JobListingId",
                        column: x => x.JobListingId,
                        principalTable: "job_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_postings_watched_companies_WatchedCompanyId",
                        column: x => x.WatchedCompanyId,
                        principalTable: "watched_companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_listings_CanonicalKey",
                table: "job_listings",
                column: "CanonicalKey");

            migrationBuilder.CreateIndex(
                name: "IX_job_listings_DiscoveredAt",
                table: "job_listings",
                column: "DiscoveredAt");

            migrationBuilder.CreateIndex(
                name: "IX_job_listings_PublishedAt",
                table: "job_listings",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_job_listings_Status",
                table: "job_listings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_JobListingId",
                table: "job_postings",
                column: "JobListingId");

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_WatchedCompanyId_SourceJobId",
                table: "job_postings",
                columns: new[] { "WatchedCompanyId", "SourceJobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_search_runs_CreatedAt",
                table: "search_runs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_watched_companies_Kind_BoardToken",
                table: "watched_companies",
                columns: new[] { "Kind", "BoardToken" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_postings");

            migrationBuilder.DropTable(
                name: "search_runs");

            migrationBuilder.DropTable(
                name: "job_listings");

            migrationBuilder.DropTable(
                name: "watched_companies");
        }
    }
}
