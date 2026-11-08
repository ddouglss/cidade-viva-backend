# Plano — SDD 002 Fundação local do backend

**Vínculo:** complementa `docs/sdd/001-mvp/plan.md` para a Entrega B (`001-PACOTE-ANTIGRAVITY.md`, seção 5).

---

## 1. Versões exatas verificadas no ambiente e em fontes oficiais

Todas as versões abaixo foram inspecionadas no ambiente local e nas fontes oficiais (`api.nuget.org` e Docker Hub `library/postgres` e `library/eclipse-mosquitto`) em 07/11/2026, sem uso de tags `latest`:

| Componente / Pacote | Versão fixada | Fonte / Compatibilidade verificada |
|---|---|---|
| **.NET SDK (LTS)** | `10.0.403` | Instalado em `C:\Program Files\dotnet\sdk\10.0.403`; fixado em `global.json` (`rollForward: latestFeature`) |
| **Target Framework / Runtime** | `net10.0` (`10.0.14`) | `Microsoft.NETCore.App 10.0.14` / `Microsoft.AspNetCore.App 10.0.14` |
| `Microsoft.EntityFrameworkCore` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Microsoft.EntityFrameworkCore.Relational` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Microsoft.Extensions.Hosting.Abstractions` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Microsoft.Extensions.Options` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.3` | NuGet oficial (`net10.0`, requer `Microsoft.EntityFrameworkCore [10.0.4, 11.0.0)` e `Npgsql >= 10.0.3`) |
| `Npgsql` | `10.0.3` | NuGet oficial (`net10.0`) |
| `MQTTnet` | `5.2.0.1603` | NuGet oficial (`MqttClientFactory`, `IMqttClient`, MQTT v5.0 / v3.1.1) |
| `Microsoft.AspNetCore.OpenApi` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Microsoft.AspNetCore.Mvc.Testing` | `10.0.14` | NuGet oficial (`net10.0`) |
| `Microsoft.NET.Test.Sdk` | `18.11.0` | NuGet oficial |
| `xunit` | `2.9.3` | NuGet oficial |
| `xunit.runner.visualstudio` | `4.0.1` | NuGet oficial |
| `coverlet.collector` | `10.1.0` | NuGet oficial |
| **Docker Engine / Compose** | `29.9.2` / `v5.6.1` | Ambiente local Windows + WSL2 |
| **PostgreSQL (container)** | `postgres:17.11-alpine3.24` | Docker Hub oficial `library/postgres` |
| **Eclipse Mosquitto (container)** | `eclipse-mosquitto:2.0.22-openssl` | Docker Hub oficial `library/eclipse-mosquitto` |

---

## 2. Arquitetura e estrutura de diretórios

```text
cidade-viva-backend/
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── CidadeViva.slnx (ou CidadeViva.sln)
├── .gitignore
├── .env.example
├── src/
│   ├── CidadeViva.Domain/
│   │   └── Modules/                      # marcadores de fronteira sem acoplamento externo
│   ├── CidadeViva.Application/
│   │   └── Abstractions/Health/          # IDatabaseReadinessProbe, IMqttReadinessProbe, ReadinessReport
│   ├── CidadeViva.Infrastructure/
│   │   ├── Configuration/                # DatabaseOptions, MqttOptions + validadores sem vazamento de segredos
│   │   ├── Persistence/                  # CidadeVivaDbContext (vazio de tabelas de negócio nesta entrega) + PostgresReadinessProbe
│   │   ├── Messaging/                    # MqttConnectionManager / MqttConnectionHostedService (reconexão automática)
│   │   └── DependencyInjection.cs
│   └── CidadeViva.Api/
│       ├── Middleware/                   # CorrelationIdMiddleware (X-Correlation-ID + LogScope)
│       ├── Endpoints/                    # HealthEndpoints (/api/v1/health/live e /api/v1/health/ready)
│       ├── Program.cs
│       └── appsettings.json              # sem segredos; valores obrigatórios injetados por ambiente
├── tests/
│   ├── CidadeViva.UnitTests/
│   └── CidadeViva.IntegrationTests/
└── infra/
    ├── compose.yaml
    └── mosquitto/
        ├── config/
        │   ├── mosquitto.conf
        │   └── acl.conf
        ├── secrets/                      # ignorado no Git (.gitignore); contém passwd gerado localmente
        └── generate-secrets.ps1          # gera infra/mosquitto/secrets/passwd via container mosquitto_passwd
```

---

## 3. Decisões técnicas de implementação

1. **Validação de configuração no boot (`ValidateOnStart`):**
   - `DatabaseOptions` exige `Host`, `Port` (`1..65535`), `Database`, `Username` e `Password` não vazios (e rejeita placeholders literais como `CHANGE_ME`).
   - `MqttOptions` exige `Host`, `Port` (`1..65535`), `ClientId`, `Username` e `Password` não vazios (e rejeita placeholders literais como `CHANGE_ME`).
   - Os validadores `IValidateOptions<DatabaseOptions>` e `IValidateOptions<MqttOptions>` retornam apenas o nome das propriedades ausentes/inválidas, garantindo que nenhum valor secreto seja incluído em mensagens de exceção ou logs de inicialização.
2. **Cliente MQTT e reconexão (`MqttConnectionManager`):**
   - Implementa `IHostedService`, `IMqttReadinessProbe` e `IAsyncDisposable`.
   - Inicia em background sem derrubar o processo caso o broker esteja offline no momento do boot (`live` permanece `200 OK`).
   - Mantém loop de supervisão periódica para reconectar automaticamente quando a sessão cair. Além disso, caso `CheckReadinessAsync` seja chamado enquanto desconectado, dispara tentativa controlada de conexão com lock assíncrono (`SemaphoreSlim`) e timeout curto, garantindo recuperação imediata assim que o broker voltar.
   - Não subscreve tópicos de telemetria nesta entrega.
3. **Sonda PostgreSQL (`PostgresReadinessProbe`):**
   - Abre conexão real com `NpgsqlDataSource` e executa `SELECT 1;` com `CancellationToken` de timeout curto, sem expor detalhes de host/credenciais em caso de falha.
4. **Isolamento e segurança:**
   - Portas em `infra/compose.yaml` publicadas exclusivamente em `127.0.0.1`.
   - Mosquitto configurado com `allow_anonymous false`, `password_file /mosquitto/secrets/passwd` e `acl_file /mosquitto/config/acl.conf`.
   - Testes de integração utilizam containers reais e verificam todos os cenários da tabela de validação obrigatória da Entrega B.
