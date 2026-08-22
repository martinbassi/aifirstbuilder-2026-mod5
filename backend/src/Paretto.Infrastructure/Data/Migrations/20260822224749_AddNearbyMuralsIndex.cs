using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paretto.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNearbyMuralsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Murals_Status_Latitude_Longitude",
                table: "Murals",
                columns: new[] { "Status", "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Murals_Status_Latitude_Longitude",
                table: "Murals");
        }
    }
}
