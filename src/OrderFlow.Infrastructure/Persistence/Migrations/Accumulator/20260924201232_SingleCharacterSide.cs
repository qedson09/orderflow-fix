using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations.Accumulator
{
    /// <inheritdoc />
    public partial class SingleCharacterSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE accumulator."Orders"
                SET "Side" = CASE "Side" WHEN 'Buy' THEN 'B' WHEN 'Sell' THEN 'S' ELSE "Side" END;
                """);
            migrationBuilder.AlterColumn<string>(
                name: "Side",
                schema: "accumulator",
                table: "Orders",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Side",
                schema: "accumulator",
                table: "Orders",
                sql: "\"Side\" IN ('B', 'S')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Side",
                schema: "accumulator",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "Side",
                schema: "accumulator",
                table: "Orders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1)",
                oldMaxLength: 1);
            migrationBuilder.Sql("""
                UPDATE accumulator."Orders"
                SET "Side" = CASE "Side" WHEN 'B' THEN 'Buy' WHEN 'S' THEN 'Sell' ELSE "Side" END;
                """);
        }
    }
}
