using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeViva.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCityMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReportedOnline = table.Column<bool>(type: "boolean", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: true),
                    Origin = table.Column<string>(type: "text", nullable: true),
                    Quality = table.Column<string>(type: "text", nullable: true),
                    MeasurementsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ActuatorsJson = table.Column<string>(type: "jsonb", nullable: false),
                    LastTelemetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devices_zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cause = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    RaisedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alerts_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "automation_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleType = table.Column<string>(type: "text", nullable: false),
                    MaxPulseMs = table.Column<int>(type: "integer", nullable: false),
                    CooldownMs = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_automation_rules_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_commands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublicationAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ObservedStateJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_commands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_commands_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Origin = table.Column<string>(type: "text", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telemetry_messages_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "command_acknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_command_acknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_command_acknowledgements_device_commands_CommandId",
                        column: x => x.CommandId,
                        principalTable: "device_commands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensor_readings_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_readings_telemetry_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "telemetry_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_DeviceId_Cause",
                table: "alerts",
                columns: new[] { "DeviceId", "Cause" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_automation_rules_DeviceId_RuleType",
                table: "automation_rules",
                columns: new[] { "DeviceId", "RuleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_command_acknowledgements_CommandId_Status",
                table: "command_acknowledgements",
                columns: new[] { "CommandId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_commands_DeviceId",
                table: "device_commands",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_device_commands_Status_RequestedAt",
                table: "device_commands",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_devices_DeviceKey",
                table: "devices",
                column: "DeviceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_ZoneId",
                table: "devices",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_DeviceId_ReceivedAt_Id",
                table: "sensor_readings",
                columns: new[] { "DeviceId", "ReceivedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_MessageId_Metric",
                table: "sensor_readings",
                columns: new[] { "MessageId", "Metric" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_messages_DeviceId_MessageId",
                table: "telemetry_messages",
                columns: new[] { "DeviceId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zones_Code",
                table: "zones",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "automation_rules");

            migrationBuilder.DropTable(
                name: "command_acknowledgements");

            migrationBuilder.DropTable(
                name: "sensor_readings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "device_commands");

            migrationBuilder.DropTable(
                name: "telemetry_messages");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "zones");
        }
    }
}
