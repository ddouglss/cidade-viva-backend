# Tarefas — SDD 002 Fundação local do backend (Entrega B)

**Regra:** marcar `[x]` somente após implementação executada, testada e evidenciada.  
**Relação com SDD 001 (`docs/sdd/001-mvp/tasks.md`):** detalha a execução de `B001`, `B002` e `B003` no escopo delimitado da Entrega B.

- [x] **FL-T01 (B001):** Fixar SDK `.NET 10.0.403` em `global.json`, configurar `Directory.Build.props` (nullable, analyzers, warnings-as-errors) e `Directory.Packages.props` com versões exatas verificadas; criar solução `CidadeViva.sln` e os 6 projetos (`Domain`, `Application`, `Infrastructure`, `Api`, `UnitTests`, `IntegrationTests`).
- [x] **FL-T02 (B002):** Criar `.gitignore`, `.env.example` (somente placeholders), `infra/compose.yaml` (PostgreSQL `17.11-alpine3.24` e Mosquitto `2.0.22-openssl` em loopback com volumes persistentes e healthchecks), `infra/mosquitto/config/{mosquitto.conf,acl.conf}` e script `infra/mosquitto/generate-secrets.ps1`.
- [x] **FL-T03 (B003):** Implementar validação de configuração obrigatória no boot (`DatabaseOptions`, `MqttOptions`), `ProblemDetails`, OpenAPI, logs estruturados com `CorrelationIdMiddleware`, sonda real de PostgreSQL, gerenciador de conexão/reconexão MQTT e endpoints `GET /api/v1/health/live` e `GET /api/v1/health/ready`.
- [x] **FL-T04 (B001/B003):** Implementar testes unitários (`CidadeViva.UnitTests`) e testes de integração reais (`CidadeViva.IntegrationTests`) cobrindo: serviços disponíveis (`200/200`), PostgreSQL indisponível (`200/503`), MQTT indisponível (`200/503`), recuperação automática sem reinício (`200`), credencial MQTT inválida/anônima recusada (`503`) e falha de boot por configuração obrigatória ausente sem vazar segredo.
- [x] **FL-T05 (B002/B003):** Documentar no `README.md` o procedimento de geração de secrets locais, inicialização, diagnóstico e encerramento seguro (sem `down -v`), e atualizar o status das tarefas `B001`–`B003` conforme a cobertura efetivamente evidenciada.
