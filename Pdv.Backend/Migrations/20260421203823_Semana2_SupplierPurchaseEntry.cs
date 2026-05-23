using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pdv.Backend.Migrations
{
    /// <inheritdoc />
    public partial class Semana2_SupplierPurchaseEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Cnpj = table.Column<string>(type: "TEXT", maxLength: 18, nullable: true),
                    ContactName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    TotalCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ReceivedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseEntries_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseEntryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PurchaseEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseEntryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseEntryItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseEntryItems_PurchaseEntries_PurchaseEntryId",
                        column: x => x.PurchaseEntryId,
                        principalTable: "PurchaseEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Status_CashSessionId",
                table: "Sales",
                columns: new[] { "Status", "CashSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseEntries_CreatedAt",
                table: "PurchaseEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseEntries_SupplierId",
                table: "PurchaseEntries",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseEntryItems_ProductId",
                table: "PurchaseEntryItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseEntryItems_PurchaseEntryId",
                table: "PurchaseEntryItems",
                column: "PurchaseEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Cnpj",
                table: "Suppliers",
                column: "Cnpj",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseEntryItems");

            migrationBuilder.DropTable(
                name: "PurchaseEntries");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Status_CashSessionId",
                table: "Sales");
        }
    }
}
