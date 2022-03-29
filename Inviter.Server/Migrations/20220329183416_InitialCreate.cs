using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inviter.Server.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    country = table.Column<string>(type: "text", nullable: true),
                    profile_picture = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
