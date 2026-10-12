using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SimplCommerce.Module.Core.Data;

#nullable disable

namespace SimplCommerce.WebHost.Migrations
{
    [DbContext(typeof(SimplDbContext))]
    [Migration("20241014090000_DropReviewerNameFromReview")]
    public partial class DropReviewerNameFromReview : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewerName",
                table: "Reviews_Review");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewerName",
                table: "Reviews_Review",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }
    }
}
