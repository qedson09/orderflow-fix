using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations.Accumulator
{
    /// <inheritdoc />
    public partial class DurableOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DecisionSequence",
                schema: "accumulator",
                table: "Exposures",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "accumulator",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionKey = table.Column<string>(type: "text", nullable: false),
                    SymbolSequence = table.Column<long>(type: "bigint", nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.EventId);
                });

            migrationBuilder.UpdateData(
                schema: "accumulator",
                table: "Exposures",
                keyColumn: "Symbol",
                keyValue: "PETR4",
                column: "DecisionSequence",
                value: 0L);

            migrationBuilder.UpdateData(
                schema: "accumulator",
                table: "Exposures",
                keyColumn: "Symbol",
                keyValue: "VALE3",
                column: "DecisionSequence",
                value: 0L);

            migrationBuilder.UpdateData(
                schema: "accumulator",
                table: "Exposures",
                keyColumn: "Symbol",
                keyValue: "VIIA4",
                column: "DecisionSequence",
                value: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PartitionKey_SymbolSequence",
                schema: "accumulator",
                table: "OutboxMessages",
                columns: new[] { "PartitionKey", "SymbolSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_NextAttemptAt",
                schema: "accumulator",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "accumulator");

            migrationBuilder.DropColumn(
                name: "DecisionSequence",
                schema: "accumulator",
                table: "Exposures");
        }
    }
}
