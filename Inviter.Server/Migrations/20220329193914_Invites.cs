using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Inviter.Server.Migrations
{
    public partial class Invites : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_invites_from_everyone",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Instant>(
                name: "last_seen",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: NodaTime.Instant.FromUnixTimeTicks(0L));

            migrationBuilder.AddColumn<decimal>(
                name: "user_id",
                table: "users",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    from_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    lobby = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    end = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invites", x => x.id);
                    table.ForeignKey(
                        name: "fk_invites_users_from_id",
                        column: x => x.from_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invites_users_to_id",
                        column: x => x.to_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_user_id",
                table: "users",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_invites_from_id",
                table: "invites",
                column: "from_id");

            migrationBuilder.CreateIndex(
                name: "ix_invites_to_id",
                table: "invites",
                column: "to_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_users_user_id",
                table: "users",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_users_user_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "invites");

            migrationBuilder.DropIndex(
                name: "ix_users_user_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "allow_invites_from_everyone",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_seen",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "users");
        }
    }
}
