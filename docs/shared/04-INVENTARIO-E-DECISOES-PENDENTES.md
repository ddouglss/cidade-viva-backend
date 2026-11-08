# Inventário e bloqueios de compra/ligação — revisar antes de energizar

## Possuído segundo relato, sem homologação elétrica
- 1 Arduino (modelo desconhecido), kit Arduino, cabo Arduino↔PC.
- Motor e cancela; dois sinaleiros (quantidade/modelos a confirmar em foto).
- Algumas lâmpadas/LEDs, resistores, conectores transparentes e uma plaquinha de energia não identificada.
- **Não equivalem a dois ESP32**. Não comprar duplicata de LEDs/resistores/botões/jumpers sem contar o kit.

## Planejados ou em orçamento, ainda não confirmados como comprados
- 2 ESP32 D1 R32 Wi-Fi/Bluetooth; BH1750; sensor de boia; sensor capacitivo V1.2; INA219; bomba AM174 3–6 V; fonte regulada externa, proteção DC, bornes, MOSFETs, cabos.
- Solar: painel com especificações compatíveis; controlador de carga (exemplo Waveshare Solar Power Manager convencional); bateria 14500 adequada e protegida.
- Reservatório de 1–2 L, mangueira conforme bocal, bandeja estanque e retorno.

## Bloqueios classificados

| ID | Decisão / prova faltante | Efeito | Dono |
|---|---|---|---|
| BL-01 | Foto do Arduino e placa de energia com inscrições | Evita compra duplicada, define função do módulo | Montagem |
| BL-02 | Revisão física dos ESP32 D1 R32; USB e pinagem confirmados | Libera firmware e ligações | Firmware/elétrica |
| BL-03 | Tensão/corrente real de bomba AM174 (inclusive partida) | Fecha fonte, driver, fios e fusível | Elétrica/hidráulica |
| BL-04 | Identificação dos sinaleiros/servomotor/cancela | Fecha drivers, estados e pinout | Elétrica/mobilidade |
| BL-05 | Modelo e manual do controlador solar, entrada de painel, bateria, conectores | Evita ligação insegura e incompatibilidade | Elétrica |
| BL-06 | Referência/calibração da umidade (substrato real ou simulação declarada) | Fecha automação demonstrável | Hidráulica/software |
| BL-07 | GPIO físicos disponíveis nos headers da placa exata | Libera `PINOUT-APROVADO.md` | Firmware |
| BL-08 | Revisão das medidas da base, escoamento e isolação | Libera corte/perfuração | Montagem |
| BL-09 | Regras acadêmicas de avaliação e integrantes | Evita escopo incompatível com rubrica | Grupo |

## Diodos/capacitores
Não devem ser prescritos indiscriminadamente. Flyback em motor DC simples pode ser necessário conforme driver/carga; em servo e bomba com eletrônica interna, consultar datasheet do fabricante. Capacitores 100 nF, 10/100/470 µF são **reserva de cotação**, não instalação obrigatória; selecionar valor, tensão nominal, polaridade e ponto de conexão após testes. Não presumir que 220 Ω atende a qualquer LED alimentado em 5 V.
