using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Maestro.Data.Migrations
{
    public partial class addRepositoryPolicy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    RepositoryName = table.Column<string>(maxLength: 450, nullable: false),
                    InstallationId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.RepositoryName);
                });

            migrationBuilder.Sql(@"
INSERT INTO [Repositories](RepositoryName, InstallationId)
SELECT Repository as RepositoryName, InstallationId
FROM [RepoInstallations]");

            migrationBuilder.DropTable(
                name: "RepoInstallations");

            migrationBuilder.CreateTable(
                name: "RepositoryBranchUpdateHistory",
                columns: table => new
                {
                    RepositoryName = table.Column<string>(maxLength: 450, nullable: false),
                    BranchName = table.Column<string>(maxLength: 450, nullable: false),
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
                    table.PrimaryKey("PK_RepositoryBranchUpdateHistory", x => new { x.RepositoryName, x.BranchName });
                })
                .Annotation("SqlServer:HistoryTable", true);

            migrationBuilder.CreateTable(
                name: "RepositoryBranches",
                columns: table => new
                {
                    RepositoryName = table.Column<string>(maxLength: 450, nullable: false),
                    BranchName = table.Column<string>(maxLength: 450, nullable: false),
                    Policy = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryBranches", x => new { x.RepositoryName, x.BranchName });
                    table.ForeignKey(
                        name: "FK_RepositoryBranches_Repositories_RepositoryName",
                        column: x => x.RepositoryName,
                        principalTable: "Repositories",
                        principalColumn: "RepositoryName",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryBranchUpdates",
                columns: table => new
                {
                    RepositoryName = table.Column<string>(maxLength: 450, nullable: false),
                    BranchName = table.Column<string>(maxLength: 450, nullable: false),
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
                    table.PrimaryKey("PK_RepositoryBranchUpdates", x => new { x.RepositoryName, x.BranchName });
                    table.ForeignKey(
                        name: "FK_RepositoryBranchUpdates_RepositoryBranches_RepositoryName_BranchName",
                        columns: x => new { x.RepositoryName, x.BranchName },
                        principalTable: "RepositoryBranches",
                        principalColumns: new[] { "RepositoryName", "BranchName" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:HistoryRetentionPeriod", "3 MONTHS")
                .Annotation("SqlServer:SystemVersioned", "Maestro.Data.Models.RepositoryBranchUpdateHistory");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryBranchUpdateHistory_RepositoryName_BranchName",
                table: "RepositoryBranchUpdateHistory",
                columns: new[] { "RepositoryName", "BranchName" })
                .Annotation("SqlServer:Clustered", true)
                .Annotation("SqlServer:ColumnstoreIndex", true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryBranchUpdateHistory");

            migrationBuilder.DropTable(
                name: "RepositoryBranchUpdates");

            migrationBuilder.DropTable(
                name: "RepositoryBranches");

            migrationBuilder.DropTable(
                name: "Repositories");

            migrationBuilder.CreateTable(
                name: "RepoInstallations",
                columns: table => new
                {
                    Repository = table.Column<string>(nullable: false),
                    InstallationId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepoInstallations", x => x.Repository);
                });
        }
    }
}
