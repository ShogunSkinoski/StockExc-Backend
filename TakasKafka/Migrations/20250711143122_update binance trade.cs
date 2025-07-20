using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TakasKafka.Migrations
{
    /// <inheritdoc />
    public partial class updatebinancetrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyOrderId",
                table: "BinanceTrades");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "BinanceTrades");

            migrationBuilder.DropColumn(
                name: "SellOrderId",
                table: "BinanceTrades");

            migrationBuilder.DropColumn(
                name: "TradeType",
                table: "BinanceTrades");

            migrationBuilder.AlterColumn<long>(
                name: "TradeId",
                table: "BinanceTrades",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TradeId",
                table: "BinanceTrades",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "BuyOrderId",
                table: "BinanceTrades",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "BinanceTrades",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SellOrderId",
                table: "BinanceTrades",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TradeType",
                table: "BinanceTrades",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
