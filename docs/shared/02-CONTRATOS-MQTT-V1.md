# Contrato MQTT e eventos — versão 1.0

**Status:** aprovado para implementação em simulador; novos campos exigem revisão do contrato. **Base local:** broker MQTT com autenticação. **IDs reservados:** `esp32-energy`, `esp32-water`. ISO 8601 UTC para eventos sincronizados; `uptimeMs` disponível offline.

## Tópicos e política

| Tópico | Publicador | Assinante | QoS | Retained |
|---|---|---|---:|---|
| `cidade-viva/v1/{deviceId}/telemetry` | dispositivo | backend | 1 | não |
| `cidade-viva/v1/{deviceId}/status` | dispositivo e LWT | backend | 1 | sim |
| `cidade-viva/v1/{deviceId}/commands` | backend | dispositivo | 1 | **não** |
| `cidade-viva/v1/{deviceId}/ack` | dispositivo | backend | 1 | não |

`commandId` é chave de deduplicação no dispositivo. Não republicar comandos expirados. Retained jamais em `/commands`.

## Telemetria — exemplo ilustrativo, NÃO leitura real

```json
{
  "schemaVersion": "1.0", "messageId": "evt-000042", "deviceId": "esp32-water",
  "timestamp": "2026-11-06T18:00:00Z", "uptimeMs": 583290,
  "origin": "PHYSICAL_SENSOR", "quality": "VALID", "mode": "AUTO",
  "measurements": { "soilHumidityPct": 37.2, "reservoirLow": false },
  "actuators": { "pumpOn": false }
}
```

- `origin`: `PHYSICAL_SENSOR | SIMULATED_INPUT | CALCULATED`.
- `quality`: `VALID | INVALID | STALE | UNSYNCHRONIZED_TIME` (extensível por revisão).
- Umidade percentual só deve ser publicada após calibração; sem calibração, registrar ADC bruto em campo apropriado, com unidade e `quality` coerentes.
- `timestamp` omitido ou qualidade de horário destacada se sem NTP válido; backend recebe `receivedAt` independentemente.
- Leituras de solar só se houver sensor conectado diretamente no circuito solar.

## Comando

```json
{
  "schemaVersion": "1.0",
  "commandId": "2af57ed7-b653-41d8-9c56-6643a9f2f0b8",
  "deviceId": "esp32-water", "action": "PUMP_PULSE",
  "parameters": { "durationMs": 2500 },
  "requestedAt": "2026-11-06T18:00:05Z", "expiresAt": "2026-11-06T18:00:15Z",
  "requestedBy": "operator:demo"
}
```

Ações **propostas**: `SET_LIGHT_MODE`, `SET_LIGHT_MAIN`, `REQUEST_CROSSING`, `PUMP_PULSE`, `SET_IRRIGATION_MODE`, `ACK_FAULT`. Manter allowlist por dispositivo e validar duração, modo, expiração e autorização. Cancelas só entram após identificação de driver.

## ACK

```json
{
  "schemaVersion": "1.0", "commandId": "2af57ed7-b653-41d8-9c56-6643a9f2f0b8",
  "deviceId": "esp32-water", "status": "REJECTED", "reason": "RESERVOIR_LOW",
  "observedState": { "pumpOn": false }, "uptimeMs": 584210
}
```

Estados do backend: `PENDING → PUBLISHED → RECEIVED → APPLIED | REJECTED | EXPIRED | TIMED_OUT`. `APPLIED` implica comando aplicado no atuador dentro da visibilidade de firmware, **não** confirmação de vazão, movimento de cancela ou carga solar (sem sensores de feedback). Duplicata deve responder mesmo resultado sem repetir pulso de bomba. Se não houver relógio seguro, recusar comando dependente de expiração ou estabelecer política de TTL confiável validada.

## Sinais de status e alertas
`status` retained com `online`, `firmwareVersion`, `uptimeMs`, `lastKnownMode`; configurar LWT offline. Payloads versão 1.0; backend marca telemetria antiga `STALE` por política de TTL, não a mantém como atual.

## Fluxo
Angular → `POST /api/v1/devices/{id}/commands` → backend persiste `PENDING` → publica MQTT → firmware valida → aplica/recusa → publica ACK → backend persiste → SignalR atualiza UI. Em erro de publicação, retornar estado adequado e permitir diagnóstico sem enviar pulsos cegos repetidos.
