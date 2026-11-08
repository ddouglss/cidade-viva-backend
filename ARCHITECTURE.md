# Arquitetura backend — fronteiras e dados

## Módulos
- **Devices:** catálogo, zona, firmware, presença, estado corrente e último contato.
- **Telemetry:** ingestão MQTT, qualidade/origem, validação por dispositivo, armazenamento, retenção.
- **Control:** comandos, autorização, idempotência, persistência de status e ACK.
- **AutomationConfiguration:** regras e limites configuráveis enviados aos firmwares; firmware valida e possui limites absolutos.
- **Alerts:** água baixa, desconectado, leitura inválida e causa persistente.
- **Realtime:** SignalR como projeção do estado, não fonte de verdade.

## Dependências
`Api → Application/Modules → Domain`, com adapters em `Infrastructure`. Interfaces para clock, broker, repositórios e publisher SignalR. Evitar transações longas e acoplamento do broker ao HTTP. Usar outbox para envio confiável se necessário; no MVP, persistir antes de publicar e registrar falha/retry idempotente.

## Tabelas propostas
`zones`, `devices`, `device_state`, `sensor_readings`, `device_commands`, `command_acknowledgements`, `automation_rules`, `system_events`, `alerts`, `calibration_records`, `users`.

Campos comuns: UUID/ID, `device_key`, `schema_version`, `observed_at` (quando válido), `received_at`, `origin`, `quality`, `mode`, `correlation_id`. Índice `(device_id, received_at DESC)` nas leituras, unicidade do `commandId` e do `messageId` por dispositivo. EF Core migrations + testes reais PostgreSQL.

## Segurança
API autenticada com papéis `viewer` e `operator`, ações auditadas; MQTT com credenciais por serviço/dispositivo e ACL por tópico quando possível. CORS com origens permitidas, TLS em qualquer exposição externa, segredos fora do Git.
