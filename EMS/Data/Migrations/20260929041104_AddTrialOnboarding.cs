using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrialOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Industry",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OperatesInShifts",
                table: "Organizations",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrialEndsOn",
                table: "Organizations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySalary",
                table: "Employees",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_OwnerUserId",
                table: "Organizations",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Organizations_AspNetUsers_OwnerUserId",
                table: "Organizations",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Organizations_AspNetUsers_OwnerUserId",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_OwnerUserId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "OperatesInShifts",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TrialEndsOn",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "MonthlySalary",
                table: "Employees");
        }
    }
}
