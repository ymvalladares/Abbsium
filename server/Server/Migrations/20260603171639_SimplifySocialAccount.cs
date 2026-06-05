using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    public partial class SimplifySocialAccount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultPageId",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "PageAccessTokens",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "PageIds",
                table: "SocialAccounts");

            migrationBuilder.RenameColumn(
                name: "PageNames",
                table: "SocialAccounts",
                newName: "AccountName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccountName",
                table: "SocialAccounts",
                newName: "PageNames");

            migrationBuilder.AddColumn<string>(
                name: "DefaultPageId",
                table: "SocialAccounts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PageAccessTokens",
                table: "SocialAccounts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PageIds",
                table: "SocialAccounts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
