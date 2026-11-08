---
title: "CIDADE VIVA — SMART CITY LAB"
subtitle: "Especificação técnica integrada para desenvolver hardware, firmware, API e dashboard"
author: "Documento de trabalho do projeto — revisão técnica"
date: "06/11/2026"
lang: pt-BR
---

**GUIA DE LEITURA:** seções 1–4 (escopo e requisitos); 5–9 (eletricidade, solar, água e GPIO); 10–16 (MQTT, API, banco, firmware e Angular); 17–23 (execução, testes, inventário, riscos e referências).

> **Versão 1.0 | ESPECIFICAÇÃO PARA DESENVOLVIMENTO — NÃO É PROJETO ELÉTRICO EXECUTIVO.** Este documento consolida decisões comunicadas pela equipe até 06/11/2026. O software pode ser desenvolvido com simuladores imediatamente; esquemas físicos definitivos, valores de proteção e pinagem devem ser aprovados após identificar componentes reais e efetuar testes em bancada. A montagem elétrica deve ser conferida por profissional habilitado antes de energizar.

# 1. Objetivo do documento

Oferecer uma fonte de verdade para implementar do zero o **Cidade Viva**, maquete acadêmica de cidade inteligente com geração solar demonstrável, iluminação e mobilidade, irrigação física e dashboard conectado. Este caderno é dirigido ao desenvolvedor, aos responsáveis pela montagem e aos integrantes de pesquisa. Seu propósito é definir **o que construir, como dividir responsabilidades, como os sistemas se comunicam, como testar e o que ainda não foi homologado**.

As decisões recentes prevalecem sobre os PDFs preliminares de outubro/2026: (i) seis postes de referência, quatro alimentados por conjunto solar próprio e dois pelo circuito principal; (ii) horta com plantas artificiais e água circulando efetivamente; (iii) dois ESP32 D1 R32 dedicados a energia/mobilidade e água/irrigação; (iv) sensores de umidade podem usar uma amostra real de substrato em recipiente isolado ou **modo demonstrativo explicitamente identificado**; (v) preferência por terminais de parafuso e conectores prontos, **sem solda na montagem**; (vi) sistema solar funcional com bateria, mas **eletricamente separado** da fonte geral.

## 1.1. Estado das decisões

| Item | Decisão | Maturidade |
|---|---|---|
| Maquete | Base de referência 1000 × 800 mm e compartimento técnico inferior | Dimensão preliminar; medir bancada, peso e altura livre |
| Controle | Dois ESP32 D1 R32, Wi-Fi, firmware C++/PlatformIO | Escolha de compra; confirmar revisão física |
| Software | Angular + TypeScript, ASP.NET Core + C#, PostgreSQL, MQTT e SignalR | Aprovado conceitualmente |
| Iluminação | Seis postes: quatro solares isolados; dois no circuito 5 V principal | Aprovado como proposta funcional |
| Energia | Fonte externa 5 V CC; quadro com chave e proteção dos ramais | Correntes e fusíveis pendentes de ensaio |
| Solar | Painel compatível + Waveshare Solar Power Manager convencional + bateria Li-ion 14500 | Modelo exato e datasheets pendentes de homologação |
| Hidráulica | Mini bomba AM174 3–6 V (cotada), reservatório e retorno de água em bandeja impermeável | Corrente, vazão, bocal e driver a testar |
| Horta | Visualmente artificial; fluxo de água físico | Confirmado |
| Mobilidade | Dois sinaleiros, cancela/motor existentes e botão de travessia | Inventário / motores / alimentação a confirmar |
| Solda | Evitar: placas prontas, bornes, jumpers para bancada | Regra de projeto |

## 1.2. O que já existe e o que não está comprovado

**Informado como existente:** placa Arduino (modelo desconhecido), kit Arduino, motor da cancela, cancela, dois sinaleiros, LEDs/lâmpadas e resistores, alguns conectores, cabo Arduino↔computador e uma “plaquinha de energia” sem modelo identificado. *Ter um Arduino não significa ter dois ESP32.* Não há confirmação de que a plaquinha seja controlador de carga solar ou que os sinaleiros possuam eletrônica compatível com 3,3 V. **Não recomprar** itens existentes antes de contabilizar, medir e fotografar seu modelo.

**Ainda sem confirmação física:** os dois ESP32 D1 R32, BH1750, INA219, sensor de nível, sensor capacitivo de umidade, bomba AM174, controlador solar exato, painel, bateria, fonte, chave, proteções, MOSFETs, mangueiras, reservatório, bandeja e quantidade dos acessórios no kit. A planilha de orçamento existente é **cotação/estimativa**, não inventário de aquisição. Fazer a conciliação antes da compra.

# 2. Visão arquitetural

[Diagrama da especificação anterior — consultar imagens no Google Drive](https://drive.google.com/drive/folders/11OELjUJqA1BLYLc3bCtcDPqhmC8M3UO3)

**Princípio central:** os ESP32 executam lógica local e publicam fatos de estado. O broker MQTT transporta mensagens, a API registra e autoriza comandos, o banco persiste eventos e o Angular apresenta o resultado. O sucesso de um comando só pode ser indicado quando o dispositivo confirmar a **execução física**; a publicação MQTT por si só não é execução.

### Responsabilidades claras

1. **ESP32-01 (energia e mobilidade):** BH1750, INA219 no circuito designado, dois postes de alimentação principal, semáforos existentes, travessia acessível com botão e buzzer; opcionalmente cancela se houver driver e GPIO suficientes. Sistema solar com quatro postes pode operar autonomamente; não deve ser fundido à saída principal de 5 V.
2. **ESP32-02 (hidráulica):** sensor de nível mínimo, sensor capacitivo em módulo de teste real ou injeção de telemetria simulada identificada, controle de bomba por driver, limites de operação e eventos de falha.
3. **Backend:** cadastro de dispositivos, estado digital, comandos idempotentes, autenticação, histórico, regras configuráveis, eventos e integração MQTT.
4. **Frontend:** visão da cidade, telemetria em tempo real, modo auto/manual, controles de segurança, histórico, indicador de sistema offline e origem REAL/SIMULADA dos dados.
5. **Sistema solar autônomo:** painel gera energia, controlador carrega bateria, saída regulada energiza quatro postes. O desligamento da fonte principal não apaga os postes solares, desde que a bateria possua carga suficiente.

# 3. Requisitos funcionais, por prioridade

| ID | Prioridade | Requisito | Aceite |
|---|---|---|---|
| RF-01 | P0 | ESP32-01 conecta ao broker e reporta estado | Online/offline e versão de firmware visíveis |
| RF-02 | P0 | ESP32-02 conecta ao broker e reporta estado | Online/offline, boia e bomba visíveis |
| RF-03 | P0 | Controle manual da iluminação principal | API envia comando; ESP32 aplica e retorna ACK |
| RF-04 | P0 | Iluminação automática por BH1750 | Ligar/desligar por limiar com histerese e temporização |
| RF-05 | P0 | Semáforo com sequência segura | Não ocorrem verdes conflitantes no modelo |
| RF-06 | P0 | Travessia por botão + aviso sonoro | Solicitação entra na sequência segura, sem saltos |
| RF-07 | P0 | Irrigação manual e automática | Bomba não liga com reservatório vazio |
| RF-08 | P0 | Corte automático por tempo máximo da bomba | Desliga mesmo sem rede ou sem backend |
| RF-09 | P0 | Histórico real de comandos e leituras | Origem, estado, dispositivo e data consultáveis |
| RF-10 | P0 | Energia solar física independente | Quatro postes funcionam sem alimentação geral, com bateria carregada |
| RF-11 | P1 | Corrente/tensão da fonte principal via INA219 | Exibir medições reais no ramo instrumentado |
| RF-12 | P1 | Cancela integrada ao painel | Operação segura somente após identificar motor/driver |
| RF-13 | P1 | Alarmes/alertas de nível baixo e offline | Alerta com causa real e confirmação |
| RF-14 | P2 | Métrica solar em tempo real | Só habilitar após sensor de medição real adicionado |
| RF-15 | P2 | Qualidade de solo além de umidade | Somente após rubrica exigir e método ser comprovado |
| RF-16 | P3 | IA, aplicativo móvel, predição | Não bloqueiam apresentação acadêmica |

# 4. Restrições e critérios não funcionais

- **Segurança elétrica:** rede AC 127/220 V fica fora da maquete; usar fonte externa fechada/certificada e distribuir apenas baixa tensão CC. Nunca colocar fonte AC de bornes abertos sob o MDF.
- **Regulação:** GPIO de ESP32 opera em lógica de 3,3 V; não aplicar sinal de 5 V diretamente. Alimentar placas pela interface recomendada no modelo comprado; não injetar 5 V no pino 3V3.
- **Segregação:** tubulação e bandeja de água afastadas de quadro, fios e conectores. Reservatório removível; entrada de cabos com alívio de tração.
- **Baixa dependência:** regras de bomba, semáforo e proteção são locais; sem internet externa o projeto deve continuar na LAN; sem broker, os controles físicos locais continuam.
- **Observabilidade:** todo comando possui ID, origem, estados e motivo de recusa; leituras possuem horário e indicador de qualidade.
- **Privacidade/segurança lógica:** broker e API com autenticação, credenciais fora do código, rotas de escrita protegidas, não publicar MQTT sem controle na internet.
- **Acessibilidade:** comandos de painel com rótulos e estados textuais, sem depender apenas de cor; sequências sonoras e visuais inteligíveis na sala.
- **Reprodutibilidade:** testes repetíveis, parâmetros versionados, esquemas revisados e resultados reais medidos, nunca presumidos.

# 5. Alimentação elétrica principal

[Diagrama da especificação anterior — consultar imagens no Google Drive](https://drive.google.com/drive/folders/11OELjUJqA1BLYLc3bCtcDPqhmC8M3UO3)

**Topologia de referência:** tomada AC (externa) → adaptador AC/DC certificado 5 V (capacidade inicial de estudo 5 A, **não homologada**) → jack apropriado → proteção geral/interruptor CC → distribuição em borneiras de +5 V e 0 V → ramais protegidos para ESP32-01, ESP32-02 e atuadores. A ordem física entre chave e proteção deve ser confirmada no desenho revisado, preservando proteção próxima à entrada. Não utilizar protoboard como barramento definitivo de potência.

| Ramo | Função | Alimentação | Controle | Verificação pendente |
|---|---|---|---|---|
| F1 | ESP32-01 e sensores | 5 V via USB/entrada aprovada | Energia principal | Cabo/limite do conector e corrente de Wi-Fi |
| F2 | ESP32-02 e sensores | 5 V via USB/entrada aprovada | Energia principal | Corrente, ruído do motor e boot estável |
| F3 | Bomba e demais atuadores | +5 V com proteção individual e driver | GPIO comandando MOSFET/driver | Corrente de partida, diodo e valor de fusível |
| S1 | Quatro postes solares | 5 V REGULADOS do controlador solar | Chave e proteção próprias | Consumo total abaixo de 1 A; autonomia |

**Não interligar diretamente o positivo regulado da fonte principal com o positivo regulado da saída solar.** Se a equipe desejar comutação automática entre fontes futuramente, isso é outro circuito, com chaveamento apropriado e validação. No MVP, os quatro postes solares são ramo independente; o backend pode mostrar “solar demonstrativo” sem inventar medição em tempo real.

## 5.1. Dimensionamento antes de fechar fusíveis e cabos

O memorial anterior propunha aproximadamente 1,94 a 3,14 A como **reserva ilustrativa** das cargas gerais, mas não há medição de corrente de partida, consumo de sinaleiros e microcontroladores. Executar: medir corrente do ESP32 com Wi-Fi, bomba em partida e operação, conjunto de LED por circuito, sirenes e servo/motor da cancela; somar simultaneidade e margem; verificar queda de tensão nos cabos, bitola, conectores, aquecimento e seletividade da proteção. **Não copiar correntes de outros anúncios como especificação da AM174.**

## 5.2. Diodo e capacitores: uso real, não automático

- **Diodo Schottky 1N5822 (3 A, 40 V), como candidato** de flyback para motor DC simples: ligar reversamente em paralelo ao motor, de modo que permaneça bloqueado enquanto a carga recebe 5 V, quando a topologia pedir. **Não instalar sem verificar se a AM174 tem eletrônica própria e as instruções de seu fabricante.** Para servo e drivers inteligentes, proteção já pode estar integrada.
- **100 nF cerâmico 50 V:** desacoplamento de alta frequência perto de módulos que necessitem capacitor adicional; muitos módulos já o possuem.
- **10 µF / 100 µF / 470 µF eletrolíticos ≥16 V:** opções de filtragem de alimentação conforme ruído medido; **respeitar polaridade e temperatura**, usar bornes e suportes adequados; não são obrigatórios em todo ramal.
- **Driver MOSFET:** verificar que comuta corretamente com entrada de 3,3 V, corrente de carga e tensão 5 V; preferir placa pronta com bornes, indicação de pinos e proteção adequada. Não alimentar bomba diretamente por GPIO.
- **LEDs:** cada série/ramo exige resistor ou driver de corrente. Exemplo de LED vermelho típico Vf=2,0 V, alimentação 5 V e alvo 8 mA: R≈375 Ω; escolher valor comercial adequado, como 390 Ω. **Não usar 220 Ω indiscriminadamente** em todos os LEDs. A cor e o modelo alteram a queda de tensão.

# 6. Energia solar funcional

Fluxo **painel solar → controlador de carga apropriado → bateria recarregável protegida → conversão/regulação para 5 V → chave/ramal solar → quatro LEDs dos postes**, cada um limitado por resistor/driver. A bateria armazena energia; o painel não. Não conectar painel diretamente à bateria, GPIO ou entrada da placa ESP32.

**Referência de produto:** o **Waveshare Solar Power Manager (modelo convencional; não confundir com B/C/D)** aceita painel 6–24 V, bateria Li-ion 14500 3,7 V 850 mAh (não inclusa), entrada USB 5 V para carga, saída 5 V / 1 A, além de proteções integradas de carga/descarga. Conferir revisão exata e manual antes de comprar bateria e painel. Fonte: <https://www.waveshare.com/wiki/Solar_Power_Manager>.

O painel de **9 V / 2 W** é hipótese de compra que se enquadra na faixa de entrada publicada, mas sua eficiência e capacidade de carregamento dependem das condições reais de iluminação, orientação e tensão/potência no ponto de operação. O ajuste MPPT do módulo deve seguir o manual. Ambiente de sala pode não oferecer irradiância suficiente. Para demonstração, carregar previamente pelo método autorizado (USB do controlador) e mostrar geração solar quando houver luz adequada, sem afirmar que houve carga em sala quando não houver evidência.

**Cuidado com solda:** o modelo convencional inclui porta-bateria e interfaces, mas conferir se a conexão escolhida utiliza borne/jack/USB já instalado ou exige pinos soldados. O projeto **não pode pressupor conexões prontas que não foram verificadas**. A bateria deve ser de procedência e química/tamanho compatíveis, nunca uma pilha AA comum no suporte de Li-ion.

### Ensaios solares mínimos

1. Medir tensão em aberto do painel sob iluminação apropriada e confirmar dentro dos limites do gerenciador (o valor em vazio pode exceder a tensão nominal).
2. Energizar gerenciador sem carga externa conforme manual; verificar entrada USB, capacidade de carga e LEDs de status.
3. Instalar bateria compatível verificando polaridade, temperatura e proteções; observar processo de carga.
4. Energizar um LED com resistor pela saída 5 V, depois quatro; medir corrente total, tensão e aquecimento.
5. Desligar entrada solar e entrada USB: verificar quatro postes ligados **pela bateria**, pelo período de apresentação planejado.
6. Desligar **somente** a alimentação principal da maquete: postes solares permanecem acesos; não há retroalimentação do circuito principal.

# 7. Hidráulica, irrigação e umidade

[Diagrama da especificação anterior — consultar imagens no Google Drive](https://drive.google.com/drive/folders/11OELjUJqA1BLYLc3bCtcDPqhmC8M3UO3)

O paisagismo é **artificial**, mas a água percorre um circuito físico fechado: reservatório de 1–2 L → bomba de baixa tensão → mangueira dimensionada pelo bocal real → difusor/gotejamento visível → bandeja impermeável da horta → calha de retorno → reservatório. O reservatório tem boia de nível mínimo para impedir funcionamento a seco. A maquete deve conter a água mesmo se a bomba operar acima do fluxo nominal por alguns segundos. Nunca permitir escoamento solto sobre MDF.

### Como demonstrar “terra seca” sem falsificar a medição

**Opção A — recomendada para laboratório:** pequeno cartucho removível com substrato real, sensor capacitivo V1.2 inserido e testado. Esse cartucho é isolado da estrutura de MDF e pode receber umedecimento controlado. Plantas e canteiro visível permanecem artificiais. A lógica de irrigação lê o cartucho real, mas a água demonstrativa pode circular na horta artificial em bandeja separada. Registrar a distinção entre amostra instrumentada e paisagismo.

**Opção B — demonstração de software:** umidade inserida/simulada por função de teste; painel e telemetria devem exibir claramente `SIMULADA`. No modo simulação, bomba ainda tem intertravamento físico da boia e limite máximo. **Não apresentar isso como leitura de solo real.**

### Máquina de estados hidráulica

- `IDLE`: bomba desligada, condição segura inicial.
- `AUTO_ARMED`: regra automática ativa, observando umidade, nível e qualidade da leitura.
- `PUMPING`: bomba ligada; cronômetro local ativo.
- `COOLDOWN`: pausa mínima entre ciclos, para reduzir repetição e permitir retorno da água.
- `BLOCKED_LOW_WATER`: boia indica falta de água ou estado incerto; bomba desligada.
- `FAULT_SENSOR`: sensor exigido indisponível/desatualizado; bloquear decisão automática, registrar alerta.
- `FAULT_TIMEOUT`: bomba excedeu tempo máximo; desligamento imediato, exigir reconhecimento conforme política.

**Proposta inicial de laboratório (parametrizável, não ajustada):** pulso máximo de 3 s, pausa de 15 s, 1 pulso por acionamento, limiar de umidade calculado após calibração; não fixar 30% como leitura absoluta do sensor V1.2. Sensor capacitivo bruto deve ter calibração para seco/molhado, qualidade e estabilidade. Controles manuais **não** ultrapassam boia/tempo máximo. Na queda da rede, decisões físicas e proteções continuam no ESP32-02.

# 8. Semáforos, acessibilidade e cancela

Os dois sinaleiros existentes precisam ser identificados: LEDs individuais ou módulos prontos? Alimentação 3,3 V / 5 V / 12 V? Resistores incluídos? Pinos? Somente após a inspeção definir drivers e quantitativo de GPIO. O controlador não pode produzir estados que concedam passagem simultânea conflitante.

**Estados de referência:** `VEHICLE_A_GO`, `VEHICLE_A_YELLOW`, `ALL_RED_1`, `VEHICLE_B_GO`, `VEHICLE_B_YELLOW`, `ALL_RED_2`, `PEDESTRIAN_GO`, `PEDESTRIAN_CLEARANCE` e retorno ao estado seguro. Tempos configuráveis e validados em bancada. A travessia é uma **demonstração**, não um controlador homologado para vias públicas. O botão apenas registra pedido; a máquina de estados escolhe o momento seguro. O buzzer de acessibilidade tem padrão distinto do alarme de emergência.

**Cancela:** o motor informado pode ser motor DC ou servo. Antes de ligar, identificar modelo e tensão/corrente; **servo não deve ser comandado como motor DC com diodo e MOSFET simples**, e motor DC pode requerer ponte H e fins de curso. Como item P1, manter integração atrás de interface `BarrierActuator` e simulador até homologar o hardware.

# 9. Mapa preliminar de sinais e pinagem

O PlatformIO documenta a placa WEMOS D1 R32 com `board = wemos_d1_uno32`. **Nomes D2/D3/A2 não são números de GPIO**; devem ser mapeados pela revisão exata do fabricante. Os pinos de ADC2 conflitam com o Wi-Fi no ESP32 clássico; para umidade analógica priorizar entradas ADC1. Referências: <https://docs.platformio.org/en/stable/boards/espressif32/wemos_d1_uno32.html>, <https://docs.espressif.com/projects/esp-faq/en/latest/software-framework/peripherals/adc.html>.

| Controlador | Sinal lógico | GPIO de estudo | Observações e impedimentos |
|---|---|---|---|
| Energia | I²C SDA (BH1750/INA219) | 21 | 3,3 V; verificar endereços, pull-ups e tensão dos módulos |
| Energia | I²C SCL (BH1750/INA219) | 22 | Mesmo barramento; endereços precisam ser distintos |
| Energia | LIGHT_MAIN | 25 (D3) | Controla módulo de acionamento; não liga todos os LEDs direto |
| Energia | TRAFFIC_A_R | 26 (D2) | Provisório; corrente e tipo de sinaleiro a identificar |
| Energia | TRAFFIC_A_Y | 27 (D6) | Provisório |
| Energia | TRAFFIC_A_G | 16 (D5)* | *Verificar serigrafia/revisão; D5 varia em clones |
| Energia | TRAFFIC_B_R | 17 (D4)* | *Verificar exposição real de GPIO17 |
| Energia | TRAFFIC_B_Y | 14 (D7) | Provisório |
| Energia | TRAFFIC_B_G | 13 (D9) | Provisório |
| Energia | BUTTON_CROSSING | 19 (D12) | Botão para 0 V com pull-up apropriado; debouncing |
| Energia | BUZZER_CROSSING | 23 (D11) | Via driver se corrente exceder GPIO |
| Água | SOIL_ADC | 35 (A2) | ADC1, entrada apenas, tensão de saída limitada a 3,3 V |
| Água | LOW_WATER_SWITCH | 26 (D2) | Boia por entrada digital com pull-up; lógica fail-safe verificada |
| Água | PUMP_DRIVER | 25 (D3) | GPIO controla entrada de módulo MOSFET, nunca motor |

**Este é um MAPA LÓGICO PROVISÓRIO, não ordem de ligar fios.** GPIO para cancela, LEDs adicionais, sirene e sensor de nível remoto podem exigir um expansor I/O I²C (ex. MCP23017) ou revisão do mapa. GPIOs 0, 2, 5, 12 e 15 exigem cautela por condições de boot; GPIO34–39 são apenas entradas. Também conferir GPIOs reservadas à flash e ao USB/serial. Fixar pinagem em `firmware/docs/PINOUT-APPROVED.md` somente após foto do módulo, datasheet, teste de cada saída e revisão elétrica.

# 10. Protocolo MQTT — contrato v1

**Prefixo:** `cidade-viva/v1/{deviceId}`. Identificadores estáveis: `esp32-energy` e `esp32-water`.

| Tópico | Direção | Finalidade | QoS/Retain propostos |
|---|---|---|---|
| `cidade-viva/v1/esp32-energy/telemetry` | ESP → broker | Leitura de luminosidade, energia e saídas | QoS 1, retain NÃO |
| `cidade-viva/v1/esp32-water/telemetry` | ESP → broker | Umidade, nível, bomba e falhas | QoS 1, retain NÃO |
| `cidade-viva/v1/{deviceId}/status` | ESP/broker → backend | Online/offline, uptime, versão | QoS 1, retained SIM (LWT) |
| `cidade-viva/v1/{deviceId}/commands` | Backend → ESP | Ações com `commandId` e expiração | QoS 1, retain NÃO |
| `cidade-viva/v1/{deviceId}/ack` | ESP → backend | Resultado final do comando | QoS 1, retain NÃO |

### 10.1. Telemetria (exemplo de desenvolvimento)

```json
{
  "schemaVersion": "1.0",
  "messageId": "evt-000042",
  "deviceId": "esp32-water",
  "timestamp": "2026-11-06T18:00:00Z",
  "uptimeMs": 583290,
  "origin": "PHYSICAL_SENSOR",
  "measurements": {
    "soilHumidityPct": 37.2,
    "reservoirLow": false
  },
  "actuators": { "pumpOn": false },
  "mode": "AUTO",
  "quality": "VALID"
}
```

Os valores são **exemplos artificiais do contrato**, não leituras realizadas. `origin` permitido: `PHYSICAL_SENSOR`, `SIMULATED_INPUT`, `CALCULATED`. Se o relógio NTP estiver indisponível, usar `uptimeMs` e registrar tempo de recepção do servidor, sinalizando timestamp não sincronizado.

### 10.2. Comando

```json
{
  "schemaVersion": "1.0",
  "commandId": "2af57ed7-b653-41d8-9c56-6643a9f2f0b8",
  "deviceId": "esp32-water",
  "action": "PUMP_PULSE",
  "parameters": { "durationMs": 2500 },
  "requestedAt": "2026-11-06T18:00:05Z",
  "expiresAt": "2026-11-06T18:00:15Z",
  "requestedBy": "operator:demo"
}
```

### 10.3. Confirmação e semântica

```json
{
  "schemaVersion": "1.0",
  "commandId": "2af57ed7-b653-41d8-9c56-6643a9f2f0b8",
  "deviceId": "esp32-water",
  "status": "REJECTED",
  "reason": "RESERVOIR_LOW",
  "observedState": { "pumpOn": false },
  "uptimeMs": 584210
}
```

Estados: `PENDING`, `PUBLISHED`, `RECEIVED`, `APPLIED`, `REJECTED`, `EXPIRED`, `TIMED_OUT`. `APPLIED` requer aplicação física/observação do estado do atuador no nível possível; para prova de vazão efetiva seria necessário sensor de fluxo, que não faz parte do MVP. O ACK `APPLIED` significa que o GPIO de comando foi aplicado e o firmware não detectou falha, **não comprova vazão**. Em QoS 1 pode haver entrega duplicada: manter cache recente de `commandId`, nunca repetir pulso de bomba. O dispositivo deve verificar expiração com relógio confiável ou rejeitar comando remoto sem base de tempo válida. A aplicação pode reconsultar estado sem reenviar automaticamente comando perigoso.

# 11. API REST e comunicação em tempo real

| Método | Rota de referência | Uso |
|---|---|---|
| GET | `/api/v1/health/live` | Processo responde |
| GET | `/api/v1/health/ready` | Banco/broker acessíveis; política a definir |
| GET | `/api/v1/devices` | Catálogo e estado atual |
| GET | `/api/v1/devices/{id}` | Dados do dispositivo |
| GET | `/api/v1/devices/{id}/telemetry?from=&to=` | Histórico paginado |
| GET | `/api/v1/zones` | Setores da maquete |
| GET | `/api/v1/alerts` | Alertas e causas |
| GET | `/api/v1/commands/{commandId}` | Progresso do comando |
| POST | `/api/v1/devices/{id}/commands` | Validação, persistência, publicação |
| PUT | `/api/v1/automation-rules/{id}` | Atualiza parâmetros autorizados |
| POST | `/api/v1/alerts/{id}/ack` | Reconhece alerta, sem resolver causa |

**Exemplo:** `POST /api/v1/devices/esp32-water/commands` com `{ "action":"PUMP_PULSE", "parameters":{"durationMs":2500} }`. API responde **202 Accepted** com `commandId` e status `PENDING`, não “bomba acionada com sucesso”. O SignalR em `/hubs/city` transmite `DeviceTelemetryUpdated`, `DeviceStatusChanged`, `CommandStatusChanged` e `AlertRaised`; frontend registra listener uma vez e se recupera de reconexões consultando snapshot atual pela API.

### Validações do backend

- Validar autorização (ex.: `operator`, `viewer`); leitura e escrita separadas.
- Validar `deviceId`, lista positiva de `action`, parâmetros e limites (inclusive duração máxima da bomba).
- Aplicar idempotência `commandId`, correlation ID e timeout; transação para gravar pedido antes de publicar.
- Não permitir comando de outro módulo em tópico errado.
- Persistir ACK recebido de modo idempotente; rejeições não desaparecem da auditoria.
- Não considerar telemetria antiga como estado atual; marcar indisponível por TTL.

# 12. Modelagem PostgreSQL

**Tabelas sugeridas:** `zones`, `devices`, `device_state`, `sensor_readings`, `device_commands`, `command_acknowledgements`, `automation_rules`, `system_events`, `alerts`, `calibration_records`, `users`. Identificadores UUID no backend, timestamps `timestamptz` em UTC. Para amostras, índices por `(device_id, observed_at DESC)` e retenção/compactação adequadas ao volume.

| Entidade | Campos principais | Restrição importante |
|---|---|---|
| `zones` | id, code, name | Código estável |
| `devices` | id, device_key, zone_id, category, firmware_version, last_seen_at | `device_key` único |
| `device_state` | device_id, online, mode, outputs_json, updated_at | Um estado corrente por dispositivo |
| `sensor_readings` | id, device_id, metric, numeric_value, unit, quality, origin, observed_at, received_at | Preservar unidade e origem |
| `device_commands` | id, device_id, action, payload_json, requested_by, status, expires_at, requested_at | IDs únicos, status auditável |
| `command_acknowledgements` | id, command_id, result, reason, received_at | Não duplicar resultado |
| `automation_rules` | id, device_id, rule_type, enabled, config_json, version | Revisão de regra e limites |
| `system_events` | id, event_type, source_device, severity, event_at, payload_json | Eventos consultáveis |
| `alerts` | id, event_id, status, acknowledged_by, acknowledged_at | Reconhecer ≠ resolver |
| `calibration_records` | id, sensor_id, dry_raw, wet_raw, date, notes | Calibração versionada |

**Estratégia de persistência:** dados contínuos podem ser agregados por minuto; guardar eventos e ACKs integralmente; não gravar 10 leituras/s de todos os sensores sem justificativa. Migrações via EF Core, com índices e testes de integração PostgreSQL real.

# 13. Estrutura recomendada do repositório

```text
cidade-viva/
├── README.md
├── docs/
│   ├── 00-escopo.md
│   ├── 01-arquitetura.md
│   ├── 02-requisitos.md
│   ├── 03-mqtt-contracts.md
│   ├── 04-api-contract.md
│   ├── 05-hardware-pinout-APPROVAL-PENDING.md
│   ├── 06-electrical-validation.md
│   ├── 07-test-plan.md
│   ├── adr/
│   │   ├── 001-two-esp32.md
│   │   ├── 002-mqtt-local.md
│   │   └── 003-solar-isolated.md
│   └── evidence/
├── apps/
│   └── dashboard-angular/
│       ├── src/app/core/
│       ├── src/app/features/dashboard/
│       ├── src/app/features/devices/
│       ├── src/app/features/energy/
│       ├── src/app/features/water/
│       ├── src/app/features/mobility/
│       ├── src/app/features/alerts/
│       └── src/app/shared/
├── backend/
│   ├── src/CidadeViva.Api/
│   ├── src/CidadeViva.Application/
│   ├── src/CidadeViva.Domain/
│   ├── src/CidadeViva.Infrastructure/
│   ├── src/CidadeViva.Modules.Devices/
│   ├── src/CidadeViva.Modules.Telemetry/
│   ├── src/CidadeViva.Modules.Control/
│   ├── src/CidadeViva.Modules.Automation/
│   ├── src/CidadeViva.Modules.Alerts/
│   └── tests/
│       ├── UnitTests/
│       ├── IntegrationTests/
│       └── ContractTests/
├── firmware/
│   ├── energy-controller/
│   │   ├── platformio.ini
│   │   ├── include/
│   │   ├── src/
│   │   ├── test/
│   │   └── docs/PINOUT-APPROVED.md
│   ├── water-controller/
│   │   ├── platformio.ini
│   │   ├── include/
│   │   ├── src/
│   │   ├── test/
│   │   └── docs/PINOUT-APPROVED.md
│   └── simulator/
├── infra/
│   ├── compose.yaml
│   ├── mosquitto/
│   └── postgres/
└── scripts/
    ├── seed-demo-data/
    └── replay-mqtt-fixtures/
```

A estrutura modular deve respeitar **dependências para dentro**, casos de uso isolados, DI, interfaces para drivers e integrações, DTOs de contratos versionados, tratamento consistente de erros. Evitar construir “microserviços” reais para duas placas e uma maquete: um monólito modular reduz custo operacional sem sacrificar separação de responsabilidades.

# 14. Firmware ESP32: padrão de desenvolvimento

**Ambiente:** VS Code + PlatformIO; framework Arduino/C++ para ESP32; biblioteca Wi-Fi/MQTT, BH1750 e INA219 selecionadas com versões fixadas após testes. Exemplo de `platformio.ini` para a **WEMOS D1 R32 confirmada**:

```ini
[env:wemos_d1_uno32]
platform = espressif32
board = wemos_d1_uno32
framework = arduino
monitor_speed = 115200
```

**Importante:** o arquivo acima é válido para WEMOS D1 R32; clones ou placa Arduino Uno tradicional exigem outro perfil. A versão do `platform` e bibliotecas deve ser fixada no repositório depois de compilar na placa comprada. Gravação com cabo USB **de dados**; após firmware gravado, o programa roda ao ligar a alimentação, sem notebook conectado, salvo dependência de rede para comandos remotos.

## 14.1. Componentes de firmware

| Interface/classe | Responsabilidade |
|---|---|
| `BoardConfiguration` | Pinos validados, modelo, limite elétrico e parâmetros |
| `WifiConnectionManager` | Conectar/reconectar sem bloquear controle local |
| `MqttTransport` | Tópicos, sessão, LWT, QoS e publicações |
| `TelemetryPublisher` | Mensagens versionadas, qualidade, timestamps |
| `CommandDispatcher` | Validar ações, expiração e deduplicação |
| `LightController` | AUTO/MANUAL, histerese e acionamento |
| `TrafficStateMachine` | Semáforos e botão de travessia |
| `IrrigationStateMachine` | Bomba, boia, cooldown, falhas e timeout |
| `SensorHealthMonitor` | Queda de sensores, valor fora de faixa |
| `SafetySupervisor` | Saídas seguras no boot, falha e watchdog |

Pseudociclo **não bloqueante**:

```cpp
void setup() {
  configureOutputsToSafeOff();
  initializeSerialAndSensors();
  initializeWiFiAndMqtt();
  publishBootStatus();
}

void loop() {
  serviceNetwork();                 // Sem impedir proteção local
  readSensorsWhenDue();             // millis(), não delay() longo
  processValidatedCommands();
  evaluateLocalAutomation();
  enforceSafetyInterlocks();        // Nível e tempo máximo, sempre
  updateTrafficStateMachine();
  publishTelemetryWhenDue();
}
```

Nenhuma saída que ligue bomba/semáforo pode permanecer acionada após boot/reset sem transição explícita. *Fail-safe* para bomba é DESLIGADA; sinais de trânsito devem iniciar em estado de parada seguro estabelecido para os módulos e disponibilidade de alimentação. Sem acesso à boia/sensor ou com leitura inválida, não assumir água presente.

# 15. Frontend Angular — especificação de telas

| Rota proposta | Elementos visíveis | Interação |
|---|---|---|
| `/dashboard` | Planta 2D, online/offline, indicadores por zona | Navegar por setor |
| `/energia` | BH1750 (lux), LEDs principais, INA219, solar independente | AUTO/MANUAL e comando de iluminação principal |
| `/hidraulica` | Estado boia, umidade REAL/SIMULADA, bomba e ciclo | Pulse manual seguro, regra AUTO, reset de falha autorizado |
| `/mobilidade` | Estado de cada sinaleiro, solicitação de travessia, cancela P1 | Teste botão/operador e histórico de transição |
| `/dispositivos` | Modelo, firmware, lastSeen, eventos | Diagnóstico e filtros |
| `/alertas` | Causa, severidade, reconhecimento | Reconhecer sem apagar causa |
| `/historico` | Gráficos e linhas de tempo | Filtro UTC/local, exportação de evidências |
| `/configuracoes` | Limiar luz, histerese, segurança, usuários | Alterações auditadas |

Padrão Angular: standalone components, feature folders, services tipados, guards, HTTP interceptor, SignalR com fallback de snapshot, estados de carregando/erro/desatualizado. Evitar botões de “ligado” por estado otimista: mostrar **Solicitado → Aguardando → Confirmado/Recusado**. Painel precisa funcionar em notebook projetado e celular, com boa legibilidade.

# 16. Simuladores e desenvolvimento antes da compra

Não esperar chegar todo o hardware para iniciar o software. Criar `firmware/simulator` que publica eventos MQTT usando os **mesmos contratos e IDs de laboratório**, mas identificando `origin: SIMULATED_INPUT`, e recebe comandos respondendo ACK. Identificadores reais devem usar outras credenciais/ambiente ou chave de simulação. O frontend sempre exibe um chip discreto `SIMULAÇÃO` quando operando nesse modo.

**Cenários fixos de simulação:** luz ambiente cai e volta; solo seco; boia baixa; botão de travessia pressionado; firmware offline; MQTT reconecta; comando duplicado; comando expirado; GPIO sem confirmação; uma leitura inválida; retorno tardio de ACK. A API deve passar pelos mesmos casos de uso no modo simulado e no real.

# 17. Roteiro de implementação em etapas

## Etapa 0 — Fundação e contratos

- [ ] Criar repositório, README e ADRs; configurar CI (.NET/Angular/testes; firmware separado).
- [ ] Estabelecer contratos MQTT v1 com schemas e fixtures de JSON válidas/inválidas.
- [ ] Subir broker MQTT local, PostgreSQL e backend em Docker Compose; não expor MQTT publicamente.
- [ ] Criar projeto Angular com layout de monitoramento e rotas; configurar tipos do contrato.
- **Aceite:** `docker compose up` inicializa serviços locais, e fixture falsa chega do MQTT até endpoint de consulta.

## Etapa 1 — Backend, simulador e dashboard

- [ ] EF Core: Devices, Readings, Commands, Ack, Alert, Rule + migrations.
- [ ] Serviço MQTT (hosted background service), validação de mensagens e reconexão.
- [ ] API de catálogo, leituras, comandos e estados; SignalR.
- [ ] Simulador MQTT com dois dispositivos lógicos.
- [ ] Angular com cards de energia/água, comando manual e badges de qualidade.
- **Aceite:** comando criado no Angular aparece como `PENDING`, recebe ACK de simulador e muda para `APPLIED` sem intervenção.

## Etapa 2 — ESP32-01 real

- [ ] Identificar placa D1 R32, revisar pinagem e medir 3,3 V/5 V antes de conectar módulos.
- [ ] Gravar firmware mínimo; publicar heartbeat e receber comando sem carga.
- [ ] Integrar BH1750 e uma saída LED com resistor/driver adequado.
- [ ] Integrar INA219 no circuito DC adequado, comparar com multímetro.
- [ ] Integrar sinaleiros existentes, lógica sem conflito e botão de travessia.
- **Aceite:** alternar AUTO/MANUAL e demonstrar luminosidade real; botão inicia sequência segura; queda de broker não trava o semáforo.

## Etapa 3 — ESP32-02 real e água

- [ ] Identificar bomba AM174, tensão/corrente e bocal; escolher MOSFET pronto.
- [ ] Ler boia e validar falha aberta/curta no circuito escolhido.
- [ ] Instalar sensor de umidade em amostra real ou habilitar demo explicitamente.
- [ ] Ativar bomba por pulso curto, medir vazão, tempo, aquecimento e retorno estanque.
- [ ] Implementar modo AUTO/MANUAL, timeout local e cooldown.
- **Aceite:** bomba rejeita comando com reservatório baixo, desliga no tempo limite, gera ACK e evento visível.

## Etapa 4 — Energia solar, estrutura e acabamento

- [ ] Verificar se plaquinha de energia existente já resolve parte do circuito.
- [ ] Validar controlador solar modelo exato, bateria protegida, painel e saídas.
- [ ] Testar quatro postes solares sem fonte principal, sem juntar saídas de 5 V.
- [ ] Instalar fonte, quadro seco, proteções, etiquetas, canaletas e bandeja de água.
- [ ] Fixar módulos removíveis e documentar posição final de cada equipamento.
- **Aceite:** transição de demonstração com fonte principal desligada deixa quatro postes solares operando; nenhum vazamento alcança eletrônica.

## Etapa 5 — Integração, qualidade e apresentação

- [ ] Testar cenários de falha, duplicidade MQTT, reboot e reconexão.
- [ ] Revisar proteção com responsável de elétrica; registrar valores medidos.
- [ ] Capturar fotos, datasheets, logs, vídeos, histórico e documentação final.
- [ ] Ensaiar roteiro da apresentação, inclusive interrupção de Wi-Fi.
- **Aceite:** demonstração repetível em sequência de 5–10 minutos e operação local segura.

# 18. Plano de testes e critérios de aceite

| Teste | Procedimento | Critério de sucesso | Evidência |
|---|---|---|---|
| T-01 | Iniciar dois ESP32 com rede disponível | Status ONLINE por dispositivo, firmware identificado | Log + painel |
| T-02 | Desconectar Wi-Fi | Semáforo continua seguro; bomba respeita limite | Vídeo + log serial |
| T-03 | Alterar luz do BH1750 | Dois postes principais seguem histerese | Telemetria e vídeo |
| T-04 | Comando manual da iluminação | ACK correlacionado, estado observado | Histórico de comando |
| T-05 | Pressionar botão de travessia | Sequência visual e sonora sem conflito | Vídeo com tempo |
| T-06 | Simular solo seco no modo declarado | Regra dispara um pulso permitido | Print com origem de dado |
| T-07 | Baixar nível da água | Bomba bloqueada, alerta criado | Foto boia + painel |
| T-08 | Comando bomba acima do máximo | Rejeitado ou limitado conforme contrato | Teste automatizado |
| T-09 | Reentregar mesmo MQTT QoS1 | Pulso não é duplicado | Log de commandId |
| T-10 | Atrasar ACK | Backend mostra timeout sem afirmar execução | Log + painel |
| T-11 | Desligar fonte principal | Solar continua aceso e não retroalimenta quadro | Vídeo + medição |
| T-12 | Desligar solar/USB do gerenciador | Poste solar opera pela bateria por período-alvo | Vídeo + duração |
| T-13 | Ligar bomba por ciclo curto | Sem transbordo, sem umidade sob quadro | Teste de estanqueidade |
| T-14 | Reiniciar ESP32-02 durante pulso | Bomba inicia/desliga em estado seguro | Vídeo + serial |
| T-15 | Diferenciar dado simulado de físico | Front sempre sinaliza origem | Captura tela |
| T-16 | Comparar INA219 com multímetro | Registrar diferença, faixa e limitação | Planilha de medições |

## Testes de software automatizados

- **Unidade (.NET):** validadores de comando, estados de comando, limites da bomba, regra de histerese, qualidade de leitura, política de autorização e deduplicação.
- **Integração (.NET):** PostgreSQL real/testcontainers quando possível, persistência MQTT→DB, API→outbox/MQTT, ACK→estado e reconexão; migrações.
- **Frontend:** testes de services/components, erros API, reconexão SignalR, estados de comando; E2E Playwright para fluxo realístico simulado.
- **Firmware:** testes em host para máquinas de estado, tempo máximo, debouncing e eventos; hardware-in-loop para GPIO real. Não declarar teste de hardware como feito até haver medição.

# 19. Decisões de compra e instrumentos de bancada

**Comprar apenas o que ainda não existe.** Inventariar primeiro Arduino/kit, sinaleiros, cancela, LEDs, resistores, conectores, cabos e a plaquinha elétrica mencionada. Cotar os itens críticos que faltam: dois ESP32 D1 R32, sensor BH1750, boia, INA219, bomba, sensor de umidade (se houver amostra real), drivers prontos, fonte externa certificada, proteção CC/borneiras, painel solar, gerenciador solar, bateria apropriada, mangueiras e recipiente estanque.

**Ferramentas para ensaiar:** multímetro, alicate/decapador, crimpador adequado, fonte CC limitada em corrente se disponível, copo graduado para vazão, suporte de ensaio e recipiente impermeável. Jumpers/protoboard servem para **prototipagem de sinais**; não são solução final de distribuição de corrente de motor.

**Registro de validação por peça:** modelo exato / foto frente-verso / fornecedor / datasheet / tensão admissível / corrente máxima e de partida / interface de controle / tipo e bitola de conector / compatibilidade 3,3 V / proteção embutida / condição de solda / ensaio realizado / responsável / status. Não adjudicar que “tudo está comprado” apenas porque foi cotado.

# 20. Matriz de responsabilidade e interface com a equipe

| Responsável | Frente | Entrega |
|---|---|---|
| Douglas — software | Arquitetura, firmware ESP32, .NET, Angular, banco, MQTT, CI, testes e integração | Repositório executável, contratos, releases e evidências |
| Responsável técnico de elétrica / pai | Revisão dos circuitos CC, proteção, cargas e segurança | Memorial de dimensionamento e aprovação do esquema |
| Equipe de montagem local | Base, fiação, tubulação, postes, testes de água | Maquete, fotos e medições reais |
| Integrante INT-01 (nome a definir) | Energia, iluminação e solar | Pesquisa, datasheets, circuitos e testes |
| Integrante INT-02 (nome a definir) | Solo e sensor | Método de medição, calibração e documentação de origem |
| Integrante INT-03 (nome a definir) | Hidráulica, bomba e reservatório | Fluxo, vazão, contenção, proteção e cotações |
| Integrante INT-04 (nome a definir) | Acessibilidade e sinalização | Sequência semafórica, buzzer, botão e testes |
| Integrante INT-05 (nome a definir) | Estrutura e arquitetura da maquete | Planta cotada final, base, inspeção e manutenção |

Todos os relatórios devem diferenciar pesquisa, simulação e ensaio presencial. Fotos e medições dos montadores precisam manter atribuição e data. Requisitos da faculdade e rubrica oficial continuam pendentes de validação.

# 21. Riscos, bloqueios e decisões em aberto

| Código | Risco/decisão | Medida de contenção | Bloqueia |
|---|---|---|---|
| B-01 | Arduino existente pode não ser ESP32 | Fotografar placa, conferir chip e USB | Compra de placas/cabos |
| B-02 | “Plaquinha de energia” sem identificação | Fotografar frente/verso e datasheet | Compra do controlador solar |
| B-03 | Motor da cancela desconhecido | Identificar tipo, driver e fim de curso | Controle real da cancela |
| B-04 | Sinaleiros já podem incluir resistores/drivers | Testar tensão, corrente e sinal | Pinagem de mobilidade |
| B-05 | Corrente de partida da AM174 desconhecida | Teste CC com limitação e multímetro | Fusível, fonte e MOSFET |
| B-06 | Quatro postes solares e 14500 | Medir LEDs, autonomia e carga real | Fechamento solar |
| B-07 | Layout elétrico sem protótipo | Aprovar posição, bitola, proteção e conectores | Fiação definitiva |
| B-08 | Horta artificial versus umidade | Escolher cartucho real OU simulação rotulada | Critério de sensor na avaliação |
| B-09 | Rubrica acadêmica não anexada | Confirmar necessidade de pH/EC e entregas | Escopo final |
| B-10 | Sinalização requer mais GPIOs | Revisar mapa e considerar expansor MCP23017 | Pinagem final |

**Gates para iniciar desenvolvimento:** contratos MQTT, modelos e estados podem ser escritos já. **Gates para energizar:** modelo das peças identificado, datasheets, fonte correta, polaridade, resistência/limitação de LED, proteção do motor, isolamento de água e revisão elétrica. **Gates para declarar concluído:** testes reais evidenciados e documentação atualizada com números medidos.

# 22. Prompts de implementação para Codex/assistente de código

> Use como contexto o arquivo `Cidade_Viva_Especificacao_Tecnica_Desenvolvimento_v1.0.md`. Construa incrementalmente um monorepo `cidade-viva` conforme seção 13, em Angular + TypeScript, ASP.NET Core/C# modular monolith e PostgreSQL. A integração física é via broker MQTT local com dois dispositivos `esp32-energy` e `esp32-water`. Implemente contratos versionados da seção 10, simulador MQTT distinguindo telemetria real/simulada, estados auditáveis de comandos e ACK, regras locais espelhadas no simulador e frontend em tempo real com SignalR. Priorize segurança: comandos idempotentes, expirados e restritos por ação; não energize saídas físicas no firmware inicial. Comece apenas pela **Etapa 0**, com árvore, dependências, documentação, CI, compose e testes de contrato. Antes de qualquer código GPIO ou arquivo pinout definitivo, solicite o modelo físico/documentação dos ESP32 e drivers e registre `PENDING_HARDWARE_VALIDATION`. Ao concluir cada etapa, rode testes, explique riscos e forneça checklist de homologação. **Não invente resultados de teste nem dimensões de componentes.**

## Checklist de PR

- [ ] Contratos ou migrations alterados acompanhados de exemplos/testes.
- [ ] Testes unitários de regras críticos executados.
- [ ] Nenhum segredo no repositório; MQTT não aberto publicamente.
- [ ] Comandos não declaram execução antes do ACK.
- [ ] Telemetria simulada não é apresentada como física.
- [ ] Timeout/reinício mantêm bomba em OFF.
- [ ] Interfaces de hardware desacopladas para simulação.
- [ ] Documentos e ADRs atualizados se houver mudança técnica.

# 23. Referências técnicas e documentos anteriores

**Fontes do projeto existentes:**

- `Cidade Viva — Documento Mestre, Sirene e Caderno Técnico` (Google Drive, visão v0.6): <https://docs.google.com/document/d/1dmzWpek3WRTsF_x4_bM7DVJKISk7QD5nbfvcbEsW7Yc/edit>
- `Cidade_Viva_Caderno_Tecnico_Maquete_v1.pdf` (18 pranchas, estudo preliminar; layout antigo): <https://drive.google.com/file/d/1LtVVgmS2i6k_287rUmcHgY9g-zHG6XN6/view>
- `Cidade_Viva_Memorial_Alimentacao_e_Lista_Compras_v0.1.pdf` (7 páginas; pré-dimensionamento): <https://drive.google.com/file/d/1pLkuwjZN2QlacFy9AoYJ7yVA9oAoWnL9/view>
- `Cidade_Viva_Orcamento_Base_Valores_2026-11-04.xlsx` (estimativas, não comprovante de compra): <https://docs.google.com/spreadsheets/d/1-_qH7HWt5Cq8LCcuDfRlj9WOBxbwGDcu/edit>

**Documentação técnica de fabricantes e ferramentas:**

- PlatformIO — WEMOS D1 R32: <https://docs.platformio.org/en/stable/boards/espressif32/wemos_d1_uno32.html>
- Espressif — ADC2 e Wi-Fi, cuidados com pinos: <https://docs.espressif.com/projects/esp-faq/en/latest/software-framework/peripherals/adc.html>
- Waveshare — Solar Power Manager (convencional): <https://www.waveshare.com/wiki/Solar_Power_Manager>
- ASP.NET Core, documentação oficial: <https://learn.microsoft.com/aspnet/core/>
- Angular, documentação oficial: <https://angular.dev/>
- PostgreSQL, documentação oficial: <https://www.postgresql.org/docs/>
- MQTT 5.0, especificação OASIS: <https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html>

---

**Controle documental**

| Versão | Data | Natureza | Revisão seguinte necessária |
|---|---|---|---|
| 1.0 | 06/11/2026 | Primeira consolidação de desenvolvimento com o escopo recente | Incluir datasheets das peças compradas, GPIO final, diagrama elétrico validado, dados de teste e rubrica acadêmica |

**STATUS: APTO PARA INICIAR DESENVOLVIMENTO DE SOFTWARE E SIMULADORES. NÃO APTO PARA CONEXÕES ELÉTRICAS DEFINITIVAS SEM VALIDAÇÃO EM BANCADA.**
