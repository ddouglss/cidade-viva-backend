# SDD 003 — Primeiro fluxo de telemetria simulada: Simulador MQTT → Validação → PostgreSQL → Consulta REST

> Histórico: proposta preservada. Em 08/11/2026 o usuário autorizou Codex a assumir o backend e definiu zonas funcionais/auth local. A execução e as decisões técnicas do recorte passaram à SDD 004, incluindo mensagem inteira deduplicada e deviceKey único nas rotas. Não implementar esta proposta antiga em paralelo.

**Estado:** PROPOSTO (aguardando revisão e fechamento de lacunas pelo Codex/responsável antes de implementar código)  
**Vínculo:** detalha o primeiro recorte vertical da [SDD 001 — Backend .NET](../001-mvp/spec.md) sobre a fundação entregue na [SDD 002 — Fundação local](../002-fundacao-local/spec.md), conforme seção 7 (itens 1 a 4) de `docs/execution/001-PACOTE-ANTIGRAVITY.md`.  
**Escopo de requisitos SDD 001 abordados (parcialmente):** `BE-002`, `BE-003`, `BE-004` e `BE-010` (apenas dispositivos, status, telemetria, leitura REST e simulador de telemetria; **sem** comandos, ACK, alertas, regras de automação, autenticação de usuários ou SignalR).

---

## 1. Objetivo e fronteira do recorte

Validar ponta a ponta o primeiro fluxo de dados da plataforma Cidade Viva sem depender das placas físicas ESP32:
1. O simulador (`tools/device-simulator`) publica mensagens MQTT de status e telemetria para `esp32-energy` e `esp32-water` identificando explicitamente `"origin": "SIMULATED_INPUT"`.
2. O backend (`CidadeViva.Infrastructure` / `CidadeViva.Application`) consome os tópicos MQTT de `status` e `telemetry`, valida o contrato `schemaVersion: "1.0"` e a correspondência entre tópico e payload, garante idempotência/deduplicação e persiste no PostgreSQL.
3. A API REST (`CidadeViva.Api`) expõe os endpoints de consulta de dispositivos, zonas e histórico paginado de telemetria, distinguindo claramente dados simulados de sensores físicos e sinalizando leituras `offline`/`stale`.

### Fora de escopo desta SDD 003
- Publicação de comandos (`POST /api/v1/devices/{id}/commands`), consumo de `/ack` e máquina de estados de comandos (`BE-005`, `BE-006`).
- Hub SignalR (`/hubs/city`, `BE-007`).
- Motor/endpoints de alertas e reconhecimento (`BE-008`) e regras de automação (`BE-009`).
- Fluxo de autenticação/autorização de usuários (`viewer`/`operator`).
- Qualquer alteração em `docs/shared/`, `cidade-viva-frontend` ou `cidade-viva-firmware`.

---

## 2. Separação estrita: Decisões Existentes vs. Propostas desta SDD vs. Lacunas em Aberto

### 2.1. Decisões existentes (já aprovadas nos documentos vigentes)
| Item | Regra aprovada | Fonte normativa |
|---|---|---|
| **IDs de dispositivos reservados** | `esp32-energy` e `esp32-water` | `docs/shared/02-CONTRATOS-MQTT-V1.md` |
| **Tópicos MQTT deste recorte** | `cidade-viva/v1/{deviceId}/telemetry` (QoS 1, `retain=false`) e `cidade-viva/v1/{deviceId}/status` (QoS 1, `retain=true`, inclui LWT) | `docs/shared/02-CONTRATOS-MQTT-V1.md` |
| **Versão do schema** | `"schemaVersion": "1.0"` obrigatório em todos os payloads | `AGENTS.md`, `02-CONTRATOS-MQTT-V1.md` |
| **Origem do dado (`origin`)** | Enum fechado: `PHYSICAL_SENSOR \| SIMULATED_INPUT \| CALCULATED`. O simulador deve publicar sempre `SIMULATED_INPUT` | `02-CONTRATOS-MQTT-V1.md`, `03-REGRAS-INTEGRACAO.md` |
| **Qualidade da leitura (`quality`)** | Enum base: `VALID \| INVALID \| STALE \| UNSYNCHRONIZED_TIME` | `02-CONTRATOS-MQTT-V1.md` |
| **Envelope base de telemetria (`esp32-water`)** | `schemaVersion`, `messageId`, `deviceId`, `timestamp` (opcional se sem NTP), `uptimeMs`, `origin`, `quality`, `mode`, `measurements: { soilHumidityPct, reservoirLow }`, `actuators: { pumpOn }` | `02-CONTRATOS-MQTT-V1.md` |
| **Campos base de `status`** | `online`, `firmwareVersion`, `uptimeMs`, `lastKnownMode` (com LWT marcando offline) | `02-CONTRATOS-MQTT-V1.md` |
| **Índice temporal de leituras** | Seguir a arquitetura do componente: índice `(device_id, received_at DESC)` em `sensor_readings` e unicidade de `messageId` por dispositivo | `ARCHITECTURE.md`, `001-PACOTE-ANTIGRAVITY.md` (seção 6) |
| **Credenciais MQTT isoladas** | Backend usa `cv_backend`; simulador usa `cv_simulator` (já configurado na ACL da Entrega B) | `001-PACOTE-ANTIGRAVITY.md` (seções 5 e 6) |
| **Rotas REST de leitura** | `GET /api/v1/devices`, `GET /api/v1/devices/{id}`, `GET /api/v1/devices/{id}/telemetry?from=&to=`, `GET /api/v1/zones` | `docs/sdd/001-mvp/spec.md` (`BE-004`) |

---

### 2.2. Lacunas explícitas pendentes de decisão (NÃO assumidas como regras aprovadas)

> [!IMPORTANT]
> As lacunas abaixo foram identificadas nos documentos de origem e na seção 6 de `001-PACOTE-ANTIGRAVITY.md`. **Nenhuma sugestão abaixo tem caráter de contrato aprovado**; a implementação de código aguarda a definição destas opções na revisão desta SDD 003.

#### Lacuna L-01 — Catálogo de Zonas (`zones`) e vínculo inicial dos dispositivos
- **Estado atual na documentação:** `BE-002`, `BE-004` e `B005` exigem a tabela `zones`, o endpoint `GET /api/v1/zones` e o seed das zonas associadas a `esp32-energy` e `esp32-water`, mas nenhum documento compartilhado define a lista oficial de códigos (`code`) e nomes (`name`) das zonas da maquete.
- **Decisão necessária na revisão:**
  1. Quais registros de `zones` (`code`, `name`) devem compor o seed inicial? *(Opções para decisão: ex. zonas funcionais por controlador como `ENERGY_MOBILITY` / `WATER_IRRIGATION`, ou setores espaciais da maquete se definidos pelo grupo, ou permitir `zone_id` anulável e seed mínimo apenas das zonas confirmadas).*

#### Lacuna L-02 — Schema completo de `esp32-energy/telemetry`, ADC bruto de umidade e payload `/status`
- **Estado atual na documentação:**
  - `02-CONTRATOS-MQTT-V1.md` traz exemplo normativo apenas para `esp32-water/telemetry` (`measurements: { soilHumidityPct, reservoirLow }`, `actuators: { pumpOn }`).
  - Para `esp32-energy/telemetry`, os requisitos (`EN-003` a `EN-006`) citam luminosidade (BH1750 em lux), dois postes principais, semáforos (`A`/`B`/pedestre) e opcionalmente INA219 (P1), mas **não fixam os nomes exatos das chaves JSON** dentro de `measurements` e `actuators`.
  - Para `esp32-water/telemetry` sem calibração, o contrato exige *"registrar ADC bruto em campo apropriado, com unidade e quality coerentes"*, sem fixar o nome da chave (ex.: `soilAdcRaw`) nem a string de unidade (`raw` / `adc`).
  - Para `cidade-viva/v1/{deviceId}/status`, o contrato lista os campos `online`, `firmwareVersion`, `uptimeMs` e `lastKnownMode`, mas não apresenta exemplo JSON completo nem especifica se `schemaVersion`, `deviceId` e `timestamp` são obrigatórios no payload de status/LWT.
- **Decisão necessária na revisão:**
  1. Confirmar as chaves JSON normativas de `measurements` e `actuators` para `esp32-energy` e o campo de ADC bruto de `esp32-water`.
  2. Confirmar o JSON exato esperado em `cidade-viva/v1/{deviceId}/status` (online e LWT offline).

#### Lacuna L-03 — Identificador `{id}` nas rotas REST (`UUID` vs. `deviceKey`) e formato dos DTOs / paginação
- **Estado atual na documentação:**
  - `ARCHITECTURE.md` e a referência v1.0 definem `id` (UUID) como chave primária interna e `device_key` (`esp32-energy`, `esp32-water`) como chave única de negócio, enquanto o exemplo na seção 11 da referência v1.0 usa `/api/v1/devices/esp32-water/...`.
  - Os DTOs de saída de `GET /api/v1/devices`, `GET /api/v1/devices/{id}`, `GET /api/v1/devices/{id}/telemetry` e `GET /api/v1/zones` (nomes de campos, paginação `page`/`pageSize` vs. `limit`/`offset`, filtro `metric`, e se `from`/`to` filtram por `receivedAt` ou `observedAt`) não estão especificados.
- **Decisão necessária na revisão:**
  1. `{id}` nas rotas `/api/v1/devices/{id}` aceita `deviceKey` (`esp32-energy` / `esp32-water`), `UUID` ou ambos (resolvendo por UUID quando for GUID válido e por `device_key` caso contrário)?
  2. Aprovar o formato dos DTOs de resposta e os parâmetros de paginação propostos na seção 4 desta SDD.

#### Lacuna L-04 — Política temporal: TTL de `stale`, relógio não sincronizado e deduplicação de `messageId`
- **Estado atual na documentação:**
  - `02-CONTRATOS-MQTT-V1.md` e `BE-004` determinam que o backend não deve apresentar telemetria antiga como estado atual, marcando-a como `STALE` por política de TTL e indicando `offline/stale` na consulta REST. Contudo, o valor do TTL não está fixado.
  - O contrato prevê que `timestamp` pode ser omitido ou acompanhado de `quality: "UNSYNCHRONIZED_TIME"` quando o ESP32 não tiver NTP válido, usando `uptimeMs` e `receivedAt` do servidor.
  - `messageId` no exemplo do contrato é sequencial (`"evt-000042"`). Se um ESP32 reiniciar sem armazenamento não-volátil, o contador pode voltar a `"evt-000001"`.
- **Decisão necessária na revisão:**
  1. Qual o TTL padrão configurável (ex.: `TelemetryStaleTtlSeconds`, sugestão de estudo: `30` ou `60` segundos) após o qual o estado corrente/última telemetria passa a ser sinalizado como `stale` na consulta REST?
  2. A deduplicação de `(device_id, message_id)` deve ser permanente na tabela ou restrita a uma janela temporal / sessão de boot, ou o simulador/firmware deverá incluir sufixo/bootId/UUID no `messageId`?

---

## 3. Requisitos propostos para a SDD 003 (sujeitos à revisão das lacunas)

- **TS-001 (Persistência mínima do recorte):** criar migração EF Core inicial contendo apenas as tabelas necessárias ao fluxo de dispositivos e telemetria (`zones`, `devices`, `device_state`, `telemetry_messages` ou controle de deduplicação + `sensor_readings`), preservando o escopo de comandos/alertas/regras para etapa posterior.
- **TS-002 (Validação estrita do contrato MQTT v1):**
  - Rejeitar com log estruturado de diagnóstico (sem derrubar o consumidor nem gravar leitura corrompida) qualquer mensagem cujo JSON seja malformado, `schemaVersion != "1.0"`, `deviceId` ausente/desconhecido, `deviceId` do payload divergente do `{deviceId}` do tópico MQTT, `origin` fora de `PHYSICAL_SENSOR | SIMULATED_INPUT | CALCULATED`, ou `quality` fora dos valores permitidos.
- **TS-003 (Idempotência e deduplicação):**
  - Entrega duplicada da mesma mensagem MQTT (QoS 1 retry com mesmo `deviceId` + `messageId`) deve ser ignorada de forma idempotente, sem duplicar linhas em `sensor_readings` e sem gerar erro não tratado.
- **TS-004 (Preservação de `origin` e `quality` e tratamento de tempo):**
  - Gravar `received_at` (sempre em UTC a partir do `TimeProvider` do backend) e `observed_at` (em UTC quando `timestamp` válido estiver presente no payload; `null` quando omitido ou não sincronizado).
  - Nunca sobrescrever `SIMULATED_INPUT` como `PHYSICAL_SENSOR`.
  - Se `soilHumidityPct` for enviado sem calibração em dispositivo real (ou marcado `INVALID`), preservar `quality` correspondente.
- **TS-005 (Atualização de presença, status e `last_seen_at`):**
  - Mensagens válidas em `/status` e `/telemetry` atualizam `last_seen_at` em `devices` e o snapshot em `device_state` (`online`, `mode`, `outputs_json`, `updated_at`).
  - Mensagem LWT/status com `online: false` marca o dispositivo imediatamente como `online = false` em `device_state`.
- **TS-006 (Consulta REST de leitura com sinalização de `stale`/`offline`):**
  - Expor `GET /api/v1/zones`, `GET /api/v1/devices`, `GET /api/v1/devices/{id}` e `GET /api/v1/devices/{id}/telemetry`.
  - Calcular e expor na leitura do dispositivo se os dados estão `isStale` com base na política de TTL configurada e no estado `online`, impedindo que dados antigos pareçam estado operacional atual garantido.
- **TS-007 (Simulador MQTT isolado em `tools/device-simulator`):**
  - Aplicação console/worker em `tools/device-simulator` no próprio repositório `cidade-viva-backend`, conectando ao broker com as credenciais `cv_simulator` e publicando status/telemetria para `esp32-energy` e `esp32-water` sempre com `"origin": "SIMULATED_INPUT"`.

---

## 4. Cenários de teste e critérios de aceite propostos

| ID | Cenário (Given / When / Then) | Camada de teste |
|---|---|---|
| **CA-01** | **Dado** simulador/cliente publicando telemetria válida para `esp32-water` e `esp32-energy` com `origin: "SIMULATED_INPUT"`, **quando** ingerida pelo backend, **então** as leituras são persistidas com `origin = SIMULATED_INPUT`, `received_at` preenchido e ficam consultáveis em `GET /api/v1/devices/{id}` e `GET /api/v1/devices/{id}/telemetry`. | Integração (PostgreSQL + Mosquitto reais) |
| **CA-02** | **Dado** a mesma mensagem MQTT (`deviceId` + `messageId`) entregue duas vezes (QoS 1 duplicate), **quando** processada pelo backend, **então** apenas um conjunto de leituras é gravado no banco (idempotência comprovada por contagem em `sensor_readings`). | Unidade + Integração |
| **CA-03** | **Dado** payload com `schemaVersion` diferente de `"1.0"`, JSON inválido ou `origin` desconhecida, **quando** publicado no tópico de telemetria, **então** a mensagem é rejeitada, gera log estruturado de aviso e não altera `sensor_readings` nem `device_state`. | Unidade + Integração |
| **CA-04** | **Dado** mensagem publicada no tópico `cidade-viva/v1/esp32-water/telemetry` cujo body declara `"deviceId": "esp32-energy"`, **quando** recebida pelo backend, **então** é rejeitada por divergência tópico vs. payload. | Unidade + Integração |
| **CA-05** | **Dado** dispositivo que publica `/status` com `online: false` (ou LWT após desconexão) e depois reconecta publicando `online: true`, **quando** consultado `GET /api/v1/devices/{id}`, **então** o estado reflete fielmente a transição `online: false → true` e atualiza `lastSeenAt`. | Integração |
| **CA-06** | **Dado** dispositivo cuja última telemetria ultrapassou o TTL configurado de `stale`, **quando** consultado `GET /api/v1/devices` ou `GET /api/v1/devices/{id}`, **então** a resposta sinaliza `isStale: true` (e `effectiveQuality: "STALE"`), sem apresentar a leitura antiga como estado atual garantido. | Unidade + Integração |
| **CA-07** | **Dado** telemetria sem campo `timestamp` (ou com `quality: "UNSYNCHRONIZED_TIME"`), **quando** ingerida, **então** `observed_at` permanece `null` (ou reflete a ressalva de qualidade) e `received_at` registra o instante UTC de recepção no servidor. | Unidade + Integração |
