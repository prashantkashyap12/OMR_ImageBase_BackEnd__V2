using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SQCScanner.Migrations
{
    /// <inheritdoc />
    public partial class StaticImgTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           
            
            migrationBuilder.CreateTable(
                name: "StaticImgClass",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateImgBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaticImgClass", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
           
        }
    }
}
