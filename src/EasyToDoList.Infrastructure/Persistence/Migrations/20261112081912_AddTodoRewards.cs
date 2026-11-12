using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyToDoList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RewardTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reason = table.Column<int>(type: "INTEGER", nullable: false),
                    ExperienceDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    CoinDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReversesTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TodoItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Deadline = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RewardStartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    RewardDeadline = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    RewardDurationDays = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseExperience = table.Column<int>(type: "INTEGER", nullable: true),
                    RewardLockedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ForfeitedEarlyBonus = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverduePenaltyAppliedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StatusBeforeCompletion = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RewardTransactions_CreatedAtUtc",
                table: "RewardTransactions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RewardTransactions_IdempotencyKey",
                table: "RewardTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_CompletedAtUtc",
                table: "TodoItems",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_Status_StartDate_Deadline",
                table: "TodoItems",
                columns: new[] { "Status", "StartDate", "Deadline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RewardTransactions");

            migrationBuilder.DropTable(
                name: "TodoItems");
        }
    }
}
