using System.Text.Json;
using CidadeViva.Application.City;
using CidadeViva.Domain.City;
using CidadeViva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CidadeViva.Infrastructure.City;

public sealed class DeviceMessageProcessor(CidadeVivaDbContext db, TimeProvider clock, ICityEventPublisher events,
    IOptions<CityRuntimeOptions> options) : IDeviceMessageProcessor
{
    public async Task ProcessAsync(string topic, ReadOnlyMemory<byte> payload, bool retained, CancellationToken ct)
    {
        MqttEnvelope envelope = MqttEnvelope.Parse(topic, payload, retained);
        JsonElement body = envelope.Body;
        DateTimeOffset now = clock.GetUtcNow();
        List<(string Name, object Data)> notifications = [];
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({envelope.DeviceKey}))", ct).ConfigureAwait(false);
        Device device = await db.Devices.SingleOrDefaultAsync(d => d.DeviceKey == envelope.DeviceKey, ct).ConfigureAwait(false)
            ?? throw new CityRequestException(400, "DEVICE_NOT_SEEDED");

        if (envelope.Kind == "telemetry")
        {
            string messageId = MqttEnvelope.Text(body, "messageId")!;
            if (await db.Messages.AnyAsync(m => m.DeviceId == device.Id && m.MessageId == messageId, ct).ConfigureAwait(false)) return;
            string quality = MqttEnvelope.Text(body, "quality")!;
            var message = new TelemetryMessage { DeviceId = device.Id, MessageId = messageId,
                Origin = MqttEnvelope.Text(body, "origin")!, Quality = quality, ReceivedAt = now,
                ObservedAt = quality != "UNSYNCHRONIZED_TIME" && body.TryGetProperty("timestamp", out JsonElement timestamp) ? timestamp.GetDateTimeOffset() : null,
                PayloadJson = body.GetRawText() };
            if (message.ObservedAt > now.AddSeconds(30)) throw new CityRequestException(400, "TIMESTAMP_IN_FUTURE");
            db.Messages.Add(message);
            foreach (JsonProperty metric in body.GetProperty("measurements").EnumerateObject())
                db.Readings.Add(new SensorReading { DeviceId = device.Id, MessageId = message.Id,
                    Metric = metric.Name, ValueJson = metric.Value.GetRawText(), ReceivedAt = now,
                    Unit = metric.Name switch { "soilHumidityPct" => "%", "illuminanceLux" => "lx", "busVoltageV" => "V", "currentMa" => "mA", "soilAdcRaw" => "raw", _ => null } });
            DateTimeOffset freshness = message.ObservedAt is { } observedAt && observedAt < now ? observedAt : now;
            bool current = device.LastTelemetryAt is null || freshness >= device.LastTelemetryAt;
            if (current)
            {
                device.LastTelemetryAt = freshness;
                device.Mode = MqttEnvelope.Text(body, "mode"); device.Origin = message.Origin; device.Quality = quality;
                device.MeasurementsJson = body.GetProperty("measurements").GetRawText(); device.ActuatorsJson = body.GetProperty("actuators").GetRawText();
                // A broker backlog is history, not evidence that the device is currently online.
                if (now - freshness <= TimeSpan.FromSeconds(options.Value.TelemetryStaleTtlSeconds))
                {
                    device.ReportedOnline = true; device.LastSeenAt = now;
                    await SetAlertAsync(device, "SENSOR_INVALID", quality == "INVALID", now, notifications, ct).ConfigureAwait(false);
                    if (quality != "INVALID" && body.GetProperty("measurements").TryGetProperty("reservoirLow", out JsonElement low))
                        await SetAlertAsync(device, "RESERVOIR_LOW", low.GetBoolean(), now, notifications, ct).ConfigureAwait(false);
                    await SetAlertAsync(device, "DEVICE_OFFLINE", false, now, notifications, ct).ConfigureAwait(false);
                }
            }
            notifications.Add(("DeviceTelemetryUpdated", new { deviceKey = device.DeviceKey, messageId, origin = message.Origin, quality, receivedAt = now }));
        }
        else if (envelope.Kind == "status")
        {
            device.FirmwareVersion = MqttEnvelope.Text(body, "firmwareVersion");
            device.ReportedOnline = body.GetProperty("online").GetBoolean();
            device.Mode = MqttEnvelope.Text(body, "lastKnownMode");
            if (!retained && device.ReportedOnline) device.LastSeenAt = now;
            if (!device.ReportedOnline) await SetAlertAsync(device, "DEVICE_OFFLINE", true, now, notifications, ct).ConfigureAwait(false);
            notifications.Add(("DeviceStatusChanged", new { deviceKey = device.DeviceKey, reportedOnline = device.ReportedOnline, retained }));
        }
        else
        {
            Guid commandId = Guid.Parse(MqttEnvelope.Text(body, "commandId")!);
            DeviceCommand command = await db.Commands.SingleOrDefaultAsync(c => c.Id == commandId && c.DeviceId == device.Id, ct).ConfigureAwait(false)
                ?? throw new CityRequestException(400, "UNKNOWN_COMMAND_ACK");
            if (command.Status == "PENDING" && command.PublicationAttemptedAt is null)
                throw new CityRequestException(400, "ACK_BEFORE_PUBLICATION");
            string status = MqttEnvelope.Text(body, "status")!;
            if (await db.Acknowledgements.AnyAsync(a => a.CommandId == commandId && a.Status == status, ct).ConfigureAwait(false)) return;
            db.Acknowledgements.Add(new CommandAcknowledgement { CommandId = commandId, Status = status,
                Reason = MqttEnvelope.Text(body, "reason"), PayloadJson = body.GetRawText(), ReceivedAt = now });
            if (command.Status is "PUBLISHED" or "RECEIVED" || command.Status == "PENDING" && command.PublicationAttemptedAt is not null)
            {
                command.Status = status; command.Reason = MqttEnvelope.Text(body, "reason");
                command.ObservedStateJson = body.GetProperty("observedState").GetRawText();
                notifications.Add(("CommandStatusChanged", CityService.ToCommand(command, device.DeviceKey)));
                if (command.Reason == "RESERVOIR_LOW")
                    await SetAlertAsync(device, "RESERVOIR_LOW", true, now, notifications, ct).ConfigureAwait(false);
            }
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        foreach ((string name, object data) in notifications)
            await events.PublishAsync(name, data, ct).ConfigureAwait(false);
    }

    internal async Task SetAlertAsync(Device device, string cause, bool active, DateTimeOffset now,
        List<(string Name, object Data)> notifications, CancellationToken ct)
    {
        CityAlert? alert = await db.Alerts.SingleOrDefaultAsync(a => a.DeviceId == device.Id && a.Cause == cause && a.Active, ct).ConfigureAwait(false);
        if (active && alert is null)
        {
            alert = new CityAlert { DeviceId = device.Id, Cause = cause, Active = true, RaisedAt = now };
            db.Alerts.Add(alert); notifications.Add(("AlertRaised", CityService.ToAlert(alert, device.DeviceKey)));
        }
        else if (!active && alert is not null) { alert.Active = false; alert.ResolvedAt = now; }
    }
}
