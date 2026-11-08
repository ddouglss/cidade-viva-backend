# Registro de decisões arquiteturais (ADRs)

## ADR-001 — Dois ESP32 por domínio
**Aceito conceitualmente.** `esp32-energy` cuida de mobilidade/iluminação e `esp32-water` cuida da hidráulica. Isola falhas e facilita demonstração. Revisar número real de GPIOs após identificação dos módulos existentes.

## ADR-002 — MQTT local + backend monólito modular
**Aceito.** MQTT desacopla dispositivos, .NET faz integração, auditoria e regras administrativas; Angular recebe atualização por SignalR. LAN local deve funcionar sem depender da internet.

## ADR-003 — Energia solar separada
**Aceito.** Quatro postes solares usam geração → controlador → bateria → 5 V regulados sem unir o positivo da fonte principal. Dois postes na fonte geral. Solar não é fonte para bomba ou ESP32 no MVP.

## ADR-004 — Água demonstrativa com paisagismo artificial
**Aceito.** Ciclo de água real, retorno em bandeja impermeável. A automação de umidade depende de amostra pequena de substrato real ou modo `SIMULATED_INPUT` explicitamente exibido.

## ADR-005 — Montagem sem solda
**Aceito como requisito de compra e execução.** Preferir headers prontos, módulos MOSFET com bornes e conectores dimensionados. Quando o componente exigir solda, registrar impedimento e buscar equivalente pronto; não improvisar terminais de força em protoboard.

## ADR-006 — SDD por componente
**Aceito.** Cada subprojeto possui `spec.md`, `plan.md` e `tasks.md`. Alterações de MQTT e requisitos transversais exigem revisão conjunta.

## ADR-007 — Arduino existente não substitui ESP32
**Pendente de inventário.** Arduino antigo pode apoiar testes ou cancela, mas não altera o compromisso dos dois ESP32 antes de confirmar o modelo efetivamente disponível.
