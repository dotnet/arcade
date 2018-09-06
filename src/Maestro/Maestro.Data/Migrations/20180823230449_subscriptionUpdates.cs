using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Maestro.Data.Migrations
{
    public partial class subscriptionUpdates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionUpdateHistory",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(nullable: false),
                    Success = table.Column<bool>(nullable: false),
                    Action = table.Column<string>(maxLength: 450, nullable: true),
                    ErrorMessage = table.Column<string>(maxLength: 450, nullable: true),
                    Method = table.Column<string>(maxLength: 450, nullable: true),
                    Arguments = table.Column<string>(maxLength: 450, nullable: true),
                    SysEndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysStartTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionUpdateHistory", x => x.SubscriptionId);
                })
                .Annotation("SqlServer:HistoryTable", true);

            migrationBuilder.CreateTable(
                name: "SubscriptionUpdates",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(nullable: false),
                    Success = table.Column<bool>(nullable: false),
                    Action = table.Column<string>(maxLength: 450, nullable: true),
                    ErrorMessage = table.Column<string>(maxLength: 450, nullable: true),
                    Method = table.Column<string>(maxLength: 450, nullable: true),
                    Arguments = table.Column<string>(maxLength: 450, nullable: true),
                    SysEndTime = table.Column<DateTime>(type: "datetime2 GENERATED ALWAYS AS ROW END", nullable: false),
                    SysStartTime = table.Column<DateTime>(type: "datetime2 GENERATED ALWAYS AS ROW START", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionUpdates", x => x.SubscriptionId);
                    table.ForeignKey(
                        name: "FK_SubscriptionUpdates_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:HistoryRetentionPeriod", "1 MONTHS")
                .Annotation("SqlServer:SystemVersioned", "Maestro.Data.Models.SubscriptionUpdateHistory");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUpdateHistory_SubscriptionId",
                table: "SubscriptionUpdateHistory",
                column: "SubscriptionId")
                .Annotation("SqlServer:Clustered", true)
                .Annotation("SqlServer:ColumnstoreIndex", true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionUpdateHistory");

            migrationBuilder.DropTable(
                name: "SubscriptionUpdates");
        }
    }
}
