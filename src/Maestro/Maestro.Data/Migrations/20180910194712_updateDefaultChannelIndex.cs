// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.EntityFrameworkCore.Migrations;

namespace Maestro.Data.Migrations
{
    public partial class updateDefaultChannelIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DefaultChannels_Repository_Branch",
                table: "DefaultChannels");

            migrationBuilder.CreateIndex(
                name: "IX_DefaultChannels_Repository_Branch_ChannelId",
                table: "DefaultChannels",
                columns: new[] { "Repository", "Branch", "ChannelId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DefaultChannels_Repository_Branch_ChannelId",
                table: "DefaultChannels");

            migrationBuilder.CreateIndex(
                name: "IX_DefaultChannels_Repository_Branch",
                table: "DefaultChannels",
                columns: new[] { "Repository", "Branch" },
                unique: true);
        }
    }
}
