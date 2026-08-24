using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    public partial class AddClippingAspectRatiosAndClipMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BestTimeToPost",
                table: "VideoClips",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Hashtags",
                table: "VideoClips",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HookText",
                table: "VideoClips",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AspectRatio",
                table: "ClipJobs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BestTimeToPost",
                table: "VideoClips");

            migrationBuilder.DropColumn(
                name: "Hashtags",
                table: "VideoClips");

            migrationBuilder.DropColumn(
                name: "HookText",
                table: "VideoClips");

            migrationBuilder.DropColumn(
                name: "AspectRatio",
                table: "ClipJobs");
        }
    }
}
