using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamsoc.Migrations
{
    /// <inheritdoc />
    public partial class AddCareJobFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CareJobId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "CareJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SeniorName",
                table: "CareJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SeniorPhone",
                table: "CareJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CareJobs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CareJobId",
                table: "Notifications",
                column: "CareJobId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_CareJobs_CareJobId",
                table: "Notifications",
                column: "CareJobId",
                principalTable: "CareJobs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_CareJobs_CareJobId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CareJobId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CareJobId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "CareJobs");

            migrationBuilder.DropColumn(
                name: "SeniorName",
                table: "CareJobs");

            migrationBuilder.DropColumn(
                name: "SeniorPhone",
                table: "CareJobs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CareJobs");
        }
    }
}
