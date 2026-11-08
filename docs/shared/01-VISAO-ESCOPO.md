# Visão e escopo — Cidade Viva

## Problema e resultado
Demonstrar em maquete de cidade inteligente a integração de recursos urbanos, iluminação, mobilidade acessível e irrigação, medidos e controlados por software. É protótipo acadêmico, não equipamento homologado para infraestrutura pública.

### P0 — demonstração mínima
- Dois ESP32 conectados via Wi-Fi/MQTT e dashboard com estado **online/offline**.
- Dois postes principais com LEDs acionados manualmente e por luminosidade; quatro solares alimentados por painel+bateria em circuito independente.
- Semáforos com máquina de estados sem verdes conflitantes; botão de travessia e buzzer se componentes confirmados.
- Circuito de água funcional: reservatório → bomba → mangueira → horta artificial impermeável → retorno. Boia bloqueia bomba sem água. Modo AUTO por amostra real de substrato **ou** simulação rotulada.
- Backend recebe telemetria, registra comandos, ACK e histórico; frontend exibe origem dos dados, erros e estados reais.
- O firmware mantém proteção e automações essenciais mesmo sem API/broker.

### P1 / condicionais
- INA219 no ramo instrumentado; cancela (dependente de modelo do motor); alertas sonoro/visual; calibração do sensor de umidade; telemetria solar **somente com medição instalada**.

### Fora do MVP
- IA executando ações físicas; medição de pH/condutividade sem metodologia; aplicativo móvel; carga simultânea de toda a maquete pelo painel; 127/220 V dentro do MDF; simulações não identificadas como físicas.

## Fonte de verdade e conflitos
Esta SDD descreve a configuração recente. O documento antigo de outubro menciona terra real e solar opcional: **substituir pela horta artificial com circulação real e solar isolado funcional**. Quantidades de sensores e GPIOs não passam de intenção de compra até inventário.

## Condições de não aceite
- Apresentar um ACK sem evidência de acionamento de GPIO como comprovação de água circulando.
- Ligar motor ou bomba diretamente ao GPIO de 3,3 V.
- Alimentar motor/ESP32 por protoboard como barramento final.
- Fazer o painel declarar solar carregando por mera suposição de irradiância.
