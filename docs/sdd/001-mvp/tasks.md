# Tarefas — SDD 001 backend .NET

**Regra:** `[x]` só com implementação e evidência. Todas iniciam pendentes.

## Fase B0 — Fundação
- [x] B001 Fixar versão .NET, criar solution, projetos, lint/analyzers e testes.
- [x] B002 Criar Docker Compose PostgreSQL + Mosquitto local com credenciais de desenvolvimento, health checks e variáveis `.env.example`.
- [x] B003 Configurar módulos, DI, tratamento de erros, OpenAPI, logs e health live/ready. *(Fundação técnica, DI, ProblemDetails, OpenAPI, logs estruturados com Correlation ID e endpoints `/api/v1/health/live` e `/api/v1/health/ready` concluídos e testados na Entrega B / SDD 002; implementação interna dos módulos de negócio Devices, Telemetry, Control, Automation e Alerts permanece vinculada às tarefas B004–B011).*

## Fase B1 — Dados
- [x] B004 Criar entidades e migrações EF Core: zones/devices/state/readings/commands/acks/rules/alerts. *(SDD 004: estado corrente junto de devices; telemetry_messages garante deduplicação da mensagem inteira.)*
- [x] B005 Seed dos dois device IDs e zonas, índices, unicidades e auditoria. *(Zonas funcionais confirmadas pelo usuário; seed idempotente e migrations testados em PostgreSQL real.)*
- [x] B006 Implementar endpoints GET, paginação de leituras e sinalização de stale/offline.

## Fase B2 — MQTT/comandos
- [x] B007 Implementar integração MQTT, reconexão, autenticação, LWT e validação do contrato v1. *(Teste com reinício de broker isolado e novas leituras dos dois simuladores.)*
- [x] B008 Implementar `POST /devices/{id}/commands`, validação/autorizações e resposta HTTP 202.
- [x] B009 Persistir primeiro, publicar depois; implementar estados e timeout.
- [x] B010 Consumir ACK de forma idempotente; recusa de boia baixa e duplicatas.
- [x] B011 Adicionar SignalR e consultas de snapshot pós-reconexão. *(Backend/hub testado por WebSocket; reconexão visual Angular cabe ao frontend.)*

## Fase B3 — Qualidade
- [x] B012 Criar testes unitários, integração PostgreSQL+MQTT e contrato v1.
- [x] B013 Criar simulador de dispositivos e cenário comando bomba recusado.
- [x] B014 Documentar runbook e registrar evidências de status, telemetria e ACK.

**Pendência normativa BE-009:** endpoint de atualização dos limites locais versionado/auditado existe, mas envio da configuração ao firmware permanece NOT_SENT até contrato coordenado. Não considerar esse requisito ponta a ponta concluído. Ver SDD 004 e evidências de 08/11/2026. Hardware permanece pendente.

**DoD:** testes verdes + API rodando local + ACK/erro rastreáveis + migrations reproduzíveis.
