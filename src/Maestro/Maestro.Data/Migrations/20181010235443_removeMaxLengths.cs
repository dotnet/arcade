using Microsoft.EntityFrameworkCore.Migrations;

namespace Maestro.Data.Migrations
{
    public partial class removeMaxLengths : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [RepositoryBranchUpdates] SET (SYSTEM_VERSIONING = OFF)");
            migrationBuilder.Sql("ALTER TABLE [SubscriptionUpdates] SET (SYSTEM_VERSIONING = OFF)");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionUpdateHistory_SubscriptionId",
                table: "SubscriptionUpdateHistory");

            migrationBuilder.DropIndex(
                name: "IX_RepositoryBranchUpdateHistory_RepositoryName_BranchName",
                table: "RepositoryBranchUpdateHistory");

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "SubscriptionUpdates",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "SubscriptionUpdates",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "SubscriptionUpdates",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "SubscriptionUpdates",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "SubscriptionUpdateHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "SubscriptionUpdateHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "SubscriptionUpdateHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "SubscriptionUpdateHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "RepositoryBranchUpdates",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "RepositoryBranchUpdates",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "RepositoryBranchUpdates",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "RepositoryBranchUpdates",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "RepositoryBranchUpdateHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "RepositoryBranchUpdateHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "RepositoryBranchUpdateHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "RepositoryBranchUpdateHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUpdateHistory_SysEndTime_SysStartTime",
                table: "SubscriptionUpdateHistory",
                columns: new[] { "SysEndTime", "SysStartTime" })
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUpdateHistory_SubscriptionId_SysEndTime_SysStartTime",
                table: "SubscriptionUpdateHistory",
                columns: new[] { "SubscriptionId", "SysEndTime", "SysStartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryBranchUpdateHistory_SysEndTime_SysStartTime",
                table: "RepositoryBranchUpdateHistory",
                columns: new[] { "SysEndTime", "SysStartTime" })
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryBranchUpdateHistory_RepositoryName_BranchName_SysEndTime_SysStartTime",
                table: "RepositoryBranchUpdateHistory",
                columns: new[] { "RepositoryName", "BranchName", "SysEndTime", "SysStartTime" });

            migrationBuilder.Sql("ALTER TABLE [RepositoryBranchUpdates] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[RepositoryBranchUpdateHistory], HISTORY_RETENTION_PERIOD = 1 MONTHS))");
            migrationBuilder.Sql("ALTER TABLE [SubscriptionUpdates] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[SubscriptionUpdateHistory], HISTORY_RETENTION_PERIOD = 1 MONTHS))");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionUpdateHistory_SysEndTime_SysStartTime",
                table: "SubscriptionUpdateHistory");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionUpdateHistory_SubscriptionId_SysEndTime_SysStartTime",
                table: "SubscriptionUpdateHistory");

            migrationBuilder.DropIndex(
                name: "IX_RepositoryBranchUpdateHistory_SysEndTime_SysStartTime",
                table: "RepositoryBranchUpdateHistory");

            migrationBuilder.DropIndex(
                name: "IX_RepositoryBranchUpdateHistory_RepositoryName_BranchName_SysEndTime_SysStartTime",
                table: "RepositoryBranchUpdateHistory");

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "SubscriptionUpdates",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "SubscriptionUpdates",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "SubscriptionUpdates",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "SubscriptionUpdates",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "SubscriptionUpdateHistory",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "SubscriptionUpdateHistory",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "SubscriptionUpdateHistory",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "SubscriptionUpdateHistory",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "RepositoryBranchUpdates",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "RepositoryBranchUpdates",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "RepositoryBranchUpdates",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "RepositoryBranchUpdates",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "RepositoryBranchUpdateHistory",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "RepositoryBranchUpdateHistory",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "RepositoryBranchUpdateHistory",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "RepositoryBranchUpdateHistory",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUpdateHistory_SubscriptionId",
                table: "SubscriptionUpdateHistory",
                column: "SubscriptionId")
                .Annotation("SqlServer:Clustered", true)
                .Annotation("SqlServer:ColumnstoreIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryBranchUpdateHistory_RepositoryName_BranchName",
                table: "RepositoryBranchUpdateHistory",
                columns: new[] { "RepositoryName", "BranchName" })
                .Annotation("SqlServer:Clustered", true)
                .Annotation("SqlServer:ColumnstoreIndex", true);
        }
    }
}
