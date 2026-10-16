using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TidyUp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationJournalAndRuleRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationJournal",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OriginalPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    TargetPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PreActionHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PostActionHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RolledBackAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RuleName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RuleRevisionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationJournal", x => x.OperationId);
                });

            migrationBuilder.CreateTable(
                name: "RuleRevisions",
                columns: table => new
                {
                    RevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SerializedRuleJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangeDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleRevisions", x => x.RevisionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationJournal_BatchId",
                table: "OperationJournal",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationJournal_OriginalPath",
                table: "OperationJournal",
                column: "OriginalPath");

            migrationBuilder.CreateIndex(
                name: "IX_OperationJournal_Status",
                table: "OperationJournal",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationJournal_TargetPath",
                table: "OperationJournal",
                column: "TargetPath");

            migrationBuilder.CreateIndex(
                name: "IX_OperationJournal_Timestamp",
                table: "OperationJournal",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_RuleRevisions_RuleId",
                table: "RuleRevisions",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleRevisions_RuleId_VersionNumber",
                table: "RuleRevisions",
                columns: new[] { "RuleId", "VersionNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationJournal");

            migrationBuilder.DropTable(
                name: "RuleRevisions");
        }
    }
}
