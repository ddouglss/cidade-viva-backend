# Plano — SDD 001 backend .NET

## Desenho de implementação
1. Criar solução ASP.NET Core com módulos `Devices`, `Telemetry`, `Control`, `Automation`, `Alerts`, APIs versionadas; DI e configuração validada no boot.
2. PostgreSQL + EF Core, migrações e seed de zonas/dispositivos. Definir convenções de timestamps UTC e JSONB apenas onde agrega valor.
3. Cliente MQTT em hosted service com reconexão e validação de tópico/payload, LWT/status, telemetria; publisher de ACK/command e locks/idempotência.
4. Casos de uso de comando com persistência antes de publish, separação resultado da fila e resultado do dispositivo; política de timeout.
5. SignalR com projeção de snapshots e eventos, REST como fonte persistente.
6. Autenticação/ACLs, readiness, logs, testes e documentação OpenAPI.

## Contratos e persistência
- Aplicar `../../shared/02-CONTRATOS-MQTT-V1.md` (`docs/shared/02-CONTRATOS-MQTT-V1.md` na raiz do repositório; não alterar JSON sem ADR).
- `DeviceCommand` tem `commandId`, `action`, `parameters`, `expiresAt`, `requestedBy`, `status`, `ackReason`.
- `SensorReading` tem `deviceId`, `metric`, `value`, `unit`, `origin`, `quality`, `observedAt?`, `receivedAt`.

## Trade-offs
- Monólito modular é suficiente; não introduzir microserviços ou filas auxiliares sem necessidade.
- Usar DTO de entrada/saída, não expor entidades EF diretamente.
- Timeout/indisponibilidade física **não** vira sucesso otimista.

## Testes
Unit: validação/comandos, expirados, roles e estados; integração: PostgreSQL real, MQTT broker containerizado, idempotência; contrato: fixtures compartilhadas; e2e: simulador publica → API consulta → POST comando → ACK → SignalR.
