using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpinionHub.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Polls",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Polls");
        }
    }
}
