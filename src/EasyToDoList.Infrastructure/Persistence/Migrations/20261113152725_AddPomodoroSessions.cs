using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyToDoList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPomodoroSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PomodoroSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Phase = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FocusMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ShortBreakMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LongBreakMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LongBreakInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    RemainingSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PlannedEndAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PausedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedFocusCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RewardAppliedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PauseReason = table.Column<int>(type: "INTEGER", nullable: true),
                    RowVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PomodoroSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PomodoroSessions_Status",
                table: "PomodoroSessions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PomodoroSessions");
        }
    }
}
