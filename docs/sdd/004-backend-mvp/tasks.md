# Execução

- [x] Persistência, migration e seed (banco real isolado; seed repetido sem duplicação).
- [x] Validação, ingestão idempotente e consultas REST (schema/tópico, duplicatas, paginação, stale).
- [x] Auth local e permissões (401/403/202 executados).
- [x] Comandos/ACK/dispatcher e timeout (idempotência, conflito, expiração, ACK tardio).
- [x] Alertas e SignalR (reconhecimento preserva causa; evento recebido por WebSocket autenticado).
- [x] Simulador e testes reais (duas conexões, RESERVOIR_LOW, LWT e reconexão após restart do broker).
- [x] Runbook e evidências; BE-009 identificado como parcial no envio remoto, sem alegar aplicação na placa.

- [ ] Integração de transporte das configurações com firmware: depende de contrato remoto ainda não definido. NOT_SENT é obrigatório até essa integração.

Evidências: `docs/execution/002-EVIDENCIAS-BACKEND-MVP.md`. Testes de hardware e de frontend não foram executados.
