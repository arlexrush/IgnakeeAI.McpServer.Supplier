using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Migrations;

[Migration("202603190001_InitialCatalog")]
[DbContext(typeof(SupplierCatalogDbContext))]
public partial class InitialCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false),
                ItemCode = table.Column<string>(maxLength: 100, nullable: false),
                Description = table.Column<string>(maxLength: 500, nullable: false),
                Category = table.Column<string>(maxLength: 100, nullable: false),
                Keywords = table.Column<string>(maxLength: 1000, nullable: false),
                Unit = table.Column<string>(maxLength: 20, nullable: false),
                UnitPrice = table.Column<decimal>(nullable: false),
                Currency = table.Column<string>(maxLength: 3, nullable: false),
                PackSize = table.Column<decimal>(nullable: true),
                PackPrice = table.Column<decimal>(nullable: true),
                Specification = table.Column<string>(maxLength: 500, nullable: true),
                Presentation = table.Column<string>(maxLength: 200, nullable: true),
                AvailableStock = table.Column<int>(nullable: true),
                LeadTimeDays = table.Column<int>(nullable: true),
                ProductUrl = table.Column<string>(maxLength: 1000, nullable: true),
                IsOnSale = table.Column<bool>(nullable: false),
                SalePrice = table.Column<decimal>(nullable: true),
                QualityRating = table.Column<int>(nullable: true),
                IsSubstitute = table.Column<bool>(nullable: false),
                ValidUntil = table.Column<DateTime>(nullable: true),
                UpdatedAt = table.Column<DateTime>(nullable: false),
                IsActive = table.Column<bool>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Products", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "ix_product_category_active",
            table: "Products",
            columns: new[] { "Category", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "ix_product_itemcode",
            table: "Products",
            column: "ItemCode");

        migrationBuilder.CreateTable(
            name: "CatalogSyncAudits",
            columns: table => new
            {
                SyncId = table.Column<Guid>(nullable: false),
                Source = table.Column<string>(maxLength: 32, nullable: false),
                ErpProvider = table.Column<string>(maxLength: 64, nullable: true),
                ProductsRead = table.Column<int>(nullable: false),
                ProductsCreated = table.Column<int>(nullable: false),
                ProductsUpdated = table.Column<int>(nullable: false),
                ProductsRejected = table.Column<int>(nullable: false),
                StartedAt = table.Column<DateTimeOffset>(nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(nullable: false),
                Succeeded = table.Column<bool>(nullable: false),
                Error = table.Column<string>(maxLength: 4000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_CatalogSyncAudits", x => x.SyncId));

        migrationBuilder.CreateIndex(
            name: "IX_CatalogSyncAudits_StartedAt",
            table: "CatalogSyncAudits",
            column: "StartedAt");

        migrationBuilder.CreateIndex(
            name: "IX_CatalogSyncAudits_Source_CompletedAt",
            table: "CatalogSyncAudits",
            columns: new[] { "Source", "CompletedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CatalogSyncAudits");
        migrationBuilder.DropTable(name: "Products");
    }
}
