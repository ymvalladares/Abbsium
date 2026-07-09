using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    public partial class AddFacebookPagesToSocialAccount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultPageId",
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

            migrationBuilder.AddColumn<string>(
                name: "PageNames",
                table: "SocialAccounts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultPageId",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "PageIds",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "PageNames",
                table: "SocialAccounts");
        }
    }
}
