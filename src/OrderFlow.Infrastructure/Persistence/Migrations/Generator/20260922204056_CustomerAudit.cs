using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations.Generator
{
    /// <inheritdoc />
    public partial class CustomerAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountId",
                schema: "generator",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "CLIENTE-001");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "generator",
                table: "OrderEventProjections",
                type: "text",
                nullable: false,
                defaultValue: "Kafka");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_AccountId_CreatedAt",
                schema: "generator",
                table: "Submissions",
                columns: new[] { "AccountId", "CreatedAt" });
            // Only recover facts backed by persisted timestamps, never invented send attempts.
            migrationBuilder.Sql("""
                INSERT INTO generator."OrderEventProjections"
                  ("EventId", "ClOrdId", "Symbol", "SymbolSequence", "OccurredAt", "Type", "Description", "Source")
                SELECT md5("ClOrdId" || ':submission')::uuid, "ClOrdId", "Symbol", NULL, "CreatedAt",
                  'SubmissionPersisted', 'Solicitação persistida (histórico recuperado na migração v2).', 'Generator'
                FROM generator."Submissions" ON CONFLICT ("EventId") DO NOTHING;
                INSERT INTO generator."OrderEventProjections"
                  ("EventId", "ClOrdId", "Symbol", "SymbolSequence", "OccurredAt", "Type", "Description", "Source")
                SELECT md5("ClOrdId" || ':report')::uuid, "ClOrdId", "Symbol", NULL, "UpdatedAt",
                  'ExecutionReportPersisted', 'ExecutionReport FIX persistido: ' || "Status" || ' (histórico recuperado).', 'Generator'
                FROM generator."Submissions" WHERE "ExecId" IS NOT NULL AND "UpdatedAt" IS NOT NULL
                ON CONFLICT ("EventId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_AccountId_CreatedAt",
                schema: "generator",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                schema: "generator",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "generator",
                table: "OrderEventProjections");
        }
    }
}
