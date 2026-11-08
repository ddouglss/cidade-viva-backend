# SDD 001 — Backend .NET: API, MQTT e telemetria

**Estado:** PROPOSTO | **Owner:** desenvolvimento de software | **Dependências:** contrato MQTT v1; PostgreSQL; broker local.

## Contexto
O backend deve tratar o estado real de duas placas IoT, auditando telemetria e comandos. Não executar automação física e não declarar sucesso sem ACK.

## Requisitos normativos
- **BE-001:** expor GET `/api/v1/health/live` e `/api/v1/health/ready` distinguindo processo de dependências.
- **BE-002:** cadastrar duas placas, suas zonas e estados de conectividade; persistir `last_seen_at` e LWT.
- **BE-003:** consumir tópicos de telemetria/ACK/status, validar `schemaVersion` e `deviceId`, rejeitar payload incompatível, guardar histórico e origem real/simulada.
- **BE-004:** expor GET `/api/v1/devices`, `/{id}`, `/{id}/telemetry` paginado, `/api/v1/zones`, `/api/v1/alerts`, `/api/v1/commands/{id}`.
- **BE-005:** POST `/api/v1/devices/{id}/commands` autentica, autoriza, valida allowlist de ação/parâmetros, persiste PENDING e responde `202` com `commandId`.
- **BE-006:** publicar comando com expiração e `retain=false`; consumir ACK idempotente e atualizar status, inclusive recusas.
- **BE-007:** disponibilizar `/hubs/city` com eventos `DeviceTelemetryUpdated`, `DeviceStatusChanged`, `CommandStatusChanged`, `AlertRaised`.
- **BE-008:** registrar eventos de boia baixa, sensor inválido, dispositivo offline; reconhecer alerta sem ocultar causa.
- **BE-009:** endpoint `PUT /api/v1/automation-rules/{id}` somente operador, valores dentro dos limites documentados, auditados e versionados.
- **BE-010:** adicionar dados seed e simulador conforme o contrato MQTT, sem apresentar fixtures como reais.

## Critérios de aceitação — estilo Given/When/Then
1. **Dado** telemetria validada de `esp32-water`, **quando** chegar pelo MQTT, **então** uma leitura com `origin` e `received_at` fica consultável pela API.
2. **Dado** pedido de pulso válido por operador, **quando** POST, **então** HTTP 202 com status inicial `PENDING` é retornado; nunca resposta imediata de bomba ligada.
3. **Dado** ACK `REJECTED/RESERVOIR_LOW`, **quando** recebido, **então** comando torna-se recusado, motivo é visível no histórico e evento SignalR é enviado.
4. **Dado** comando reenviado/ACK duplicado, **quando** processado, **então** há no máximo um registro efetivo por ID e o hardware não recebe um pulso duplicado pelo backend.
5. **Dado** dispositivo offline e último dado antigo, **quando** frontend solicita estado, **então** a API identifica `offline/stale`, em vez de garantir operação corrente.
6. **Dado** mensagem inválida ou dispositivo de outro tópico, **quando** ingerida, **então** é rejeitada e gera diagnóstico sem corromper banco.

## Fora de escopo
Controlar pinos físicos, inferir vazão sem sensor de fluxo, decidir horário seguro de travessia, conectar fontes elétricas, IA comandando hardware, cadastro corporativo complexo.

## Segurança e observabilidade
Operador autenticado para comandos; viewer só consulta. Correlation ID, logs estruturados, timeout de publicação, alerta de broker down, readiness, migrações versionadas e segredos externos.
