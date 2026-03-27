using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InRemedy.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrimaryUser = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Manufacturer = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OsVersion = table.Column<string>(type: "text", nullable: false),
                    OsBuild = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "text", nullable: false),
                    UpdateRing = table.Column<string>(type: "text", nullable: false),
                    LastSyncDateTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileHashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImportType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ImportedRows = table.Column<int>(type: "integer", nullable: false),
                    ErrorRows = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DuplicateOfImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.ImportBatchId);
                });

            migrationBuilder.CreateTable(
                name: "Remediations",
                columns: table => new
                {
                    RemediationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemediationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActiveFlag = table.Column<bool>(type: "boolean", nullable: false),
                    DetectionScriptVersion = table.Column<string>(type: "text", nullable: false),
                    RemediationScriptVersion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Remediations", x => x.RemediationId);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    SavedViewId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    ViewDefinitionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.SavedViewId);
                });

            migrationBuilder.CreateTable(
                name: "ImportErrors",
                columns: table => new
                {
                    ImportErrorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ColumnName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RowSnapshotJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportErrors", x => x.ImportErrorId);
                    table.ForeignKey(
                        name: "FK_ImportErrors_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "ImportBatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemediationResults",
                columns: table => new
                {
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemediationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RemediationAttemptedFlag = table.Column<bool>(type: "boolean", nullable: false),
                    RemediationSucceededFlag = table.Column<bool>(type: "boolean", nullable: false),
                    DetectionOutputRaw = table.Column<string>(type: "text", nullable: false),
                    RemediationOutputRaw = table.Column<string>(type: "text", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true),
                    OutputCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScriptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataSource = table.Column<string>(type: "text", nullable: false),
                    IngestionTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationResults", x => x.ResultId);
                    table.ForeignKey(
                        name: "FK_RemediationResults_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemediationResults_Remediations_RemediationId",
                        column: x => x.RemediationId,
                        principalTable: "Remediations",
                        principalColumn: "RemediationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceName",
                table: "Devices",
                column: "DeviceName");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_FileHashSha256",
                table: "ImportBatches",
                column: "FileHashSha256");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_StartedUtc",
                table: "ImportBatches",
                column: "StartedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ImportErrors_ImportBatchId",
                table: "ImportErrors",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationResults_DeviceId",
                table: "RemediationResults",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationResults_RemediationId_DeviceId_RunTimestampUtc",
                table: "RemediationResults",
                columns: new[] { "RemediationId", "DeviceId", "RunTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Remediations_RemediationName",
                table: "Remediations",
                column: "RemediationName");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_OwnerUserId_PageType_IsDefault",
                table: "SavedViews",
                columns: new[] { "OwnerUserId", "PageType", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportErrors");

            migrationBuilder.DropTable(
                name: "RemediationResults");

            migrationBuilder.DropTable(
                name: "SavedViews");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Remediations");
        }
    }
}
