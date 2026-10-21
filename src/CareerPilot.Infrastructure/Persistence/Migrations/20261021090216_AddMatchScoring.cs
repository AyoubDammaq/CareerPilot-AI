using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatchJson",
                table: "job_listings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchScore",
                table: "job_listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScoredAt",
                table: "job_listings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatchJson",
                table: "job_listings");

            migrationBuilder.DropColumn(
                name: "MatchScore",
                table: "job_listings");

            migrationBuilder.DropColumn(
                name: "ScoredAt",
                table: "job_listings");
        }
    }
}
