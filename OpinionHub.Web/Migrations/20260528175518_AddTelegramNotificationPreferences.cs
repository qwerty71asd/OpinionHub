using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpinionHub.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnCommentOnMyPoll",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnLikeMilestone",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Default true для обратной совместимости: существующая рассылка
            // NotifySubscribersOfNewPollAsync продолжит работать для уже зарегистрированных
            // пользователей без необходимости вручную включать переключатель.
            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnNewPollFromSubscription",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnNewSubscriber",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnReplyToMyComment",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyOnCommentOnMyPoll",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyOnLikeMilestone",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyOnNewPollFromSubscription",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyOnNewSubscriber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyOnReplyToMyComment",
                table: "AspNetUsers");
        }
    }
}
