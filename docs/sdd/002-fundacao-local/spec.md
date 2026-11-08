# SDD 002 — Fundação local do backend (.NET, PostgreSQL, Mosquitto e Health Live/Ready)

**Estado:** APROVADO PARA EXECUÇÃO (Entrega B) | **Vínculo:** detalha a fundação técnica da [SDD 001 — Backend .NET](../001-mvp/spec.md) sem substituí-la.  
**Escopo normativo:** `BE-001` completo; fundação técnica de `BE-010` (infraestrutura local de broker e banco); tarefas `B001`, `B002` e `B003` (nas partes aplicáveis à fundação local).

---

## 1. Contexto e delimitação
Antes de implementar entidades de domínio, migrations de negócio, ingestão de telemetria, comandos ou SignalR, o repositório `cidade-viva-backend` precisa de uma fundação local determinística, segura e verificável.

Esta especificação cobre exclusivamente:
1. Estrutura da solução .NET em Clean Architecture / Monólito Modular (`Domain`, `Application`, `Infrastructure`, `Api`, `UnitTests`, `IntegrationTests`) com dependências voltadas para dentro e versões fixadas.
2. Ambiente local em Docker Compose para PostgreSQL e Eclipse Mosquitto com versões fixadas, persistência em volumes, publicação restrita a loopback (`127.0.0.1`), autenticação obrigatória no MQTT (`allow_anonymous false`), credenciais separadas para `backend` e `simulator`, e ACLs por tópico.
3. Validação de configuração obrigatória no boot sem vazar segredos em logs ou respostas HTTP.
4. Padronização de erros (`ProblemDetails`), OpenAPI e logs estruturados com correlação (`X-Correlation-ID`).
5. Endpoints `GET /api/v1/health/live` e `GET /api/v1/health/ready`, verificando conexão real com PostgreSQL e sessão autenticada ativa no broker MQTT, com reconexão automática sem necessidade de reiniciar a API.

## 2. Fora de escopo desta SDD 002
- Tabelas de domínio (`zones`, `devices`, `sensor_readings`, `device_commands`, etc.), migrations de negócio e seed (`B004`, `B005`).
- Ingestão ou subscrição de tópicos de telemetria/status/ACK e publicação de comandos (`B006`–`B010`).
- Autenticação/autorização de usuários finais (`viewer`/`operator`) e endpoints de negócio.
- Hub SignalR funcional (`/hubs/city`, `B011`).
- Simulador de dispositivos (`tools/device-simulator`, `B013`).
- Dockerfiles de produção da API, CI/CD, abertura de portas na LAN/firewall ou deploy externo.

## 3. Requisitos da fundação local
- **FL-001 (Solução e dependências):** quatro projetos em `src/` (`CidadeViva.Domain`, `CidadeViva.Application`, `CidadeViva.Infrastructure`, `CidadeViva.Api`) e dois em `tests/` (`CidadeViva.UnitTests`, `CidadeViva.IntegrationTests`). `CidadeViva.Domain` não pode referenciar EF Core, Npgsql, ASP.NET Core ou MQTTnet.
- **FL-002 (Infraestrutura local segura):** `infra/compose.yaml` com PostgreSQL e Mosquitto em versões fixadas (proibido `latest`), bind exclusivo em `127.0.0.1`, volumes nomeados persistentes, `.env.example` contendo apenas placeholders e geração local documentada do arquivo de senhas do Mosquitto fora do controle de versão.
- **FL-003 (Segurança MQTT):** broker Mosquitto com `allow_anonymous false`, usuários distintos para backend (`cv_backend`) e simulador (`cv_simulator`) e arquivo de ACL restringindo leitura/escrita aos tópicos do contrato v1 (`cidade-viva/v1/{deviceId}/...`).
- **FL-004 (Validação de configuração no boot):** ausência de configuração obrigatória de banco ou MQTT deve impedir a inicialização da aplicação com mensagem diagnóstica clara que indique o nome da propriedade ausente/inválida, jamais expondo valores de senhas ou segredos.
- **FL-005 (Liveness vs Readiness):**
  - `GET /api/v1/health/live` retorna `200 OK` sempre que o processo HTTP estiver ativo, mesmo que PostgreSQL ou Mosquitto estejam indisponíveis.
  - `GET /api/v1/health/ready` retorna `200 OK` somente quando uma consulta real ao PostgreSQL (`SELECT 1`) e a sessão autenticada MQTT estiverem operacionais; caso contrário, retorna `503 Service Unavailable`.
  - Nenhuma resposta de health ou erro pode expor connection strings, senhas, endereços internos ou stack traces.
- **FL-006 (Reconexão automática):** se PostgreSQL ou Mosquitto ficarem temporariamente indisponíveis e depois voltarem, a API deve recuperar o estado `200 OK` em `GET /api/v1/health/ready` automaticamente, sem reinício manual do processo.

## 4. Critérios de aceite (Given / When / Then)
1. **Dado** código limpo no checkout, **quando** executados `dotnet restore`, `dotnet build` e `dotnet test`, **então** todos compilam sem erros ou avisos tratados como falha nas versões fixadas.
2. **Dado** PostgreSQL e Mosquitto ativos com credenciais válidas, **quando** chamados `GET /api/v1/health/live` e `GET /api/v1/health/ready`, **então** ambos retornam `200 OK` com payload sanitizado.
3. **Dado** PostgreSQL indisponível e Mosquitto disponível, **quando** consultados os endpoints de health, **então** `live` retorna `200 OK` e `ready` retorna `503 Service Unavailable`.
4. **Dado** Mosquitto indisponível e PostgreSQL disponível, **quando** consultados os endpoints de health, **então** `live` retorna `200 OK` e `ready` retorna `503 Service Unavailable`.
5. **Dado** dependência indisponível que volta a operar, **quando** `GET /api/v1/health/ready` é consultado após a recuperação, **então** retorna `200 OK` sem reiniciar a API.
6. **Dado** cliente MQTT anônimo ou com credencial inválida, **quando** tenta conectar ao broker, **então** a conexão é recusada e a API configurada com credencial inválida permanece `503 Service Unavailable` no endpoint `ready`.
7. **Dado** variável obrigatória de banco ou MQTT ausente/vazia, **quando** a aplicação inicia, **então** o boot falha imediatamente com erro claro de validação sem registrar segredos.
