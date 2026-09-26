using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations.Accumulator;

public partial class CustomerExposure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve balances, versions and sequences. Never delete/reseed the first account.
        migrationBuilder.Sql("""
            CREATE TABLE accumulator."Customers" ("Id" text NOT NULL, "Name" text NOT NULL, CONSTRAINT "PK_Customers" PRIMARY KEY ("Id"));
            INSERT INTO accumulator."Customers" VALUES
              ('CLIENTE-001', 'Cliente demonstração 1'), ('CLIENTE-002', 'Cliente demonstração 2');
            ALTER TABLE accumulator."Orders" ADD COLUMN "AccountId" text NOT NULL DEFAULT 'CLIENTE-001';
            ALTER TABLE accumulator."Exposures" ADD COLUMN "AccountId" text NOT NULL DEFAULT 'CLIENTE-001';
            ALTER TABLE accumulator."Exposures" DROP CONSTRAINT "PK_Exposures";
            ALTER TABLE accumulator."Exposures" ADD CONSTRAINT "PK_Exposures" PRIMARY KEY ("AccountId", "Symbol");
            ALTER TABLE accumulator."Exposures" ADD CONSTRAINT "FK_Exposures_Customers_AccountId"
              FOREIGN KEY ("AccountId") REFERENCES accumulator."Customers" ("Id") ON DELETE CASCADE;
            INSERT INTO accumulator."Exposures" ("AccountId", "Symbol", "Amount", "Version", "DecisionSequence")
              VALUES ('CLIENTE-002','PETR4',0,0,0), ('CLIENTE-002','VALE3',0,0,0), ('CLIENTE-002','VIIA4',0,0,0);
            UPDATE accumulator."OutboxMessages" SET "PartitionKey" = 'CLIENTE-001:' || "PartitionKey"
              WHERE "PartitionKey" IN ('PETR4','VALE3','VIIA4');
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("Migração financeira sem downgrade automático. Restaure um backup consistente anterior à v2.");
    }
}
