# Regras de integração e fronteiras

## Interfaces
- Plataforma digital **não controla pinos**: publica comandos versionados e recebe fatos.
- O ESP32 faz intertravamento físico e decide último estado seguro; nunca confiar exclusivamente em validação remota.
- Dashboard nunca muda estado atuador antes de ACK/telemetria; mostrar `Aguardando` enquanto sem confirmação.
- Sistema solar é físico, isolado dos 5 V principais; ESP32-01 não comanda os quatro postes solares no MVP, a menos que se adicione driver/alimentação adequada sem unir fontes.
- Boia do reservatório é sinal de segurança; entrada desconectada/indeterminada bloqueia bomba.
- Simulação é categoria explícita, inclusive quando o sensor não estiver montado; usar indicador visível na tela.

## Matriz comando → dono → proteções

| Ação | Dispositivo | Validação local | Observabilidade |
|---|---|---|---|
| `SET_LIGHT_MAIN` | `esp32-energy` | Modo e condição do driver/ramal | GPIO/estado reportado |
| `SET_LIGHT_MODE` | `esp32-energy` | limiares com histerese | lux, modo, saída |
| `REQUEST_CROSSING` | `esp32-energy` | transição segura; nada de verde conflitante | estado da máquina + ACK |
| `PUMP_PULSE` | `esp32-water` | boia OK, timeout, cooldown, driver | comando GPIO, tempo e estado da boia |
| `SET_IRRIGATION_MODE` | `esp32-water` | sensor válido ou simulação rotulada | modo, origem, estado |

## Políticas de indisponibilidade
- Wi-Fi/broker offline: regras locais continuam; painel indica última leitura, nunca estado atual garantido.
- Queda de energia: bomba OFF no boot; semáforos inicializam em estado seguro definido no firmware.
- Sensor inválido: AUTO de água inibido, sem ler `0` como seca; modo manual só dentro das intertravamentos.
- Solar sem luz: bateria pode sustentar circuito se previamente carregada; não anunciar geração sem medida.

## API ↔ tela
GET snapshot depois de conectar/reconectar SignalR; eventos são otimizações, não fonte única da verdade. Endpoint de escrita requer autenticação, limite de requisições e auditoria.
