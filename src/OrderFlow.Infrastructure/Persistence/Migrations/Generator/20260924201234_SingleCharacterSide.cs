using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations.Generator
{
    /// <inheritdoc />
    public partial class SingleCharacterSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE generator."Submissions"
                SET "Side" = CASE "Side" WHEN 'Buy' THEN 'B' WHEN 'Sell' THEN 'S' ELSE "Side" END;
                """);
            migrationBuilder.AlterColumn<string>(
                name: "Side",
                schema: "generator",
                table: "Submissions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submissions_Side",
                schema: "generator",
                table: "Submissions",
                sql: "\"Side\" IN ('B', 'S')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Submissions_Side",
                schema: "generator",
                table: "Submissions");

            migrationBuilder.AlterColumn<string>(
                name: "Side",
                schema: "generator",
                table: "Submissions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1)",
                oldMaxLength: 1);
            migrationBuilder.Sql("""
                UPDATE generator."Submissions"
                SET "Side" = CASE "Side" WHEN 'B' THEN 'Buy' WHEN 'S' THEN 'Sell' ELSE "Side" END;
                """);
        }
    }
}
