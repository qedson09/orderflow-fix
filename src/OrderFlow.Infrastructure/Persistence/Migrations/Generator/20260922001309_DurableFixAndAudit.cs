using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations.Generator
{
    /// <inheritdoc />
    public partial class DurableFixAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FixOutbox",
                schema: "generator",
                columns: table => new
                {
                    ClOrdId = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixOutbox", x => x.ClOrdId);
                    table.ForeignKey(
                        name: "FK_FixOutbox_Submissions_ClOrdId",
                        column: x => x.ClOrdId,
                        principalSchema: "generator",
                        principalTable: "Submissions",
                        principalColumn: "ClOrdId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "generator",
                columns: table => new
                {
                    Consumer = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "text", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.Consumer, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "OrderEventProjections",
                schema: "generator",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClOrdId = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    SymbolSequence = table.Column<long>(type: "bigint", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEventProjections", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "QuarantinedMessages",
                schema: "generator",
                columns: table => new
                {
                    Topic = table.Column<string>(type: "text", nullable: false),
                    Partition = table.Column<int>(type: "integer", nullable: false),
                    Offset = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuarantinedMessages", x => new { x.Topic, x.Partition, x.Offset });
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixOutbox_CompletedAt_NextAttemptAt",
                schema: "generator",
                table: "FixOutbox",
                columns: new[] { "CompletedAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderEventProjections_ClOrdId_OccurredAt",
                schema: "generator",
                table: "OrderEventProjections",
                columns: new[] { "ClOrdId", "OccurredAt" });
            migrationBuilder.Sql("""
                INSERT INTO generator."FixOutbox" ("ClOrdId", "Attempts", "NextAttemptAt", "CompletedAt")
                SELECT "ClOrdId", 0, CURRENT_TIMESTAMP, NULL
                FROM generator."Submissions" WHERE "Status" = 'Pending'
                ON CONFLICT ("ClOrdId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixOutbox",
                schema: "generator");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "generator");

            migrationBuilder.DropTable(
                name: "OrderEventProjections",
                schema: "generator");

            migrationBuilder.DropTable(
                name: "QuarantinedMessages",
                schema: "generator");
        }
    }
}
