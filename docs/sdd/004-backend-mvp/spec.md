# SDD 004 — conclusão incremental do backend MVP

Estado: execução autorizada em 08/11/2026. Codex assume implementação do backend; Antigravity permanece no frontend. Preservar arquivos existentes sem commit/push.

Decisões do usuário: zonas funcionais; autenticação local viewer/operator, senha com hash e acesso por token, sem cadastro público.

## Contratos do recorte

- Zonas: ENERGY_MOBILITY / Energia e Mobilidade; WATER_IRRIGATION / Hidráulica e Irrigação. UUID interno, deviceKey nas rotas REST.
- GET de devices, zones, telemetry, alerts e commands requer viewer ou operator. Escritas requerem operator. Login local POST /api/v1/auth/login, sem registro público. Usuários iniciais provisionados explicitamente com secrets de ambiente, nunca senhas padrão.
- Telemetria preserva envelope v1 e origem/qualidade; aceita measurements numéricas/booleanas e actuators como fatos reportados, sem inferir potência solar nem vazão. Métricas desconhecidas não recebem unidade inventada. Payloads de energia novos permanecem fixtures de laboratório até revisão coordenada do contrato.
- Persistir mensagem única por (device, messageId), todas as métricas e projeção na mesma transação. messageId deve ser único entre boots; nunca usar só contador reiniciado. Duplicatas não rejuvenescem lastSeen. Falhas de banco não confirmam PUBACK; reentrega só após recuperação. Não prometer durabilidade de sessão que não tenha sido testada.
- Status retained online=true não atualiza último contato nem comprova presença atual. Offline/LWT invalida presença. Telemetria nova válida comprova contato. TTL local configurável 30s; isStale separado de quality original. Timestamp não sincronizado não vira horário físico confiável.
- Mensagens de backlog com timestamp sincronizado antigo permanecem no histórico sem rejuvenescer presença nem substituir projeção mais recente. Freshness usa observedAt confiável, com fallback receivedAt se indisponível; horário mais de 30s no futuro é rejeitado. quality STALE continua stale independentemente da recepção recente.
- Histórico: receivedAt UTC, intervalo [from,to), page>=1, pageSize=1..200 padrão 50, ordenação receivedAt DESC, id DESC. Parâmetros inválidos => 400 ProblemDetails; device desconhecido =>404.
- MQTT ingress limitado a 64KiB; valida versão, ID do tópico, origem, qualidade, tipos e limites. Nenhum valor inválido é convertido silenciosamente em zero. Ações e ACKs de outro dispositivo são recusados.
- Comandos: PUMP_PULSE(durationMs inteiro 1..3000, limite provisório de laboratório), SET_LIGHT_MODE(mode AUTO/MANUAL), SET_LIGHT_MAIN(on boolean), REQUEST_CROSSING(parâmetros vazios), SET_IRRIGATION_MODE(mode AUTO/MANUAL). ACK_FAULT só após regra de reconhecimento definida; não habilitar genericamente.
- POST aceita commandId opcional UUID para retry idempotente do cliente. Mesmo ID/conteúdo/autor => mesmo comando; conteúdo ou autor divergente =>409. requestedBy vem exclusivamente da identidade. Persiste PENDING e retorna 202 sem depender de publicação. Expiração técnica padrão 10s, timeout de ACK 15s após publicação, configuráveis.
- Dispatcher reserva tentativa de publicação no banco antes de enviar para evitar dois publishers; PUBLISHED só após retorno de publicação do broker. Não republicar automaticamente uma publicação ambígua de comando físico. ACK RECEIVED não é execução; APPLIED/REJECTED/EXPIRED são terminais. ACK duplicado não repete evento; ACK tardio pode ser auditado sem apagar TIMED_OUT. observedState nunca comprova vazão.
- SignalR /hubs/city autenticado, eventos existentes após commit, REST permanece fonte persistente. Reconexão exige novo snapshot.
- Alertas persistem água baixa, qualidade INVALID e ausência de contato. Reconhecer não resolve causa; causa cessada resolve condição. Escrita auditada.
- BE-009 depende de contrato de transporte de configuração ainda inexistente: não inventar ação MQTT. Só implementar armazenamento versionado dos limites documentados; não afirmar que a placa recebeu configuração. Demais limiares/automações não definidos permanecem bloqueados.

## Segurança e operação

Bootstrap/migrations são comando explícito; nunca apagar banco nem aplicar migração destrutiva automática. Testes usam banco isolado, IDs MQTT separados e nenhuma parada dos containers existentes. Loopback nesta fase; CORS allowlist configurável; rate limit em login/escritas. A API local não deve ser publicada sem TLS/configuração de produção.

## Aceite

Banco real: migration reprodutível, seed idempotente, duplicata concorrente, rollback de ingestão, consulta paginada e stale. Broker real: dois simuladores, tópicos/versionamento/rejeição, reconexão, ACL, ACK recusado. API: auth 401/403/202, idempotência 409, snapshots, eventos SignalR, reconhecimento de alerta mantendo causa. Testes existentes continuam verdes. Nenhuma tarefa composta marcada completa por entrega parcial.
