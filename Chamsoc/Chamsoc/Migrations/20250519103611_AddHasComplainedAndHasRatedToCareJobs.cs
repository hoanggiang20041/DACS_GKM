using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamsoc.Migrations
{
    /// <inheritdoc />
    public partial class AddHasComplainedAndHasRatedToCareJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasComplained",
                table: "CareJobs",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasRated",
                table: "CareJobs",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasComplained",
                table: "CareJobs");

            migrationBuilder.DropColumn(
                name: "HasRated",
                table: "CareJobs");
        }
    }
}
