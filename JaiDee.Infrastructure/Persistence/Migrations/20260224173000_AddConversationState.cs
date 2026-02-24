using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JaiDee.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260224173000_AddConversationState")]
    public partial class AddConversationState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineUserId = table.Column<string>(type: "text", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    PendingType = table.Column<int>(type: "integer", nullable: true),
                    PendingNote = table.Column<string>(type: "text", nullable: false),
                    PendingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStates_LineUserId",
                table: "ConversationStates",
                column: "LineUserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationStates");
        }
    }
}
