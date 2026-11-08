using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeViva.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignSqlColumnNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alerts_devices_DeviceId",
                table: "alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_automation_rules_devices_DeviceId",
                table: "automation_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_command_acknowledgements_device_commands_CommandId",
                table: "command_acknowledgements");

            migrationBuilder.DropForeignKey(
                name: "FK_device_commands_devices_DeviceId",
                table: "device_commands");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_zones_ZoneId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_sensor_readings_devices_DeviceId",
                table: "sensor_readings");

            migrationBuilder.DropForeignKey(
                name: "FK_sensor_readings_telemetry_messages_MessageId",
                table: "sensor_readings");

            migrationBuilder.DropForeignKey(
                name: "FK_system_events_devices_DeviceId",
                table: "system_events");

            migrationBuilder.DropForeignKey(
                name: "FK_telemetry_messages_devices_DeviceId",
                table: "telemetry_messages");

            migrationBuilder.DropIndex(
                name: "IX_alerts_DeviceId_Cause",
                table: "alerts");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "zones",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "zones",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "zones",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_zones_Code",
                table: "zones",
                newName: "IX_zones_code");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "users",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "users",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "users",
                newName: "password_hash");

            migrationBuilder.RenameIndex(
                name: "IX_users_Username",
                table: "users",
                newName: "IX_users_username");

            migrationBuilder.RenameColumn(
                name: "Quality",
                table: "telemetry_messages",
                newName: "quality");

            migrationBuilder.RenameColumn(
                name: "Origin",
                table: "telemetry_messages",
                newName: "origin");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "telemetry_messages",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ReceivedAt",
                table: "telemetry_messages",
                newName: "received_at");

            migrationBuilder.RenameColumn(
                name: "PayloadJson",
                table: "telemetry_messages",
                newName: "payload_json");

            migrationBuilder.RenameColumn(
                name: "ObservedAt",
                table: "telemetry_messages",
                newName: "observed_at");

            migrationBuilder.RenameColumn(
                name: "MessageId",
                table: "telemetry_messages",
                newName: "message_id");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "telemetry_messages",
                newName: "device_id");

            migrationBuilder.RenameIndex(
                name: "IX_telemetry_messages_DeviceId_MessageId",
                table: "telemetry_messages",
                newName: "IX_telemetry_messages_device_id_message_id");

            migrationBuilder.RenameColumn(
                name: "Actor",
                table: "system_events",
                newName: "actor");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "system_events",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "PayloadJson",
                table: "system_events",
                newName: "payload_json");

            migrationBuilder.RenameColumn(
                name: "OccurredAt",
                table: "system_events",
                newName: "occurred_at");

            migrationBuilder.RenameColumn(
                name: "EventType",
                table: "system_events",
                newName: "event_type");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "system_events",
                newName: "device_id");

            migrationBuilder.RenameIndex(
                name: "IX_system_events_DeviceId_OccurredAt",
                table: "system_events",
                newName: "IX_system_events_device_id_occurred_at");

            migrationBuilder.RenameColumn(
                name: "Unit",
                table: "sensor_readings",
                newName: "unit");

            migrationBuilder.RenameColumn(
                name: "Metric",
                table: "sensor_readings",
                newName: "metric");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "sensor_readings",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ValueJson",
                table: "sensor_readings",
                newName: "value_json");

            migrationBuilder.RenameColumn(
                name: "ReceivedAt",
                table: "sensor_readings",
                newName: "received_at");

            migrationBuilder.RenameColumn(
                name: "MessageId",
                table: "sensor_readings",
                newName: "message_id");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "sensor_readings",
                newName: "device_id");

            migrationBuilder.RenameIndex(
                name: "IX_sensor_readings_MessageId_Metric",
                table: "sensor_readings",
                newName: "IX_sensor_readings_message_id_metric");

            migrationBuilder.RenameIndex(
                name: "IX_sensor_readings_DeviceId_ReceivedAt_Id",
                table: "sensor_readings",
                newName: "IX_sensor_readings_device_id_received_at_id");

            migrationBuilder.RenameColumn(
                name: "Quality",
                table: "devices",
                newName: "quality");

            migrationBuilder.RenameColumn(
                name: "Origin",
                table: "devices",
                newName: "origin");

            migrationBuilder.RenameColumn(
                name: "Mode",
                table: "devices",
                newName: "mode");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "devices",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ZoneId",
                table: "devices",
                newName: "zone_id");

            migrationBuilder.RenameColumn(
                name: "ReportedOnline",
                table: "devices",
                newName: "reported_online");

            migrationBuilder.RenameColumn(
                name: "MeasurementsJson",
                table: "devices",
                newName: "measurements_json");

            migrationBuilder.RenameColumn(
                name: "LastTelemetryAt",
                table: "devices",
                newName: "last_telemetry_at");

            migrationBuilder.RenameColumn(
                name: "LastSeenAt",
                table: "devices",
                newName: "last_seen_at");

            migrationBuilder.RenameColumn(
                name: "FirmwareVersion",
                table: "devices",
                newName: "firmware_version");

            migrationBuilder.RenameColumn(
                name: "DeviceKey",
                table: "devices",
                newName: "device_key");

            migrationBuilder.RenameColumn(
                name: "ActuatorsJson",
                table: "devices",
                newName: "actuators_json");

            migrationBuilder.RenameIndex(
                name: "IX_devices_ZoneId",
                table: "devices",
                newName: "IX_devices_zone_id");

            migrationBuilder.RenameIndex(
                name: "IX_devices_DeviceKey",
                table: "devices",
                newName: "IX_devices_device_key");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "device_commands",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "device_commands",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "device_commands",
                newName: "action");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "device_commands",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "RequestedBy",
                table: "device_commands",
                newName: "requested_by");

            migrationBuilder.RenameColumn(
                name: "RequestedAt",
                table: "device_commands",
                newName: "requested_at");

            migrationBuilder.RenameColumn(
                name: "PublishedAt",
                table: "device_commands",
                newName: "published_at");

            migrationBuilder.RenameColumn(
                name: "PublicationAttemptedAt",
                table: "device_commands",
                newName: "publication_attempted_at");

            migrationBuilder.RenameColumn(
                name: "ParametersJson",
                table: "device_commands",
                newName: "parameters_json");

            migrationBuilder.RenameColumn(
                name: "ObservedStateJson",
                table: "device_commands",
                newName: "observed_state_json");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "device_commands",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "device_commands",
                newName: "device_id");

            migrationBuilder.RenameIndex(
                name: "IX_device_commands_Status_RequestedAt",
                table: "device_commands",
                newName: "IX_device_commands_status_requested_at");

            migrationBuilder.RenameIndex(
                name: "IX_device_commands_DeviceId",
                table: "device_commands",
                newName: "IX_device_commands_device_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "command_acknowledgements",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "command_acknowledgements",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "command_acknowledgements",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ReceivedAt",
                table: "command_acknowledgements",
                newName: "received_at");

            migrationBuilder.RenameColumn(
                name: "PayloadJson",
                table: "command_acknowledgements",
                newName: "payload_json");

            migrationBuilder.RenameColumn(
                name: "CommandId",
                table: "command_acknowledgements",
                newName: "command_id");

            migrationBuilder.RenameIndex(
                name: "IX_command_acknowledgements_CommandId_Status",
                table: "command_acknowledgements",
                newName: "IX_command_acknowledgements_command_id_status");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "automation_rules",
                newName: "version");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "automation_rules",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "automation_rules",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "automation_rules",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "RuleType",
                table: "automation_rules",
                newName: "rule_type");

            migrationBuilder.RenameColumn(
                name: "MaxPulseMs",
                table: "automation_rules",
                newName: "max_pulse_ms");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "automation_rules",
                newName: "device_id");

            migrationBuilder.RenameColumn(
                name: "CooldownMs",
                table: "automation_rules",
                newName: "cooldown_ms");

            migrationBuilder.RenameIndex(
                name: "IX_automation_rules_DeviceId_RuleType",
                table: "automation_rules",
                newName: "IX_automation_rules_device_id_rule_type");

            migrationBuilder.RenameColumn(
                name: "Cause",
                table: "alerts",
                newName: "cause");

            migrationBuilder.RenameColumn(
                name: "Active",
                table: "alerts",
                newName: "active");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "alerts",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ResolvedAt",
                table: "alerts",
                newName: "resolved_at");

            migrationBuilder.RenameColumn(
                name: "RaisedAt",
                table: "alerts",
                newName: "raised_at");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "alerts",
                newName: "device_id");

            migrationBuilder.RenameColumn(
                name: "AcknowledgedBy",
                table: "alerts",
                newName: "acknowledged_by");

            migrationBuilder.RenameColumn(
                name: "AcknowledgedAt",
                table: "alerts",
                newName: "acknowledged_at");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_device_id_cause",
                table: "alerts",
                columns: new[] { "device_id", "cause" },
                unique: true,
                filter: "active = true");

            migrationBuilder.AddForeignKey(
                name: "FK_alerts_devices_device_id",
                table: "alerts",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_automation_rules_devices_device_id",
                table: "automation_rules",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_command_acknowledgements_device_commands_command_id",
                table: "command_acknowledgements",
                column: "command_id",
                principalTable: "device_commands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_device_commands_devices_device_id",
                table: "device_commands",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_zones_zone_id",
                table: "devices",
                column: "zone_id",
                principalTable: "zones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sensor_readings_devices_device_id",
                table: "sensor_readings",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sensor_readings_telemetry_messages_message_id",
                table: "sensor_readings",
                column: "message_id",
                principalTable: "telemetry_messages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_system_events_devices_device_id",
                table: "system_events",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_telemetry_messages_devices_device_id",
                table: "telemetry_messages",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alerts_devices_device_id",
                table: "alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_automation_rules_devices_device_id",
                table: "automation_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_command_acknowledgements_device_commands_command_id",
                table: "command_acknowledgements");

            migrationBuilder.DropForeignKey(
                name: "FK_device_commands_devices_device_id",
                table: "device_commands");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_zones_zone_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_sensor_readings_devices_device_id",
                table: "sensor_readings");

            migrationBuilder.DropForeignKey(
                name: "FK_sensor_readings_telemetry_messages_message_id",
                table: "sensor_readings");

            migrationBuilder.DropForeignKey(
                name: "FK_system_events_devices_device_id",
                table: "system_events");

            migrationBuilder.DropForeignKey(
                name: "FK_telemetry_messages_devices_device_id",
                table: "telemetry_messages");

            migrationBuilder.DropIndex(
                name: "IX_alerts_device_id_cause",
                table: "alerts");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "zones",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "code",
                table: "zones",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "zones",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_zones_code",
                table: "zones",
                newName: "IX_zones_Code");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "users",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "users",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "users",
                newName: "PasswordHash");

            migrationBuilder.RenameIndex(
                name: "IX_users_username",
                table: "users",
                newName: "IX_users_Username");

            migrationBuilder.RenameColumn(
                name: "quality",
                table: "telemetry_messages",
                newName: "Quality");

            migrationBuilder.RenameColumn(
                name: "origin",
                table: "telemetry_messages",
                newName: "Origin");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "telemetry_messages",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "received_at",
                table: "telemetry_messages",
                newName: "ReceivedAt");

            migrationBuilder.RenameColumn(
                name: "payload_json",
                table: "telemetry_messages",
                newName: "PayloadJson");

            migrationBuilder.RenameColumn(
                name: "observed_at",
                table: "telemetry_messages",
                newName: "ObservedAt");

            migrationBuilder.RenameColumn(
                name: "message_id",
                table: "telemetry_messages",
                newName: "MessageId");

            migrationBuilder.RenameColumn(
                name: "device_id",
                table: "telemetry_messages",
                newName: "DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_telemetry_messages_device_id_message_id",
                table: "telemetry_messages",
                newName: "IX_telemetry_messages_DeviceId_MessageId");

            migrationBuilder.RenameColumn(
                name: "actor",
                table: "system_events",
                newName: "Actor");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "system_events",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "payload_json",
                table: "system_events",
                newName: "PayloadJson");

            migrationBuilder.RenameColumn(
                name: "occurred_at",
                table: "system_events",
                newName: "OccurredAt");

            migrationBuilder.RenameColumn(
                name: "event_type",
                table: "system_events",
                newName: "EventType");

            migrationBuilder.RenameColumn(
                name: "device_id",
                table: "system_events",
                newName: "DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_system_events_device_id_occurred_at",
                table: "system_events",
                newName: "IX_system_events_DeviceId_OccurredAt");

            migrationBuilder.RenameColumn(
                name: "unit",
                table: "sensor_readings",
                newName: "Unit");

            migrationBuilder.RenameColumn(
                name: "metric",
                table: "sensor_readings",
                newName: "Metric");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "sensor_readings",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "value_json",
                table: "sensor_readings",
                newName: "ValueJson");

            migrationBuilder.RenameColumn(
                name: "received_at",
                table: "sensor_readings",
                newName: "ReceivedAt");

            migrationBuilder.RenameColumn(
                name: "message_id",
                table: "sensor_readings",
                newName: "MessageId");

            migrationBuilder.RenameColumn(
                name: "device_id",
                table: "sensor_readings",
                newName: "DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_sensor_readings_message_id_metric",
                table: "sensor_readings",
                newName: "IX_sensor_readings_MessageId_Metric");

            migrationBuilder.RenameIndex(
                name: "IX_sensor_readings_device_id_received_at_id",
                table: "sensor_readings",
                newName: "IX_sensor_readings_DeviceId_ReceivedAt_Id");

            migrationBuilder.RenameColumn(
                name: "quality",
                table: "devices",
                newName: "Quality");

            migrationBuilder.RenameColumn(
                name: "origin",
                table: "devices",
                newName: "Origin");

            migrationBuilder.RenameColumn(
                name: "mode",
                table: "devices",
                newName: "Mode");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "devices",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "zone_id",
                table: "devices",
                newName: "ZoneId");

            migrationBuilder.RenameColumn(
                name: "reported_online",
                table: "devices",
                newName: "ReportedOnline");

            migrationBuilder.RenameColumn(
                name: "measurements_json",
                table: "devices",
                newName: "MeasurementsJson");

            migrationBuilder.RenameColumn(
                name: "last_telemetry_at",
                table: "devices",
                newName: "LastTelemetryAt");

            migrationBuilder.RenameColumn(
                name: "last_seen_at",
                table: "devices",
                newName: "LastSeenAt");

            migrationBuilder.RenameColumn(
                name: "firmware_version",
                table: "devices",
                newName: "FirmwareVersion");

            migrationBuilder.RenameColumn(
                name: "device_key",
                table: "devices",
                newName: "DeviceKey");

            migrationBuilder.RenameColumn(
                name: "actuators_json",
                table: "devices",
                newName: "ActuatorsJson");

            migrationBuilder.RenameIndex(
                name: "IX_devices_zone_id",
                table: "devices",
                newName: "IX_devices_ZoneId");

            migrationBuilder.RenameIndex(
                name: "IX_devices_device_key",
                table: "devices",
                newName: "IX_devices_DeviceKey");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "device_commands",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "device_commands",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "action",
                table: "device_commands",
                newName: "Action");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "device_commands",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "requested_by",
                table: "device_commands",
                newName: "RequestedBy");

            migrationBuilder.RenameColumn(
                name: "requested_at",
                table: "device_commands",
                newName: "RequestedAt");

            migrationBuilder.RenameColumn(
                name: "published_at",
                table: "device_commands",
                newName: "PublishedAt");

            migrationBuilder.RenameColumn(
                name: "publication_attempted_at",
                table: "device_commands",
                newName: "PublicationAttemptedAt");

            migrationBuilder.RenameColumn(
                name: "parameters_json",
                table: "device_commands",
                newName: "ParametersJson");

            migrationBuilder.RenameColumn(
                name: "observed_state_json",
                table: "device_commands",
                newName: "ObservedStateJson");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "device_commands",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "device_id",
                table: "device_commands",
                newName: "DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_device_commands_status_requested_at",
                table: "device_commands",
                newName: "IX_device_commands_Status_RequestedAt");

            migrationBuilder.RenameIndex(
                name: "IX_device_commands_device_id",
                table: "device_commands",
                newName: "IX_device_commands_DeviceId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "command_acknowledgements",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "command_acknowledgements",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "command_acknowledgements",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "received_at",
                table: "command_acknowledgements",
                newName: "ReceivedAt");

            migrationBuilder.RenameColumn(
                name: "payload_json",
                table: "command_acknowledgements",
                newName: "PayloadJson");

            migrationBuilder.RenameColumn(
                name: "command_id",
                table: "command_acknowledgements",
                newName: "CommandId");

            migrationBuilder.RenameIndex(
                name: "IX_command_acknowledgements_command_id_status",
                table: "command_acknowledgements",
                newName: "IX_command_acknowledgements_CommandId_Status");

            migrationBuilder.RenameColumn(
                name: "version",
                table: "automation_rules",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "automation_rules",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "automation_rules",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "automation_rules",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "rule_type",
                table: "automation_rules",
                newName: "RuleType");

            migrationBuilder.RenameColumn(
                name: "max_pulse_ms",
                table: "automation_rules",
                newName: "MaxPulseMs");

            migrationBuilder.RenameColumn(
                name: "device_id",
                table: "automation_rules",
                newName: "DeviceId");

            migrationBuilder.RenameColumn(
                name: "cooldown_ms",
                table: "automation_rules",
                newName: "CooldownMs");

            migrationBuilder.RenameIndex(
                name: "IX_automation_rules_device_id_rule_type",
                table: "automation_rules",
                newName: "IX_automation_rules_DeviceId_RuleType");

            migrationBuilder.RenameColumn(
                name: "cause",
                table: "alerts",
                newName: "Cause");

            migrationBuilder.RenameColumn(
                name: "active",
                table: "alerts",
                newName: "Active");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "alerts",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "resolved_at",
                table: "alerts",
                newName: "ResolvedAt");

            migrationBuilder.RenameColumn(
                name: "raised_at",
                table: "alerts",
                newName: "RaisedAt");

            migrationBuilder.RenameColumn(
                name: "device_id",
                table: "alerts",
                newName: "DeviceId");

            migrationBuilder.RenameColumn(
                name: "acknowledged_by",
                table: "alerts",
                newName: "AcknowledgedBy");

            migrationBuilder.RenameColumn(
                name: "acknowledged_at",
                table: "alerts",
                newName: "AcknowledgedAt");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_DeviceId_Cause",
                table: "alerts",
                columns: new[] { "DeviceId", "Cause" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_alerts_devices_DeviceId",
                table: "alerts",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_automation_rules_devices_DeviceId",
                table: "automation_rules",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_command_acknowledgements_device_commands_CommandId",
                table: "command_acknowledgements",
                column: "CommandId",
                principalTable: "device_commands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_device_commands_devices_DeviceId",
                table: "device_commands",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_zones_ZoneId",
                table: "devices",
                column: "ZoneId",
                principalTable: "zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sensor_readings_devices_DeviceId",
                table: "sensor_readings",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sensor_readings_telemetry_messages_MessageId",
                table: "sensor_readings",
                column: "MessageId",
                principalTable: "telemetry_messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_system_events_devices_DeviceId",
                table: "system_events",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_telemetry_messages_devices_DeviceId",
                table: "telemetry_messages",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
