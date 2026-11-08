# Cidade Viva — pacote 001 para Antigravity

Data: 07/11/2026. Estado: preparado para execução; nenhuma implementação ou teste executado por este pacote.

## 1. Objetivo e decisões vigentes

Preparar o desenvolvimento incremental da maquete acadêmica de cidade inteligente, começando pela organização documental e pela fundação local do backend.

Decisões explícitas do Douglas nesta conversa prevalecem sobre a organização antiga:

- Codex analisa, especifica, planeja e revisa. Antigravity executa todas as camadas.
- Três repositórios públicos independentes, sem submodules, subtrees ou Git aninhado:
  - https://github.com/ddouglss/cidade-viva-backend
  - https://github.com/ddouglss/cidade-viva-frontend
  - https://github.com/ddouglss/cidade-viva-firmware
- O antigo `cidade-viva-platform` foi renomeado para `cidade-viva-backend`.
- A divisão em dois repositórios e a atribuição do backend ao Codex no prompt mestre v1.2 foram superadas. Stack, regras e contratos permanecem válidos.
- Não editar o pacote SDD original nem o documento-mestre do Drive durante a execução.
- Este pacote não autoriza push, publicação de código, criação de PR ou deploy. Preparar alterações locais para revisão; commits/push ficam para instrução específica.

## 2. Diagnóstico confirmado e fontes

O workspace de origem contém Markdown; não foram encontrados projetos de aplicação, manifests, migrations, testes, pipelines ou configuração de containers. Tecnologias e arquitetura estão especificadas, ainda não implementadas. Não há `.git` na raiz inspecionada.

Raiz de origem:

`C:\Users\ddoug\projetos\Cidade_Viva_SDD_v1.1\Cidade_Viva_SDD_v1.1`

Ler antes de executar:

1. `AGENTS.md` e `README.md`.
2. Todos os documentos de `00_COMPARTILHADO/`; o arquivo `08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md` é referência histórica e não prevalece sobre decisões mais recentes.
3. `01_PLATAFORMA_DIGITAL/backend_dotnet/{README.md,ARCHITECTURE.md,sdd/001-mvp/spec.md,sdd/001-mvp/plan.md,sdd/001-mvp/tasks.md}`.
4. Equivalentes do frontend e dos dois firmwares para conhecer as fronteiras; não implementá-los nesta entrega.

Evidências principais:

| Tema | Fonte relativa à raiz de origem |
|---|---|
| Produto, P0/P1, fora do MVP | `00_COMPARTILHADO/01-VISAO-ESCOPO.md` |
| Tópicos, versão, QoS, retained, exemplos | `00_COMPARTILHADO/02-CONTRATOS-MQTT-V1.md` |
| Autoridade física, snapshot, segurança | `00_COMPARTILHADO/03-REGRAS-INTEGRACAO.md` |
| Hardware não homologado e BL-01 a BL-09 | `00_COMPARTILHADO/04-INVENTARIO-E-DECISOES-PENDENTES.md` |
| Arquitetura aceita | `00_COMPARTILHADO/05-ADRS.md` |
| Aceite e evidências | `00_COMPARTILHADO/06-PLANO-VALIDACAO-END-TO-END.md` |
| BE-001 a BE-010, B001 a B014 | `01_PLATAFORMA_DIGITAL/backend_dotnet/sdd/001-mvp/` |

Fluxo especificado para etapas posteriores:

Angular → API autenticada → comando persistido PENDING → MQTT → validação local do dispositivo → ACK → persistência → SignalR. HTTP 202 representa aceitação, não execução. Telemetria → MQTT → validação → PostgreSQL → snapshot REST/eventos SignalR. Sem broker, proteções locais continuam no firmware.

## 3. Estrutura de destino

Usar como destino proposto `C:\Users\ddoug\projetos\cidade-viva`. Antes de criar, verificar se já existe e preservar qualquer conteúdo. A pasta `cidade-viva` não terá `.git`; cada filho terá seu próprio repositório. Clonar cada remoto vazio no filho correspondente; se já houver checkout, verificar seu remote e alterações antes de reutilizar. Não mover nem apagar a origem.

```text
cidade-viva/
├── cidade-viva-backend/
│   ├── AGENTS.md
│   ├── README.md
│   ├── ARCHITECTURE.md
│   ├── src/{CidadeViva.Api,CidadeViva.Application,CidadeViva.Domain,CidadeViva.Infrastructure}/
│   ├── tests/{CidadeViva.UnitTests,CidadeViva.IntegrationTests}/
│   ├── tools/device-simulator/                 # etapa futura
│   ├── infra/{compose.yaml,mosquitto/}
│   └── docs/{shared/,sdd/001-mvp/,execution/,decisions/}
├── cidade-viva-frontend/
│   ├── AGENTS.md
│   ├── README.md
│   ├── ARCHITECTURE.md
│   └── docs/{shared/,sdd/001-mvp/,execution/,decisions/}
└── cidade-viva-firmware/
    ├── AGENTS.md
    ├── README.md
    ├── docs/{shared/,execution/,decisions/,eletrica_e_solar/}
    └── apps/
        ├── esp32-energy/{README.md,PINOUT-PENDENTE.md,sdd/001-mvp/}
        └── esp32-water/{README.md,PINOUT-PENDENTE.md,sdd/001-mvp/}
```

Essa árvore é uma decisão de organização do pacote, não um novo requisito funcional. Criar diretórios de código somente na etapa autorizada correspondente; não criar bibliotecas vazias para módulos futuros. Frontend e firmware recebem somente documentação na primeira entrega.

### Distribuição da documentação

- Copiar `AGENTS.md` para cada raiz e adicionar a organização de três repos e a divisão atual de papéis; corrigir os caminhos de leitura nas cópias.
- Copiar `00_COMPARTILHADO/*.md` para `docs/shared/` nos três repos, preservando nomes e conteúdo. Essas são cópias de referência sincronizadas, não três contratos independentes.
- Backend: copiar seu README, ARCHITECTURE e `sdd/001-mvp/` para os destinos equivalentes da árvore.
- Frontend: fazer o mesmo com sua documentação.
- Firmware: README da frente IoT na raiz, documentos de cada dispositivo nas respectivas apps; documento elétrico em `docs/eletrica_e_solar/`.
- Incluir este pacote em `docs/execution/` nos três repos.
- Criar em `docs/decisions/` um registro da organização aprovada e um mapa origem → destino. Corrigir links relativos das cópias sem mudar requisitos nem IDs.
- Registrar em cada repo a origem e SHA-256 dos documentos compartilhados; comparar as três cópias. Evitar alterações mecânicas no conteúdo compartilhado que impeçam comparação. Mudança futura de contrato exige revisão coordenada e sincronização antes de integrar.
- README de cada repo deve explicar responsabilidade, relação com os outros dois, ordem de leitura e estado real de implementação.
- Não publicar automaticamente os arquivos. Referências pessoais ou links internos dos documentos devem ser identificados para revisão antes de um futuro push público.

## 4. Entrega A — organização documental

Executar primeiro e apresentar resultado para revisão antes de iniciar a entrega B.

1. Inspecionar a pasta de destino, Git e acesso aos três remotos; não sobrescrever arquivos existentes.
2. Preparar os três checkouts independentes e distribuir a documentação conforme seção 3.
3. Registrar decisões superadas e preservar o histórico documental de origem.
4. Verificar caminhos/links locais, integridade das cópias compartilhadas e remotes.
5. Entregar árvores, mapa de cópia, diff e pendências. Não marcar B001–B014, F001–F011, E001–E012 ou W001–W010 como concluídas: documentação não equivale à implementação.

Aceite A: três checkouts com remotes corretos; nenhum `.git` no diretório pai; nenhuma aplicação gerada; origem preservada; SDD acessível a partir de cada checkout; contratos iguais nas três cópias.

## 5. Entrega B — fundação do backend (após revisão de A)

Escopo: B001, B002 e B003; BE-001 e fundação técnica de BE-010. BE-010 permanece incompleto até seed e simulador testados.

Antes de implementar, preparar uma especificação curta de fundação em `docs/sdd/002-fundacao-local/{spec.md,plan.md,tasks.md}`, vinculada à SDD 001, sem substituí-la. Registrar versões exatas e decisões de execução. Verificar no ambiente e em fontes oficiais versões suportadas/compatíveis de SDK .NET LTS, EF Core, provedor PostgreSQL e cliente MQTT. Não escolher por lembrança nem usar tags `latest`.

Implementação delimitada:

- Solução com os quatro projetos da árvore, DI e dependências para dentro. Domínio sem dependência de EF/HTTP/MQTT. Organização futura por módulos, sem implementar Control, Alerts e Automation agora.
- Fixar SDK e dependências, nullable/analyzers e comandos de restore/build/test reproduzíveis.
- Compose apenas para PostgreSQL e Mosquitto locais, com persistência em volumes e versões fixadas. API roda pelo SDK nesta fase; Dockerfiles de produção ficam para depois.
- Credenciais externas; `.env.example` somente com placeholders; `.gitignore` cobrindo `.env`, secrets, dados e artefatos. Gerar arquivos de senha do broker localmente por procedimento documentado, sem versionar credenciais ou hashes de senha de uso real.
- MQTT autenticado, sem acesso anônimo; separar credenciais de backend e simulador. Usar ACLs por tópico/serviço. Bind dos serviços publicados em loopback nesta fase; acesso LAN para ESP32 fica para etapa posterior. Não abrir firewall ou rede pública.
- Configuração validada no boot sem registrar valores secretos. OpenAPI, erros consistentes/Problem Details e logs estruturados com correlação.
- Implementar somente GET `/api/v1/health/live` e GET `/api/v1/health/ready`. Live verifica processo; ready verifica conexão real com PostgreSQL e sessão autenticada MQTT, retornando 503 quando indisponível. Detalhes sensíveis não aparecem na resposta.
- Cliente MQTT com conexão/reconexão suficiente para readiness, sem subscrever ou ingerir telemetria nesta entrega. Falha de dependência deve permitir processo vivo e diagnóstico; configuração obrigatória ausente deve impedir boot com erro claro e sem segredo.
- Documentar inicialização, geração de secrets locais, encerramento sem apagar volumes e diagnóstico. Nunca executar `down -v` ou remover dados existentes automaticamente.

Não implementar nesta entrega: tabelas de domínio, seed de zonas, ingestão, endpoints de devices/comandos, autenticação de usuários, SignalR funcional, frontend, simulador, firmware, GPIO, CI/CD, registry, hospedagem ou deploy.

### Validação obrigatória de B

| Caso | Resultado esperado |
|---|---|
| Restore/build/test | Executados com versões registradas; sem falhas |
| Serviços locais disponíveis | Live 200 e ready 200, com PostgreSQL e MQTT reais |
| PostgreSQL indisponível | Live 200 e ready 503 |
| MQTT indisponível | Live 200 e ready 503 |
| Dependências recuperadas | Ready volta a 200 sem reinício manual da API |
| Credencial MQTT inválida ou cliente anônimo | Conexão recusada; API não fica ready com credencial inválida |
| Configuração obrigatória ausente | Boot falha com diagnóstico sem segredo |
| Segurança dos arquivos e portas | Secrets ignorados; somente loopback publicado |

Registrar testes automatizados de integração para os comportamentos relevantes de readiness/reconexão, usando serviços reais e credenciais de teste externas. Não substituir integração real por mocks e chamá-la de validada. Usar ambiente isolado de testes, sem interromper serviços de outros projetos. Sem Docker/SDK/acesso disponível, registrar bloqueio e teste não executado, nunca sucesso presumido.

Aceite B não é aceite ponta a ponta de telemetria. B001–B003 só recebem `[x]` quando todas as suas partes aplicáveis estiverem executadas e evidenciadas; senão manter pendentes e explicar a cobertura parcial.

## 6. Lacunas identificadas e limites para a próxima etapa

| Item | Evidência e tratamento |
|---|---|
| SDD dos componentes está PROPOSTO | Não tratar cada detalhe técnico como contrato fechado. Este pacote delimita A/B; regras não definidas devem voltar para especificação. |
| REST tem rotas, mas não DTOs completos | Antes de integrar Angular, especificar respostas, erros, paginação, filtros e semântica de `{id}` UUID vs device key. Não inventar agora. |
| Autenticação tem roles, mas não fluxo | Definir emissão/validação de credenciais e bootstrap de usuário antes de endpoints de negócio. Não escolher provedor externo nem endpoint de login sem especificação. |
| MQTT contém exemplos e ações propostas | Faltam payloads completos de energia/status, regras normativas de ACK/transições e parâmetros por ação. Backend consome ACK; trecho do plano que diz publisher de ACK não deve inverter a direção do contrato. |
| Origem no frontend | Arquitetura menciona `source`, contrato usa `origin`; mapear explicitamente depois, sem renomear o payload MQTT. |
| Política temporal | TTL de stale, timeout, ACK tardio e deduplicação após reboot precisam ser fechados antes do fluxo de comandos. Exemplos com data fixa não servem como comandos válidos atuais. |
| Campos de ADC bruto | Contrato pede campo apropriado, mas não define nome/unidade normativos; requer especificação, não improvisação. |
| Zonas | Seed pede zonas, mas catálogo/associação não estão fechados; não criar regra de negócio para preencher lacuna. |
| Índice de leituras | Referência histórica usa observed_at; arquitetura do componente usa received_at. Seguir componente e justificar índice conforme consultas na etapa de persistência. |
| Segurança física | Pinouts, modelos, calibração e ensaios permanecem pendentes. Não bloqueiam A/B; bloqueiam fiação e alegações de validação física. |
| Laboratório vs físico | Simulador deve usar ambiente/credenciais isolados, IDs previstos e `SIMULATED_INPUT`, sem colisão com dispositivos reais. |

Nenhuma dessas lacunas autoriza ampliar o escopo de B. Não implementar contratos incompletos e pedir revisão somente depois.

## 7. Sequência posterior — planejamento, não execução deste pacote

1. Fechar especificação dos contratos e fixtures necessários ao primeiro fluxo de telemetria, incluindo REST mínimo, status e semântica de tempo.
2. Persistência mínima de Devices/Telemetry com migrations reais e unicidades; seed apenas com zonas definidas. B004/B005 não ficam completos enquanto faltar o restante de seu escopo.
3. MQTT → validação → persistência → consulta REST; simulador em `tools/device-simulator` no backend, sem importação de código de outro repo. Runtime definido e fixado nessa etapa.
4. Testar os dois IDs, origem simulada, duplicatas, payload incompatível, dispositivo divergente do tópico, desconexão/reconexão e leitura stale.
5. Fechar autenticação, comandos e ACK; testar PENDING/202, expiração, recusa `RESERVOIR_LOW`, idempotência e timeout. Limites de laboratório da água são provisórios, nunca homologação física.
6. SignalR e frontend, com snapshot na reconexão e sem estado otimista de atuador.
7. Firmware e bancada após identificação/aprovação do hardware. Simulação não comprova GPIO, circulação de água, geração solar ou corrente.

## 8. Formato de retorno obrigatório do executor

- Entrega executada e IDs de requisitos/tarefas efetivamente atendidos.
- Caminho de cada checkout, remotes, arquivos criados/alterados e diff.
- Decisões técnicas com motivo e versões exatas verificadas.
- Comandos executados, resultado, exit code e evidências sanitizadas.
- Testes executados, falhas e testes não executados com motivo.
- Impactos em contrato, energia e compra; para A/B, confirmar se inexistentes.
- Tarefas concluídas com evidências e tarefas ainda parciais/pendentes.
- Bloqueios, perguntas que alteram a próxima implementação e próximo passo proposto.

Não enviar secrets, dump de ambiente ou arquivos `.env` no retorno. Não afirmar fluxo ponta a ponta completo sem executá-lo. Parar ao terminar a entrega autorizada.

## 9. Prompt para colar no Antigravity agora

> Execute somente a ENTREGA A do arquivo `03_EXECUCAO/001-PACOTE-ANTIGRAVITY.md` no pacote Cidade Viva SDD localizado em `C:\Users\ddoug\projetos\Cidade_Viva_SDD_v1.1\Cidade_Viva_SDD_v1.1`. Leia o pacote e suas fontes primeiro. Codex planeja e revisa; você executa. A organização atual aprovada é de três repositórios públicos independentes: `ddouglss/cidade-viva-backend`, `ddouglss/cidade-viva-frontend` e `ddouglss/cidade-viva-firmware`. Prepare os checkouts e a documentação conforme o mapa, preservando a origem. Não gere aplicações, não implemente funcionalidades, não faça commit/push/deploy e não edite o documento-mestre. Verifique integridade documental, caminhos e remotes; apresente os resultados no formato da seção 8 e aguarde a revisão antes da entrega B.
