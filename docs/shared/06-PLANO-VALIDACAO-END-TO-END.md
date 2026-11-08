# Plano de validação ponta a ponta

## Testes de contrato (antes do hardware)
- [ ] Fixture `PHYSICAL_SENSOR` é aceita com dados válidos; `SIMULATED_INPUT` aparece como simulação.
- [ ] Telemetria duplicada não duplica amostra/evento onde a idempotência é necessária.
- [ ] `POST` comando retorna `202`, ID e status; UI mostra solicitado/aguardando.
- [ ] ACK rejeitado por `RESERVOIR_LOW` chega ao banco e à interface.
- [ ] Duplicate `commandId` não repete pulso.
- [ ] Comando expirado não é aplicado.
- [ ] Firmware desconectado é marcado offline e dados antigos não parecem atuais.
- [ ] Reconexão SignalR obtém novo snapshot.

## Testes na bancada (medir, documentar, não presumir)
- [ ] Alimentação externa sem AC exposta; isolamento, polaridade, corrente e fusíveis revisados.
- [ ] LEDs, buzzer, sinaleiros e componentes existentes identificados e testados com seus resistores/drivers.
- [ ] Bomba em 5 V com driver apropriado, medição de corrente de partida e de regime.
- [ ] Boia desconectada/água baixa impede bomba, inclusive em modo manual.
- [ ] Timeout local força OFF mesmo com MQTT travado.
- [ ] Semáforos nunca exibem fase verde conflitante; travessia acessível respeita transição.
- [ ] Quatro postes solares operam pela bateria sem fonte principal; medir tensão e duração.
- [ ] Não há retorno de água sobre madeira, sensores ou terminais.
- [ ] Dois ESP32 ligados por USB/entrada adequada mantêm Wi-Fi ao ligar bomba.

## Evidências
Foto geral de bancada, fotos de componentes e etiquetas, relatório de medição com instrumentos, logs MQTT, captura da API e do dashboard, vídeo de ação física e casos de falha. Teste não executado permanece `PENDENTE`.

## Definition of Done
Especificação aceita; testes relevantes rodados; nenhuma falha P0 aberta; revisão elétrica/pinagem registrada; as evidências vinculadas; documentação e tarefas atualizadas.
