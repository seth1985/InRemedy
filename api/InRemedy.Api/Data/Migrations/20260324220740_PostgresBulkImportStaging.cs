using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InRemedy.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PostgresBulkImportStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS pgcrypto;""");

            migrationBuilder.DropIndex(
                name: "IX_RemediationResults_RemediationId_DeviceId_RunTimestampUtc",
                table: "RemediationResults");

            migrationBuilder.CreateTable(
                name: "ImportStagingRows",
                columns: table => new
                {
                    ImportStagingRowId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    RemediationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DetectionScriptVersion = table.Column<string>(type: "text", nullable: false),
                    RemediationScriptVersion = table.Column<string>(type: "text", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrimaryUser = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OsVersion = table.Column<string>(type: "text", nullable: false),
                    OsBuild = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "text", nullable: false),
                    UpdateRing = table.Column<string>(type: "text", nullable: false),
                    LastSyncDateTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RunTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DetectionOutputRaw = table.Column<string>(type: "text", nullable: false),
                    RemediationOutputRaw = table.Column<string>(type: "text", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true),
                    OutputCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScriptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataSource = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportStagingRows", x => x.ImportStagingRowId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationResults_RemediationId_DeviceId_RunTimestampUtc",
                table: "RemediationResults",
                columns: new[] { "RemediationId", "DeviceId", "RunTimestampUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportStagingRows_ImportBatchId",
                table: "ImportStagingRows",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportStagingRows_ImportBatchId_RowNumber",
                table: "ImportStagingRows",
                columns: new[] { "ImportBatchId", "RowNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportStagingRows");

            migrationBuilder.DropIndex(
                name: "IX_RemediationResults_RemediationId_DeviceId_RunTimestampUtc",
                table: "RemediationResults");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationResults_RemediationId_DeviceId_RunTimestampUtc",
                table: "RemediationResults",
                columns: new[] { "RemediationId", "DeviceId", "RunTimestampUtc" });
        }
    }
}
