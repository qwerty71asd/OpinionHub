using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpinionHub.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPollMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImagePath",
                table: "Polls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "PollOptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PollAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollAttachments_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PollAttachments_PollId",
                table: "PollAttachments",
                column: "PollId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PollAttachments");

            migrationBuilder.DropColumn(
                name: "CoverImagePath",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "PollOptions");
        }
    }
}
