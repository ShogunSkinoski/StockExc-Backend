using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TakasKafka.Migrations
{
    /// <inheritdoc />
    public partial class addbinancetrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BinanceTrades",
                columns: table => new
                {
                    TradeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    EventTime = table.Column<long>(type: "bigint", nullable: false),
                    TradingSymbol = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<string>(type: "text", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<string>(type: "text", nullable: false),
                    BuyOrderId = table.Column<long>(type: "bigint", nullable: false),
                    SellOrderId = table.Column<long>(type: "bigint", nullable: false),
                    TradeCompletedTime = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    TradeType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BinanceTrades", x => x.TradeId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BinanceTrades");
        }
    }
}
