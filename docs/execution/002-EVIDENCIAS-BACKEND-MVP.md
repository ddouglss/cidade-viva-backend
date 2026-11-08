# Backend MVP — execução Codex em 08/11/2026

## Escopo e decisões

SDD 004 implementa o backend local após a fundação SDD 002. Usuário confirmou zonas funcionais e auth local viewer/operator sem registro público. Codex assumiu backend; frontend e firmware não foram modificados.

## Verificações executadas

- `dotnet tool restore`: EF CLI 10.0.14 fixado no manifesto.
- `dotnet build CidadeViva.sln --no-restore --verbosity quiet`: 0 warnings, 0 erros.
- `dotnet test CidadeViva.sln --no-build`: 36 unitários + 5 integrações aprovados no estado verificado; inclui banco/broker reais, WebSocket SignalR e execução do simulador real.
- Integração cria banco `cv_backend_it_<uuid>` e broker `cv-mqtt-it-<uuid>` exclusivos e os remove ao finalizar. Os containers de desenvolvimento não são interrompidos pelos testes.
- Migrations reais aplicadas e seed executado duas vezes no teste sem duplicar dispositivos/zonas.
- Autenticação: sem token =>401; viewer escrevendo =>403; operator POST=>202 PENDING; login emite token opaco.
- Telemetria origin SIMULATED_INPUT, receivedAt e observedAt nulo sem timestamp; duplicata não aumenta leituras. Schema incompatível e divergência tópico/payload são rejeitados, consumidor continua.
- Comando: mesmo UUID não duplica; conteúdo divergente =>409; ACK RESERVOIR_LOW consumido e duplicata ignorada. Pulso sem publicação expira; publicação sem ACK chega a TIMED_OUT; ACK tardio é auditado sem apagar timeout.
- Reconhecer alerta mantém causa ativa; limite versionado valida expectedVersion e restringe pulso na API. Auditoria persistida.
- Simulador real publica os dois dispositivos, responde recusa por boia e gera LWT dos dois clientes ao encerrar. SignalR entrega DeviceTelemetryUpdated por WebSocket autenticado.
- Testes existentes de live/ready, falha/retorno das dependências e credenciais inválidas continuam passando.
- Restart real do broker isolado: duas placas simuladas e backend reconectam; snapshot contém novas telemetrias recebidas após o restart. O teste encontrou e protege a correção de PUBACK em contexto de conexão encerrada.
- Telemetria histórica atrasada é persistida sem sobrescrever o snapshot mais novo nem recolocar dispositivo offline como online. Teste mantém pumpOn=false diante de backlog antigo com pumpOn=true.
- Verificação com API Kestrel em 127.0.0.1:5080 e simulador: readiness Ready; ambos online, SIMULATED_INPUT; comando PENDING → REJECTED / RESERVOIR_LOW. Tokens/senhas não foram incluídos nesta evidência.
- `.env` e diretório `.keys` confirmados como ignorados pelo Git.

## Persistência e impactos

Tabelas implementadas: zones, devices (catálogo e projeção corrente juntos), telemetry_messages, sensor_readings, device_commands, command_acknowledgements, alerts, users, automation_rules e system_events. Projeção junto a devices evita uma tabela 1:1 sem necessidade atual; mensagem separada garante unicidade da mensagem inteira e transação atômica com leituras. Migrações seguintes preservam dados e alinham nomes SQL em snake_case. Não há schemaVersion MQTT novo, alteração elétrica, de energia ou de compra.

## Limitações explícitas

- BE-009: limites ficam versionados e auditados no backend, NOT_SENT; transporte de configuração à placa não foi inventado.
- Hardware não foi conectado, gravado ou homologado. Nenhuma evidência de água circulando/solar/atuador físico foi produzida.
- Contratos compartilhados não foram alterados; campos adicionais usados pelas fixtures de energia e parâmetros de ações estão documentados no recorte local e precisam de revisão na integração física.
- UI não foi integrada nem testada neste trabalho. REST/OpenAPI/realtime estão disponíveis para o executor do frontend.
- Infraestrutura é local/loopback; deploy, TLS externo, operação de produção, retenção de histórico e gestão completa de usuários não fazem parte desta entrega.
- Sem commit, push, PR ou deploy.
