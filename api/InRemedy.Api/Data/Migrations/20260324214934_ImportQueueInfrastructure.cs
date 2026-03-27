using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InRemedy.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportQueueInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessedRows",
                table: "ImportBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StoredFilePath",
                table: "ImportBatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessedRows",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "StoredFilePath",
                table: "ImportBatches");
        }
    }
}
