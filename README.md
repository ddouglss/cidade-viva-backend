# Subprojeto backend — CidadeViva API (`cidade-viva-backend`)

## Backend local — implementação de 08/11/2026

A fundação abaixo foi estendida pela **SDD 004**: telemetria MQTT idempotente, PostgreSQL com migrations/seed, auth local viewer/operator, leitura REST, comandos auditáveis, ACK/timeout, alertas e SignalR. Simulador com duas conexões MQTT independentes disponível em `tools/device-simulator`.

Inicialização reproduzível, na raiz, com Docker Desktop em execução:

```powershell
pwsh -NoProfile -File scripts/Initialize-Local.ps1
pwsh -NoProfile -File scripts/Run-Api.ps1
```

O primeiro comando preserva secrets existentes, gera apenas os ausentes no `.env` ignorado, sobe os serviços locais, compila, aplica migrations explicitamente e cria usuários ausentes com hashes seguros. Senhas iniciais estão somente nas chaves `Bootstrap__*` do `.env`; não são impressas. Não usar esse script para redefinir senha de usuário existente.

Em outro terminal, na raiz:

```powershell
dotnet run --no-build --project tools/device-simulator
```

API: `http://127.0.0.1:5080`; OpenAPI: `/openapi/v1.json`. Comandos detalhados e contrato para o frontend em [docs/api/MVP-LOCAL.md](docs/api/MVP-LOCAL.md); evidências em [docs/execution/002-EVIDENCIAS-BACKEND-MVP.md](docs/execution/002-EVIDENCIAS-BACKEND-MVP.md).

### Visual Studio / F5

O navegador abre automaticamente o Swagger em `http://127.0.0.1:5080/swagger`. A interface usa o documento `/openapi/v1.json` existente e está disponível em Development ou build Debug.

O assistente do dashboard é opcional. As configurações e instruções de ativação estão no [recorte Gemini](docs/sdd/005-assistente-consulta.md).

O perfil Development usa `http://127.0.0.1:5080`. O Visual Studio não carrega `.env` automaticamente. A API possui UserSecretsId e usa o provedor nativo User Secrets em Development. Nesta máquina, a configuração local já foi importada para esse armazenamento, fora do repositório. Em outra máquina, após inicializar o ambiente, importe as chaves com `__` do `.env`:

Em compilações Debug, o carregamento de User Secrets também é explícito, mesmo sem o perfil Development. Variáveis de ambiente e argumentos prevalecem. Esse carregamento adicional não existe em Release.

Debug também carrega o `.env` ignorado na raiz deste repositório, pesquisando a raiz pelo arquivo `CidadeViva.sln`. Isso permite F5 mesmo quando o armazenamento de User Secrets do Visual Studio não estiver disponível. Ordem de prioridade crescente: configuração padrão, `.env`, User Secrets, ambiente e argumentos. Release não possui esse carregamento local. Não copie `.env` para `bin`, nem adicione suas senhas a `launchSettings.json`.

```powershell
$localConfig = @{}
foreach ($line in Get-Content .env) {
    if ($line -match '^\s*([^#=\s]+)=(.*)$' -and $Matches[1].Contains('__')) {
        $localConfig[$Matches[1].Replace('__', ':')] = $Matches[2].Trim()
    }
}
$localConfig | ConvertTo-Json -Compress | dotnet user-secrets set --project src/CidadeViva.Api
```

Não use `user-secrets list` em logs compartilhados. Pare a depuração anterior antes de recompilar e executar novamente. Rode uma única instância da API local para evitar conflito de porta e clientId MQTT.

Validação: `dotnet build CidadeViva.sln` e `dotnet test CidadeViva.sln --no-build`. Testes novos usam banco e broker isolados; não removem volumes do ambiente de desenvolvimento. Docker deve estar ativo e credenciais locais previamente configuradas.

**Limites desta entrega:** configurações de irrigação são versionadas/auditadas e limitam solicitações na API, mas ficam `NOT_SENT` para a placa até contrato remoto coordenado. Hardware e integração visual do frontend continuam pendentes. Não há comprovação física de vazão, energia solar ou GPIO. Sem commit/push/deploy.

As seções seguintes preservam o runbook da fundação SDD 002 como referência histórica; para o estado atual, usar esta seção e a SDD 004.

**Stack fixada (Entrega B):** .NET SDK `10.0.403` (`net10.0`, runtime `10.0.14`), ASP.NET Core, C#, EF Core `10.0.14`, `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3`, `MQTTnet` `5.2.0.1603`, PostgreSQL `17.11-alpine3.24` e Eclipse Mosquitto `2.0.22-openssl`. Monólito modular com Clean Architecture.

**SDD:**
- MVP geral: `docs/sdd/001-mvp/spec.md` → `docs/sdd/001-mvp/plan.md` → `docs/sdd/001-mvp/tasks.md`
- Fundação local (Entrega B): `docs/sdd/002-fundacao-local/spec.md` → `docs/sdd/002-fundacao-local/plan.md` → `docs/sdd/002-fundacao-local/tasks.md`

Não publicar comandos da API diretamente em GPIO, não persistir segredos, não considerar `202 Accepted` execução confirmada. Contrato compartilhado sincronizado em `docs/shared/` a partir da raiz deste repositório.

---

## Responsabilidade deste repositório
- Expor a API HTTP REST versionada (`/api/v1/...`) e o hub SignalR (`/hubs/city`) para monitoramento e solicitação de comandos autenticados.
- Consumir e validar telemetria, status/LWT e ACKs publicados no broker MQTT local conforme `docs/shared/02-CONTRATOS-MQTT-V1.md` (`schemaVersion=1.0`, dispositivos `esp32-energy` e `esp32-water`).
- Persistir catálogo de zonas/dispositivos, leituras de sensores (com distinção estrita de `origin` entre `PHYSICAL_SENSOR`, `SIMULATED_INPUT` e `CALCULATED`), comandos idempotentes (`PENDING → PUBLISHED → RECEIVED → APPLIED | REJECTED | EXPIRED | TIMED_OUT`), ACKs, regras parametrizadas e alertas em PostgreSQL.
- Prover a infraestrutura local de desenvolvimento (`infra/`, PostgreSQL e Mosquitto autenticado em loopback) e, na etapa correspondente, o simulador MQTT (`tools/device-simulator/`).
- **Fronteira de segurança:** o backend nunca controla pinos físicos diretamente nem substitui os intertravamentos locais de segurança do firmware (boia de nível mínimo, timeout de bomba, máquina de estados semafórica sem verdes conflitantes).

## Relação com os outros dois repositórios
O projeto Cidade Viva está organizado em três repositórios públicos independentes (sem submodules, subtrees ou Git aninhado):
1. **`cidade-viva-backend` (este repositório — `https://github.com/ddouglss/cidade-viva-backend`, antigo `cidade-viva-platform`):** API .NET, persistência PostgreSQL, integração MQTT, projeção SignalR e simulador de dispositivos.
2. **`cidade-viva-frontend` (`https://github.com/ddouglss/cidade-viva-frontend`):** dashboard Angular + TypeScript que consome a API REST (snapshots, histórico, envio de comandos `202 Accepted`) e eventos SignalR deste backend, sem estado otimista de atuadores.
3. **`cidade-viva-firmware` (`https://github.com/ddouglss/cidade-viva-firmware`):** firmwares C++/PlatformIO para `esp32-energy` e `esp32-water` e documentação elétrica/solar. Comunica-se com este backend exclusivamente via broker MQTT local (`cidade-viva/v1/{deviceId}/...`).

---

## Como executar localmente (Entrega B — Fundação)

### 1. Preparar variáveis de ambiente e gerar secrets locais do Mosquitto
1. Copie `.env.example` para `.env` na raiz do repositório (o arquivo `.env` e a pasta `infra/mosquitto/secrets/` são ignorados pelo Git):
   ```powershell
   Copy-Item .env.example .env
   ```
2. Edite `.env` e substitua todos os placeholders `CHANGE_ME_*` por senhas locais seguras para `POSTGRES_PASSWORD`, `MQTT_BACKEND_PASSWORD` e `MQTT_SIMULATOR_PASSWORD` (e seus equivalentes `Database__Password` e `Mqtt__Password`).
3. Gere o arquivo de hashes de senha do Mosquitto (`infra/mosquitto/secrets/passwd`) usando o container oficial fixado:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\infra\mosquitto\generate-secrets.ps1
   ```

### 2. Iniciar PostgreSQL e Mosquitto locais (bind exclusivo em `127.0.0.1`)
```powershell
docker compose --env-file .env -f infra/compose.yaml up -d --wait
docker compose --env-file .env -f infra/compose.yaml ps
```

### 3. Restaurar, compilar e executar os testes automatizados
```powershell
dotnet restore CidadeViva.sln
dotnet build CidadeViva.sln --no-restore
dotnet test CidadeViva.sln --no-build --logger "console;verbosity=normal"
```

### 4. Executar a API pelo SDK e diagnosticar health/OpenAPI
Carregue as variáveis do `.env` na sessão atual e inicie `CidadeViva.Api`:
```powershell
Get-Content .env | Where-Object { $_ -match '^\s*[^#=\s]+=' } | ForEach-Object {
    $k, $v = $_ -split '=', 2
    [Environment]::SetEnvironmentVariable($k.Trim(), $v.Trim(), 'Process')
}
dotnet run --project src/CidadeViva.Api --urls http://127.0.0.1:5080
```
Endpoints disponíveis nesta entrega:
- `GET http://127.0.0.1:5080/api/v1/health/live` — retorna `200 OK` quando o processo HTTP está vivo.
- `GET http://127.0.0.1:5080/api/v1/health/ready` — retorna `200 OK` quando PostgreSQL (`SELECT 1`) e sessão autenticada MQTT estão prontos, ou `503 Service Unavailable` quando qualquer dependência estiver indisponível (sem expor segredos ou connection strings). Reconecta automaticamente quando a dependência volta sem precisar reiniciar a API.
- `GET http://127.0.0.1:5080/openapi/v1.json` — especificação OpenAPI v1.

### 5. Encerrar serviços locais preservando os volumes de dados
Para parar os containers sem apagar os volumes persistentes (`cidade_viva_postgres_data`, `cidade_viva_mosquitto_data`, `cidade_viva_mosquitto_log`):
```powershell
docker compose --env-file .env -f infra/compose.yaml stop
```
> **Atenção:** nunca execute `docker compose down -v` automaticamente para não destruir dados locais existentes.

---

## Ordem de leitura recomendada
1. `AGENTS.md` — regras obrigatórias do projeto, divisão de papéis (Codex planeja/revisa; Antigravity executa) e critérios de PR.
2. `docs/shared/01-VISAO-ESCOPO.md`, `docs/shared/02-CONTRATOS-MQTT-V1.md` e `docs/shared/03-REGRAS-INTEGRACAO.md`.
3. `docs/shared/04-INVENTARIO-E-DECISOES-PENDENTES.md`, `docs/shared/05-ADRS.md`, `docs/shared/06-PLANO-VALIDACAO-END-TO-END.md` e `docs/shared/07-REFERENCIAS.md`.
4. `ARCHITECTURE.md` — módulos internos do monólito modular, dependências, tabelas propostas e segurança.
5. `docs/sdd/001-mvp/{spec.md,plan.md,tasks.md}` e `docs/sdd/002-fundacao-local/{spec.md,plan.md,tasks.md}`.
6. `docs/execution/001-PACOTE-ANTIGRAVITY.md` e `docs/decisions/001-ORGANIZACAO-REPOSITORIOS-E-MAPA-DOCUMENTAL.md`.

## Estado real de implementação
- **Concluído até a Entrega B:**
  - Organização documental e contratos sincronizados (`Entrega A`).
  - Fundação local do backend (`Entrega B` / `SDD 002`): solução `CidadeViva.sln` com 4 projetos em `src/` e 2 projetos em `tests/`; Docker Compose para PostgreSQL `17.11-alpine3.24` e Mosquitto `2.0.22-openssl` autenticados em loopback com ACL por tópico; validação de configuração obrigatória no boot sem vazamento de segredos; `ProblemDetails`, OpenAPI e logs JSON estruturados com `X-Correlation-ID`; endpoints `GET /api/v1/health/live` e `GET /api/v1/health/ready` (`BE-001`) com reconexão automática e 25 testes automatizados (21 unitários + 4 de integração com serviços reais).
- **Pendente para as próximas etapas (`B004`–`B014`):**
  - Entidades de domínio, migrations EF Core e seed (`B004`, `B005`);
  - Ingestão de telemetria/status/ACK, endpoints REST de dispositivos/comandos/alertas, SignalR e simulador MQTT (`B006`–`B014`; `BE-002` a `BE-010`).
