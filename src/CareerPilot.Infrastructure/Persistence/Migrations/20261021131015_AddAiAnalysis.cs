using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiAnalysisJson",
                table: "job_listings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiAnalyzedAt",
                table: "job_listings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAnalysisJson",
                table: "job_listings");

            migrationBuilder.DropColumn(
                name: "AiAnalyzedAt",
                table: "job_listings");
        }
    }
}
