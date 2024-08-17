using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayPalPaymentWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPayementId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentId",
                table: "PaymentTokens",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "PaymentTokens");
        }
    }
}
