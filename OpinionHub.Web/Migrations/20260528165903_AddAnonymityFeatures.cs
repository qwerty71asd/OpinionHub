using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpinionHub.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAnonymityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymousAuthor",
                table: "Polls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyAnonymousPolls",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAnonymousAuthor",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "NotifyAnonymousPolls",
                table: "AspNetUsers");
        }
    }
}
