using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SQCScanner.Migrations
{
    /// <inheritdoc />
    public partial class StaticImgClassUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "discription",
                table: "StaticImgClass",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "discription",
                table: "StaticImgClass");
        }
    }
}
