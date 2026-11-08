# Contrato HTTP e realtime — MVP local

Base local: `http://127.0.0.1:5080`. OpenAPI: `/openapi/v1.json`. Esta descrição acompanha SDD 004 e a implementação; não substitui MQTT compartilhado nem homologa hardware.

## Autenticação

`POST /api/v1/auth/login` recebe `{ "username": "operator", "password": "<secret local>" }`.
Resposta 200 no formato nativo ASP.NET Core: `tokenType`, `accessToken`, `expiresIn`, `refreshToken`.
O access token é opaco (não JWT), expira em 1800 segundos e deve ser enviado como `Authorization: Bearer <accessToken>`.
Não há cadastro público nem endpoint de refresh nesta entrega; ao expirar, autenticar novamente. Não depender do conteúdo do token nem armazenar senha no frontend. `GET /api/v1/auth/me` fornece `id`, `username` e `role` autenticados.

Credenciais iniciais ficam em `.env` ignorado pelo Git, chaves `Bootstrap__viewer__*` e `Bootstrap__operator__*`. Senhas no banco são hashes do PasswordHasher ASP.NET Core. Bootstrap explícito cria usuários ausentes e não redefine senhas existentes. Chaves locais de proteção de tokens estão em `src/CidadeViva.Api/.keys/`, ignoradas pelo Git; preservar o diretório para manter tokens válidos em reinícios. Para produção, proteção das chaves, TLS, rotação/revogação e provisionamento precisam de operação própria.

## Leitura — viewer e operator

| Rota GET | Resposta |
|---|---|
| `/api/v1/health/live` | Público, 200 se processo vivo |
| `/api/v1/health/ready` | Público, 200 ou 503; banco e MQTT operacionais (schema instalado quando City habilitado) |
| `/api/v1/zones` | Lista `{ id, code, name }` |
| `/api/v1/devices` | Lista de snapshots descritos abaixo |
| `/api/v1/devices/{deviceKey}` | Snapshot do dispositivo; 404 se inexistente |
| `/api/v1/devices/{deviceKey}/telemetry` | Histórico paginado |
| `/api/v1/commands/{commandId}` | Estado persistido de um comando |
| `/api/v1/alerts` | Até 200 alertas mais recentes, ativos e resolvidos |

deviceKey é `esp32-energy` ou `esp32-water`; UUID interno não é alias de rota.

Snapshot:

```json
{
  "id": "<uuid>", "deviceKey": "esp32-water", "zoneCode": "WATER_IRRIGATION",
  "firmwareVersion": "0.1.0-sim", "online": true, "reportedOnline": true,
  "isStale": false, "mode": "MANUAL", "origin": "SIMULATED_INPUT", "quality": "VALID",
  "lastSeenAt": "<UTC>", "lastTelemetryAt": "<UTC>",
  "measurements": { "soilHumidityPct": 37.2, "reservoirLow": true },
  "actuators": { "pumpOn": false }, "automationRules": []
}
```

Exemplo ilustrativo, não medição física. `reportedOnline` é o último status reportado; retained online não comprova presença. `online` exige telemetria recente e status não offline. `isStale` considera última telemetria e TTL configurável (padrão 30s); `quality` original não é sobrescrita. Mesmo online, os atuadores são o último estado reportado, não comprovação instantânea física. Antes da primeira leitura, datas/origem/qualidade podem ser null e os objetos vazios.

`lastTelemetryAt` usa o instante observado quando há horário sincronizado, com fallback para recepção quando ele está ausente; timestamp mais de 30s no futuro é rejeitado. Backlog antigo fica no histórico, não rejuvenesce presença nem sobrescreve snapshot mais novo. `automationRules` do dispositivo water contém `{ id, deviceKey, maxPulseMs, cooldownMs, version, updatedAt, updatedBy, deliveryStatus }`.

Histórico: `?from=<UTC>&to=<UTC>&metric=soilHumidityPct&page=1&pageSize=50`.
Intervalo `[from,to)` sobre `receivedAt`; page>=1, pageSize 1..200; ordenação `receivedAt DESC, id DESC`.
Resposta `{ items, page, pageSize, totalCount }`. Cada item: `{ id, messageId, metric, value, unit, origin, quality, observedAt, receivedAt }`. `value` preserva número ou booleano; `unit` pode ser null para métrica sem unidade definida. Sem relógio sincronizado, `observedAt` é null; `receivedAt` continua UTC do servidor. Não converter null/INVALID em zero.

## Escrita — somente operator

`POST /api/v1/devices/{deviceKey}/commands`:

```json
{ "commandId": "<UUID opcional para retry>", "action": "PUMP_PULSE", "parameters": { "durationMs": 2500 } }
```

Retorna 202 + Location `/api/v1/commands/{commandId}`. Comando novo retorna PENDING, nunca bomba ligada.
DTO: `{ commandId, deviceKey, action, parameters, status, reason, requestedAt, expiresAt, publishedAt, requestedBy, observedState }`.
requestedBy é gerado pela identidade, nunca aceito do corpo. Mesmo ID/conteúdo/autor devolve o mesmo comando; ID conflitante =>409.

| Device | Ação | Parameters |
|---|---|---|
| water | PUMP_PULSE | durationMs inteiro 1..3000, sujeito ao limite configurado mais restrito |
| water | SET_IRRIGATION_MODE | mode AUTO ou MANUAL |
| energy | SET_LIGHT_MODE | mode AUTO ou MANUAL |
| energy | SET_LIGHT_MAIN | on boolean |
| energy | REQUEST_CROSSING | objeto vazio |

Esses parâmetros são o recorte técnico implementado em SDD 004, a ser conferido na integração do firmware. ACK_FAULT e cancela permanecem indisponíveis. O simulador pode recusar ações que não modela; não simula segurança semafórica completa.

Expiração de fila padrão 10s; timeout ACK padrão 15s após publicação. Uma tentativa é reservada antes do envio; PUBLISHED só depois da resposta do broker. Envio ambíguo permanece diagnosticável e não é repetido automaticamente. PUBACK do transporte não implica ACK do dispositivo. ACKs RECEIVED/APPLIED/REJECTED/EXPIRED são consumidos de forma idempotente; tardios ficam auditados sem apagar TIMED_OUT. APPLIED não comprova vazão hidráulica.

`POST /api/v1/alerts/{id}/ack` reconhece e audita; mantém `active=true` enquanto houver causa.
Alert DTO: `{ id, deviceKey, cause, active, raisedAt, resolvedAt, acknowledgedAt, acknowledgedBy }`.

`PUT /api/v1/automation-rules/{id}` recebe `{ maxPulseMs, cooldownMs, expectedVersion }`.
Pulso 1..3000 e cooldown>=15000 são limites provisórios do laboratório. Atualização concorrente =>409.
Grava revisão e auditoria e restringe novas solicitações da API; responde `deliveryStatus=NOT_SENT`. **Não envia configuração ao firmware.** BE-009 permanece parcial até contrato remoto coordenado; proteção física continua local na placa.

Erros: 400 validação, 401 sem login/token inválido, 403 perfil insuficiente, 404 inexistente, 409 conflito/idempotência/versão, 429 limite de requisições. ProblemDetails e correlation ID para erros tratados. Login limita tentativas por IP; escritas por usuário. Sem respostas com senhas, connection strings ou stack traces.

## SignalR

Hub autenticado `/hubs/city`, protocolo JSON. Usar `accessTokenFactory` do cliente SignalR; query `access_token` aceita apenas no caminho do hub para WebSocket/SSE. Não colocar token em logs ou URLs copiadas.

| Evento | Payload |
|---|---|
| DeviceTelemetryUpdated | `{deviceKey,messageId,origin,quality,receivedAt}` |
| DeviceStatusChanged | `{deviceKey,reportedOnline,retained}` |
| CommandStatusChanged | CommandDto |
| AlertRaised | AlertDto |

Eventos são notificações após commit. Obter snapshot REST ao conectar/reconectar; eventos não substituem estado persistido. Consultar snapshot ao receber telemetria/status e reconsultar periodicamente quando necessário para refletir TTL. Não usar evento como confirmação de efeito físico não instrumentado.

## Simulador

Executar na raiz: `dotnet run --no-build --project tools/device-simulator`.
Publica a cada 2s, sempre `SIMULATED_INPUT`, com UUID por mensagem; dois clientes MQTT/LWT independentes.
Por padrão, boia simulada baixa recusa PUMP_PULSE com RESERVOIR_LOW. `-- --allow-water` permite testar pulso simulado e cooldown; nunca aciona GPIO. As confirmações são deduplicadas na memória durante a execução; reiniciar o simulador não é prova de deduplicação persistente em firmware físico.
