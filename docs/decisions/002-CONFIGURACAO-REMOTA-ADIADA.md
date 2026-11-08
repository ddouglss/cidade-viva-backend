# Decisão — configuração remota de irrigação adiada

Data: 08/11/2026. Estado: aprovado explicitamente pelo usuário neste chat.

O MVP local pode ser encerrado com limites de irrigação versionados e auditados no backend e `deliveryStatus=NOT_SENT`. O usuário confirmou: “Sim, mantenha NOT_SENT até integrar o firmware”.

O endpoint de atualização e a restrição de novas solicitações da API permanecem disponíveis. Nenhuma configuração é declarada recebida ou aplicada pela placa. Boia, timeout e demais proteções continuam sob autoridade do firmware.

O envio remoto de configuração (parte integrada de BE-009) fica como pendência aceita para a integração do firmware, após definição e revisão coordenada do contrato. Esta decisão delimita a entrega atual, não representa conclusão do fluxo remoto nem altera schemaVersion=1.0.

Referências: SDD 004; `docs/api/MVP-LOCAL.md`; `docs/execution/002-EVIDENCIAS-BACKEND-MVP.md`.
