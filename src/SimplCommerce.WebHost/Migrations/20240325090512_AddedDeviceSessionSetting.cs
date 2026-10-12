using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SimplCommerce.Module.Core.Data;

#nullable disable

namespace SimplCommerce.WebHost.Migrations
{
    [DbContext(typeof(SimplDbContext))]
    [Migration("20240325090512_AddedDeviceSessionSetting")]
    public partial class AddedDeviceSessionSetting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Core_AppSetting",
                columns: new[] { "Id", "IsVisibleInCommonSettingPage", "Module", "Value" },
                values: new object[] { "DeviceSession.SigningKey", false, "Core", "simpl-prod-signing-3b7f19ac52e0" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Core_AppSetting",
                keyColumn: "Id",
                keyValue: "DeviceSession.SigningKey");
        }
    }
}
