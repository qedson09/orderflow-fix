using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrderFlow.Infrastructure.Persistence.Migrations.Accumulator
{
    /// <inheritdoc />
    public partial class InitialAccumulator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "accumulator");

            migrationBuilder.CreateTable(
                name: "Exposures",
                schema: "accumulator",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exposures", x => x.Symbol);
                    table.CheckConstraint("CK_Exposure_Limit", "abs(\"Amount\") <= 100000000");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "accumulator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionKey = table.Column<string>(type: "text", nullable: false),
                    ClOrdId = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Side = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExecId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    InputError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "accumulator",
                table: "Exposures",
                columns: new[] { "Symbol", "Amount", "Version" },
                values: new object[,]
                {
                    { "PETR4", 0m, 0L },
                    { "VALE3", 0m, 0L },
                    { "VIIA4", 0m, 0L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SessionKey_ClOrdId",
                schema: "accumulator",
                table: "Orders",
                columns: new[] { "SessionKey", "ClOrdId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Exposures",
                schema: "accumulator");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "accumulator");
        }
    }
}
