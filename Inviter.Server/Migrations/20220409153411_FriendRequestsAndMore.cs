using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inviter.Server.Migrations
{
    public partial class FriendRequestsAndMore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_users_user_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_user_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "state",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "users");

            migrationBuilder.AddColumn<bool>(
                name: "allow_friend_requests",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "user_user",
                columns: table => new
                {
                    friend_requests_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    friends_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_user", x => new { x.friend_requests_id, x.friends_id });
                    table.ForeignKey(
                        name: "fk_user_user_users_friend_requests_id",
                        column: x => x.friend_requests_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_user_users_friends_id",
                        column: x => x.friends_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_user_friends_id",
                table: "user_user",
                column: "friends_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_user");

            migrationBuilder.DropColumn(
                name: "allow_friend_requests",
                table: "users");

            migrationBuilder.AddColumn<int>(
                name: "state",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "user_id",
                table: "users",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_user_id",
                table: "users",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_users_user_id",
                table: "users",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id");
        }
    }
}
