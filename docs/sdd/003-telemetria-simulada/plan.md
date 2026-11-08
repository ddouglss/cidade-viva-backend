# Plano — SDD 003 Primeiro fluxo de telemetria simulada

**Estado:** PROPOSTO (nenhum código implementado antes da revisão e aprovação deste plano e das lacunas `L-01` a `L-04`).  
**Vínculo:** estende `docs/sdd/001-mvp/plan.md` e `docs/sdd/002-fundacao-local/plan.md`.

---

## 1. Desenho técnico proposto (separado do que já está fechado)

### 1.1. Modelagem e persistência mínima no PostgreSQL (`CidadeViva.Domain` / `CidadeViva.Infrastructure`)

Reutilizando integralmente os nomes de tabelas e colunas já previstos em `ARCHITECTURE.md` e na seção 12 de `08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md`, o recorte de persistência mínima abrange:

1. **`zones` (Catálogo de setores/zonas):**
   - `id` (`uuid`, PK)
   - `code` (`varchar(64)`, `UNIQUE`, `NOT NULL`)
   - `name` (`varchar(128)`, `NOT NULL`)
   - *Nota (Lacuna L-01):* os registros do seed inicial dependem da confirmação do catálogo de zonas.

2. **`devices` (Catálogo de dispositivos IoT):**
   - `id` (`uuid`, PK)
   - `device_key` (`varchar(64)`, `UNIQUE`, `NOT NULL`) — seed obrigatório: `'esp32-energy'` e `'esp32-water'`.
   - `zone_id` (`uuid`, FK para `zones.id`, anulável caso o vínculo de zonas ainda não esteja fechado).
   - `category` (`varchar(64)`, `NOT NULL`) — ex.: `'ENERGY_MOBILITY'` e `'WATER_IRRIGATION'`.
   - `firmware_version` (`varchar(64)`, `NULL`).
   - `last_seen_at` (`timestamptz`, `NULL`).

3. **`device_state` (Projeção do estado corrente — 1:1 com `devices`):**
   - `device_id` (`uuid`, PK e FK para `devices.id`).
   - `online` (`boolean`, `NOT NULL`, default `false`).
   - `mode` (`varchar(32)`, `NULL`) — ex.: `'AUTO'`, `'MANUAL'`.
   - `last_origin` (`varchar(32)`, `NULL`) — `'PHYSICAL_SENSOR' | 'SIMULATED_INPUT' | 'CALCULATED'`.
   - `last_quality` (`varchar(32)`, `NULL`) — `'VALID' | 'INVALID' | 'STALE' | 'UNSYNCHRONIZED_TIME'`.
   - `last_uptime_ms` (`bigint`, `NULL`).
   - `outputs_json` (`jsonb`, `NOT NULL`, default `'{}'::jsonb`) — armazena o estado reportado de `actuators` (e últimas métricas resumidas para snapshot rápido).
   - `last_telemetry_at` (`timestamptz`, `NULL`) — instante `received_at` da última telemetria válida, usado para cálculo determinístico de `stale`.
   - `updated_at` (`timestamptz`, `NOT NULL`).

4. **`sensor_readings` (Histórico de leituras de telemetria):**
   - `id` (`uuid`, PK)
   - `device_id` (`uuid`, FK para `devices.id`, `NOT NULL`)
   - `message_id` (`varchar(128)`, `NOT NULL`) — identificador da mensagem MQTT de origem (`messageId`).
   - `metric` (`varchar(64)`, `NOT NULL`) — nome da métrica extraída de `measurements` (ex.: `soilHumidityPct`, `reservoirLow`).
   - `numeric_value` (`double precision`, `NOT NULL`) — valor numérico da métrica (para booleanos em `measurements`, como `reservoirLow`, proposta: mapear `0.0` / `1.0` com `unit = 'boolean'`, ou manter apenas métricas numéricas em `sensor_readings` e estado booleano no snapshot — *ver decisão técnica abaixo*).
   - `unit` (`varchar(32)`, `NOT NULL`) — ex.: `'%'`, `'lx'`, `'V'`, `'mA'`, `'raw'`, `'boolean'`.
   - `quality` (`varchar(32)`, `NOT NULL`) — `'VALID' | 'INVALID' | 'STALE' | 'UNSYNCHRONIZED_TIME'`.
   - `origin` (`varchar(32)`, `NOT NULL`) — `'PHYSICAL_SENSOR' | 'SIMULATED_INPUT' | 'CALCULATED'`.
   - `observed_at` (`timestamptz`, `NULL`) — preenchido quando `timestamp` do payload é UTC válido.
   - `received_at` (`timestamptz`, `NOT NULL`) — preenchido pelo relógio UTC do backend (`TimeProvider`).
   - **Índices obrigatórios:**
     - `ix_sensor_readings_device_received_at`: `(device_id, received_at DESC)` — conforme `ARCHITECTURE.md` (garante ordenação eficiente por tempo de recepção independente de sincronismo NTP nos ESP32).
     - `ux_sensor_readings_device_message_metric`: índice `UNIQUE` em `(device_id, message_id, metric)` — garante no banco que a mesma mensagem MQTT (`messageId`) nunca duplique uma métrica para o mesmo dispositivo.

---

### 1.2. Ingestão MQTT, validação e deduplicação (`CidadeViva.Application` / `CidadeViva.Infrastructure`)

1. **Subscrição nos tópicos do contrato v1:**
   - Estender o `MqttConnectionManager` (ou criar `MqttTelemetryConsumerHostedService` coordenado com a sessão MQTT) para subscrever com QoS 1:
     - `cidade-viva/v1/+/telemetry`
     - `cidade-viva/v1/+/status`
   - Re-subscrever automaticamente após qualquer reconexão da sessão MQTT.

2. **Pipeline de validação de telemetria (`TelemetryIngestionService`):**
   - Extrair `{topicDeviceId}` do tópico `cidade-viva/v1/{topicDeviceId}/telemetry`.
   - Desserializar JSON de forma estrita; se inválido, registrar warning estruturado e descartar.
   - Validar:
     - `schemaVersion == "1.0"`;
     - `deviceId` não vazio, igual a `{topicDeviceId}` e existente no catálogo (`esp32-energy` ou `esp32-water`);
     - `messageId` não vazio (máx. 128 caracteres);
     - `uptimeMs >= 0`;
     - `origin` em `{ "PHYSICAL_SENSOR", "SIMULATED_INPUT", "CALCULATED" }`;
     - `quality` em `{ "VALID", "INVALID", "STALE", "UNSYNCHRONIZED_TIME" }`.
   - **Deduplicação (`messageId`):**
     - Verificar se já existe leitura para `(device_id, message_id)` (e apoiar-se na constraint única `ux_sensor_readings_device_message_metric` para concorrência). Se já processada, retornar resultado `DuplicateIgnored` sem alterar o banco e confirmar o ACK de nível de protocolo MQTT (QoS 1 `PUBACK`).
   - **Persistência transacional:**
     - Inserir as linhas de `sensor_readings` correspondentes às métricas do payload;
     - Atualizar `devices.last_seen_at = received_at`;
     - Fazer upsert em `device_state` (`online = true`, `mode`, `last_origin = origin`, `last_quality = quality`, `last_uptime_ms = uptimeMs`, `outputs_json`, `last_telemetry_at = received_at`, `updated_at = received_at`).

3. **Pipeline de validação de status/LWT (`DeviceStatusIngestionService`):**
   - Extrair `{topicDeviceId}` de `cidade-viva/v1/{topicDeviceId}/status`.
   - Validar correspondência com `esp32-energy` ou `esp32-water` e `schemaVersion == "1.0"` (conforme definição da Lacuna L-02).
   - Se `online == true`: atualizar `devices.firmware_version`, `devices.last_seen_at = received_at` e `device_state.online = true`, `last_uptime_ms`, `mode = lastKnownMode`, `updated_at = received_at`.
   - Se `online == false` (incluindo LWT publicado pelo broker): atualizar `device_state.online = false` e `updated_at = received_at`.

---

### 1.3. Proposta de endpoints REST de leitura (`CidadeViva.Api`)

> [!NOTE]
> Os DTOs e parâmetros abaixo são **propostas para fechar a Lacuna L-03** antes da implementação.

1. **`GET /api/v1/zones`**
   - Retorna lista de zonas cadastradas (`id`, `code`, `name`).
2. **`GET /api/v1/devices`**
   - Retorna catálogo com estado atual projetado de cada dispositivo:
     - `id` (UUID), `deviceKey` (`esp32-energy` / `esp32-water`), `zoneCode`, `category`, `firmwareVersion`, `online`, `isStale`, `mode`, `lastOrigin`, `effectiveQuality` (`STALE` quando `isStale == true`, senão `lastQuality`), `lastSeenAtUtc`, `lastTelemetryAtUtc`, `outputs` (objeto JSON de atuadores).
3. **`GET /api/v1/devices/{id}`**
   - **Proposta para `{id}` (Lacuna L-03):** aceitar tanto o `deviceKey` (`esp32-water`, `esp32-energy`) quanto o `UUID` interno (`Guid.TryParse`), retornando `404 Not Found` (`ProblemDetails`) quando não encontrado.
   - Retorna o detalhe completo do dispositivo, seu estado corrente (`online`, `isStale`, `lastOrigin`, `effectiveQuality`, `outputs`) e o resumo das últimas métricas conhecidas.
4. **`GET /api/v1/devices/{id}/telemetry?from=&to=&metric=&page=1&pageSize=50`**
   - **Proposta de filtro e paginação (Lacuna L-03):**
     - `from` e `to` (ISO 8601 UTC opcionais) filtrando sobre `received_at` (índice `(device_id, received_at DESC)`), garantindo que leituras sem NTP (`observed_at == null`) também sejam retornadas de forma consistente;
     - `metric` (opcional) para filtrar uma métrica específica (ex.: `soilHumidityPct`);
     - `page` (`>= 1`, default `1`) e `pageSize` (`1..200`, default `50`);
     - Resposta paginada contendo `items` (`id`, `messageId`, `metric`, `value`, `unit`, `origin`, `quality`, `observedAtUtc`, `receivedAtUtc`), `page`, `pageSize`, `totalCount`.

---

### 1.4. Proposta para o Simulador MQTT (`tools/device-simulator`)

- Projeto console .NET (`net10.0`) em `tools/device-simulator/CidadeViva.DeviceSimulator.csproj`, referenciando apenas `MQTTnet` (`5.2.0.1603`) e configuração externa via variáveis de ambiente / `.env` (`MQTT_SIMULATOR_USER=cv_simulator`, `MQTT_SIMULATOR_PASSWORD`).
- Não importa código de `cidade-viva-frontend` ou `cidade-viva-firmware`.
- Modos de operação propostos por argumento de linha de comando / variável:
  - `continuous`: publica status `online: true` (retained) no boot, configura LWT `online: false` (retained) e publica telemetria periódica (`origin: "SIMULATED_INPUT"`) para `esp32-energy` e `esp32-water`.
  - `replay-scenarios`: executa determinísticamente os cenários de teste (leitura válida dos dois dispositivos, duplicata de `messageId`, leitura sem `timestamp` com `UNSYNCHRONIZED_TIME`, payload inválido para teste de rejeição e desconexão com LWT).

---

## 2. Resumo das decisões pendentes para aprovação do Codex/usuário

Antes de iniciar a codificação das tarefas de `docs/sdd/003-telemetria-simulada/tasks.md`, solicita-se confirmação sobre as 4 lacunas detalhadas em `spec.md`:

1. **L-01 (Zonas):** quais códigos e nomes de zonas devem entrar no seed inicial (`zones`) e como vincular `esp32-energy` e `esp32-water`?
2. **L-02 (Payloads de `esp32-energy`, ADC bruto e `/status`):**
   - Proposta de chaves para `esp32-energy/telemetry`:
     - `measurements`: `{ "illuminanceLux": 142.5, "busVoltageV": 5.02, "currentMa": 310.0 }` (sendo `busVoltageV` e `currentMa` opcionais apenas quando o ramo INA219 for simulado/medido);
     - `actuators`: `{ "mainLightOn": true, "trafficState": "VEHICLE_A_GO", "pedestrianRequestPending": false }`.
   - Proposta para ADC bruto em `esp32-water/telemetry` (quando não calibrado): `"soilAdcRaw": 2450` (unidade `"raw"`).
   - Proposta para `cidade-viva/v1/{deviceId}/status`:
     ```json
     {
       "schemaVersion": "1.0",
       "deviceId": "esp32-water",
       "timestamp": "2026-11-07T18:00:00Z",
       "uptimeMs": 583290,
       "online": true,
       "firmwareVersion": "0.1.0-sim",
       "lastKnownMode": "AUTO"
     }
     ```
3. **L-03 (Rotas REST e DTOs):** confirmar se `{id}` aceita `deviceKey` (`esp32-energy` / `esp32-water`) + `UUID`, e se a paginação `page`/`pageSize` filtrada por `received_at` está aprovada.
4. **L-04 (TTL de `stale` e janela de `messageId`):** confirmar o valor padrão de `TelemetryStaleTtlSeconds` (ex.: `30` s) e se a unicidade `(device_id, message_id)` na tabela `sensor_readings` atende ao MVP (com o simulador gerando `messageId` único por mensagem, exceto no cenário explícito de teste de duplicata).
