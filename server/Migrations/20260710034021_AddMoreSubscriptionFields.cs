using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    public partial class AddMoreSubscriptionFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: "Order",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Order",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEnd",
                table: "Order",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodStart",
                table: "Order",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DealerId",
                table: "Order",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "FailedPaymentAttempts",
                table: "Order",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Interval",
                table: "Order",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "IntervalCount",
                table: "Order",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastStripeEvent",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextBillingDate",
                table: "Order",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlanMode",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Order",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeBrand",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerEmail",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StripeExpMonth",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StripeExpYear",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StripeInvoiceUrl",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StripeLast4",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentMethod",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionEndDate",
                table: "Order",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionId",
                table: "Order",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEnd",
                table: "Order",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_DealerId",
                table: "Order",
                column: "DealerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Order_Dealers_DealerId",
                table: "Order",
                column: "DealerId",
                principalTable: "Dealers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Order_Dealers_DealerId",
                table: "Order");

            migrationBuilder.DropIndex(
                name: "IX_Order_DealerId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEnd",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodStart",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "DealerId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "FailedPaymentAttempts",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "Interval",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "IntervalCount",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "LastStripeEvent",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "NextBillingDate",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PlanMode",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "StripeBrand",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "StripeCustomerEmail",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "StripeExpMonth",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "StripeExpYear",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "StripeInvoiceUrl",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "StripeLast4",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "StripePaymentMethod",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "SubscriptionEndDate",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "TrialEnd",
                table: "Order");
        }
    }
}
