# Tarefas — SDD 003 Primeiro fluxo de telemetria simulada (PROPOSTA)

**Estado:** PROPOSTO — **nenhuma tarefa iniciada ou executada nesta entrega**. A execução aguarda revisão da SDD 003 e fechamento das lacunas `L-01` a `L-04`.  
**Relação com SDD 001 (`docs/sdd/001-mvp/tasks.md`):** cobre as partes de dispositivos/telemetria/status/leitura REST e simulador de telemetria de `B004`, `B005`, `B006`, `B007`, `B012` e `B013`. Conforme seção 7 de `001-PACOTE-ANTIGRAVITY.md`, `B004` e `B005` na SDD 001 não serão marcadas como completas enquanto faltar o restante do seu escopo (comandos, ACKs, regras e alertas).

---

## Fase TS0 — Fechamento de contratos do recorte (pré-requisito bloqueante)
- [ ] **TS-T01:** Revisar e aprovar as decisões para as lacunas `L-01` (catálogo de zonas), `L-02` (payloads de `esp32-energy`, ADC bruto e `/status`), `L-03` (semântica de `{id}` REST e DTOs/paginação) e `L-04` (TTL de `stale` e escopo de unicidade de `messageId`), sincronizando `docs/shared/02-CONTRATOS-MQTT-V1.md` nos três repositórios caso novos campos normativos sejam oficializados.

## Fase TS1 — Persistência mínima e seed (`B004` parcial / `B005` parcial)
- [ ] **TS-T02:** Modelar entidades `Zone`, `Device`, `DeviceState` e `SensorReading` em `CidadeViva.Domain` (sem dependência externa) e configurar mapeamento EF Core em `CidadeViva.Infrastructure`, com índice `(device_id, received_at DESC)` e constraint de unicidade `(device_id, message_id, metric)`.
- [ ] **TS-T03:** Gerar migration EF Core inicial da persistência mínima e implementar seed idempotente dos dispositivos `esp32-energy` e `esp32-water` e das zonas aprovadas em `L-01`.

## Fase TS2 — Ingestão MQTT de status e telemetria (`B007` parcial)
- [ ] **TS-T04:** Implementar validação do contrato MQTT v1 (`schemaVersion == "1.0"`, correspondência `{deviceId}` tópico vs. payload, `origin`, `quality`, métricas e timestamps), rejeitando payloads inválidos com log estruturado.
- [ ] **TS-T05:** Implementar subscrição MQTT com QoS 1 (`cidade-viva/v1/+/telemetry` e `cidade-viva/v1/+/status`), re-subscrição automática pós-reconexão, persistência idempotente por `(device_id, message_id)` e atualização de `last_seen_at` e `device_state` (incluindo LWT `online: false`).

## Fase TS3 — Endpoints REST de leitura (`B006` parcial)
- [ ] **TS-T06:** Implementar `GET /api/v1/zones`, `GET /api/v1/devices`, `GET /api/v1/devices/{id}` e `GET /api/v1/devices/{id}/telemetry` paginado, expondo `origin`, `quality`, estado `online` e cálculo de `isStale` conforme TTL aprovado em `L-04`, documentados no OpenAPI.

## Fase TS4 — Simulador MQTT e testes automatizados (`B012` parcial / `B013` parcial)
- [ ] **TS-T07:** Criar `tools/device-simulator` no repositório `cidade-viva-backend` usando credenciais `cv_simulator`, publicando status/LWT e telemetria (`origin: "SIMULATED_INPUT"`) para `esp32-energy` e `esp32-water`.
- [ ] **TS-T08:** Implementar testes unitários e de integração reais (PostgreSQL + Mosquitto) cobrindo todos os cenários `CA-01` a `CA-07` (`esp32-energy` e `esp32-water`, origem simulada, duplicatas de `messageId`, payload incompatível, `deviceId` divergente do tópico, desconexão/reconexão LWT e leitura `stale`).
