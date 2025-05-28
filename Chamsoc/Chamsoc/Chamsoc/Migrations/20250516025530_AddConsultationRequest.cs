using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamsoc.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Contact",
                table: "Seniors");

            migrationBuilder.CreateTable(
                name: "ConsultationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaregiverId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequesterId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultationRequests");

            migrationBuilder.AddColumn<string>(
                name: "Contact",
                table: "Seniors",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
