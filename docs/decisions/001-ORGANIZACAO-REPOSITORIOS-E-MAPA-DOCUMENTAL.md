# Decisão 001 — Organização em três repositórios, decisões superadas e mapa documental

**Data:** 07/11/2026  
**Estado:** APROVADO (vigente a partir do pacote `03_EXECUCAO/001-PACOTE-ANTIGRAVITY.md`)  
**Origem:** `C:\Users\ddoug\projetos\Cidade_Viva_SDD_v1.1\Cidade_Viva_SDD_v1.1`  
**Destino base:** `C:\Users\ddoug\projetos\cidade-viva` (diretório agregador local, **sem `.git`**)

---

## 1. Organização de repositórios aprovada e divisão de papéis

1. **Papéis de execução e revisão:**
   - **Codex:** analisa, especifica, planeja e revisa.
   - **Antigravity:** executa todas as camadas (`backend`, `frontend` e `firmware`).
2. **Três repositórios públicos independentes** (sem Git submodules, subtrees ou `.git` aninhado na pasta pai):
   - **Backend:** `https://github.com/ddouglss/cidade-viva-backend` (`C:\Users\ddoug\projetos\cidade-viva\cidade-viva-backend`)
   - **Frontend:** `https://github.com/ddouglss/cidade-viva-frontend` (`C:\Users\ddoug\projetos\cidade-viva\cidade-viva-frontend`)
   - **Firmware:** `https://github.com/ddouglss/cidade-viva-firmware` (`C:\Users\ddoug\projetos\cidade-viva\cidade-viva-firmware`)
3. **Preservação da origem:** o pacote original em `C:\Users\ddoug\projetos\Cidade_Viva_SDD_v1.1\Cidade_Viva_SDD_v1.1` e o documento-mestre no Google Drive permanecem inalterados.

---

## 2. Decisões históricas superadas

As seguintes formulações anteriores foram explícita e formalmente superadas, mantendo-se válidos a stack tecnológica, as regras de segurança elétrica/física e os contratos MQTT v1:

| Tema | Formulação anterior (superada) | Decisão vigente |
|---|---|---|
| Nome do repositório backend | `cidade-viva-platform` | Renomeado para `cidade-viva-backend` |
| Topologia de repositórios | Monorepo único (`08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md`, seção 13) ou divisão em 2 repositórios (prompt mestre v1.2) | 3 repositórios independentes (`cidade-viva-backend`, `cidade-viva-frontend`, `cidade-viva-firmware`) |
| Divisão de agentes | Backend atribuído ao Codex no prompt mestre v1.2 | Codex analisa, especifica, planeja e revisa; Antigravity executa todas as camadas |
| Papel de `08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md` | Documento consolidado v1.0 de 06/11/2026 | Preservado em `docs/shared/` como referência histórica; não prevalece sobre o desmembramento SDD v1.1 nem sobre `001-PACOTE-ANTIGRAVITY.md` |

---

## 3. Mapa de distribuição documental (Origem → Destino)

Raiz de origem (`<ORIGEM>`): `C:\Users\ddoug\projetos\Cidade_Viva_SDD_v1.1\Cidade_Viva_SDD_v1.1`  
Raiz de destino (`<DESTINO>`): `C:\Users\ddoug\projetos\cidade-viva`

| Arquivo na `<ORIGEM>` | Destino em `<DESTINO>` | Tratamento na Entrega A |
|---|---|---|
| `README.md` | Absorvido nos `README.md` de cada repo e nesta decisão | Contexto geral e guia SDD incorporados às raízes dos 3 repos; original preservado |
| `AGENTS.md` | `cidade-viva-backend/AGENTS.md`<br>`cidade-viva-frontend/AGENTS.md`<br>`cidade-viva-firmware/AGENTS.md` | Adaptado por repo: adicionada seção de 3 repos e divisão atual de papéis; corrigidos caminhos locais de leitura |
| `00_COMPARTILHADO/01-VISAO-ESCOPO.md` | `{backend,frontend,firmware}/docs/shared/01-VISAO-ESCOPO.md` | Cópia idêntica byte a byte (sincronizada) |
| `00_COMPARTILHADO/02-CONTRATOS-MQTT-V1.md` | `{backend,frontend,firmware}/docs/shared/02-CONTRATOS-MQTT-V1.md` | Cópia idêntica byte a byte (sincronizada) |
| `00_COMPARTILHADO/03-REGRAS-INTEGRACAO.md` | `{backend,frontend,firmware}/docs/shared/03-REGRAS-INTEGRACAO.md` | Cópia idêntica byte a byte (sincronizada) |
| `00_COMPARTILHADO/04-INVENTARIO-E-DECISOES-PENDENTES.md` | `{backend,frontend,firmware}/docs/shared/04-INVENTARIO-E-DECISOES-PENDENTES.md` | Cópia idêntica byte a byte (sincronizada) |
| `00_COMPARTILHADO/05-ADRS.md` | `{backend,frontend,firmware}/docs/shared/05-ADRS.md` | Cópia idêntica byte a byte (sincronizada) |
| `00_COMPARTILHADO/06-PLANO-VALIDACAO-END-TO-END.md` | `{backend,frontend,firmware}/docs/shared/06-PLANO-VALIDACAO-END-TO-END.md` | Cópia idêntica byte a byte (sincronizada) |
| `00_COMPARTILHADO/07-REFERENCIAS.md` | `{backend,frontend,firmware}/docs/shared/07-REFERENCIAS.md` | Cópia idêntica byte a byte (sincronizada) |
| `00_COMPARTILHADO/08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md` | `{backend,frontend,firmware}/docs/shared/08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md` | Cópia idêntica byte a byte (referência histórica v1.0) |
| `01_PLATAFORMA_DIGITAL/README.md` | Absorvido em `cidade-viva-backend/README.md` e `cidade-viva-frontend/README.md` | Contexto de integração HTTP/SignalR e ordem de execução incorporados |
| `01_PLATAFORMA_DIGITAL/backend_dotnet/README.md` | `cidade-viva-backend/README.md` | Expandido com responsabilidade, relação entre os 3 repos, ordem de leitura, estado real e caminhos `docs/sdd/` e `docs/shared/` |
| `01_PLATAFORMA_DIGITAL/backend_dotnet/ARCHITECTURE.md` | `cidade-viva-backend/ARCHITECTURE.md` | Cópia idêntica byte a byte |
| `01_PLATAFORMA_DIGITAL/backend_dotnet/sdd/001-mvp/spec.md` | `cidade-viva-backend/docs/sdd/001-mvp/spec.md` | Cópia idêntica byte a byte |
| `01_PLATAFORMA_DIGITAL/backend_dotnet/sdd/001-mvp/plan.md` | `cidade-viva-backend/docs/sdd/001-mvp/plan.md` | Ajuste apenas no caminho relativo do contrato para `../../shared/02-CONTRATOS-MQTT-V1.md` (`docs/shared/02-CONTRATOS-MQTT-V1.md`) |
| `01_PLATAFORMA_DIGITAL/backend_dotnet/sdd/001-mvp/tasks.md` | `cidade-viva-backend/docs/sdd/001-mvp/tasks.md` | Cópia idêntica byte a byte (`B001`–`B014` permanecem `[ ]`) |
| `01_PLATAFORMA_DIGITAL/frontend_angular/README.md` | `cidade-viva-frontend/README.md` | Expandido com responsabilidade, relação entre os 3 repos, ordem de leitura, estado real e caminhos `docs/sdd/` e `docs/shared/` |
| `01_PLATAFORMA_DIGITAL/frontend_angular/ARCHITECTURE.md` | `cidade-viva-frontend/ARCHITECTURE.md` | Cópia idêntica byte a byte |
| `01_PLATAFORMA_DIGITAL/frontend_angular/sdd/001-mvp/spec.md` | `cidade-viva-frontend/docs/sdd/001-mvp/spec.md` | Cópia idêntica byte a byte |
| `01_PLATAFORMA_DIGITAL/frontend_angular/sdd/001-mvp/plan.md` | `cidade-viva-frontend/docs/sdd/001-mvp/plan.md` | Cópia idêntica byte a byte |
| `01_PLATAFORMA_DIGITAL/frontend_angular/sdd/001-mvp/tasks.md` | `cidade-viva-frontend/docs/sdd/001-mvp/tasks.md` | Cópia idêntica byte a byte (`F001`–`F011` permanecem `[ ]`) |
| `02_FIRMWARE_IOT/README.md` | `cidade-viva-firmware/README.md` | Expandido com responsabilidade, relação entre os 3 repos, ordem de leitura e estado real |
| `02_FIRMWARE_IOT/eletrica_e_solar/SEGURANCA-E-DIMENSIONAMENTO.md` | `cidade-viva-firmware/docs/eletrica_e_solar/SEGURANCA-E-DIMENSIONAMENTO.md` | Cópia idêntica byte a byte |
| `02_FIRMWARE_IOT/esp32_energia/README.md` | `cidade-viva-firmware/apps/esp32-energy/README.md` | Cópia idêntica byte a byte |
| `02_FIRMWARE_IOT/esp32_energia/PINOUT-PENDENTE.md` | `cidade-viva-firmware/apps/esp32-energy/PINOUT-PENDENTE.md` | Cópia idêntica byte a byte |
| `02_FIRMWARE_IOT/esp32_energia/sdd/001-mvp/{spec,plan,tasks}.md` | `cidade-viva-firmware/apps/esp32-energy/sdd/001-mvp/{spec,plan,tasks}.md` | Cópias idênticas byte a byte (`E001`–`E012` permanecem `[ ]`) |
| `02_FIRMWARE_IOT/esp32_hidraulica/README.md` | `cidade-viva-firmware/apps/esp32-water/README.md` | Cópia idêntica byte a byte |
| `02_FIRMWARE_IOT/esp32_hidraulica/PINOUT-PENDENTE.md` | `cidade-viva-firmware/apps/esp32-water/PINOUT-PENDENTE.md` | Cópia idêntica byte a byte |
| `02_FIRMWARE_IOT/esp32_hidraulica/sdd/001-mvp/{spec,plan,tasks}.md` | `cidade-viva-firmware/apps/esp32-water/sdd/001-mvp/{spec,plan,tasks}.md` | Cópias idênticas byte a byte (`W001`–`W010` permanecem `[ ]`) |
| `03_EXECUCAO/001-PACOTE-ANTIGRAVITY.md` | `{backend,frontend,firmware}/docs/execution/001-PACOTE-ANTIGRAVITY.md` | Cópia idêntica byte a byte |

---

## 4. Registro de integridade (SHA-256) dos documentos compartilhados (`docs/shared/`)

Os documentos em `docs/shared/` são cópias sincronizadas de referência de `00_COMPARTILHADO/` e não três contratos independentes. Qualquer alteração futura de contrato exige revisão coordenada e atualização simultânea nos três repositórios antes da integração.

| Arquivo em `docs/shared/` | Origem (`00_COMPARTILHADO/`) | SHA-256 (idêntico na origem e nos 3 repositórios) |
|---|---|---|
| `01-VISAO-ESCOPO.md` | `00_COMPARTILHADO/01-VISAO-ESCOPO.md` | `18B3709DB0C38758E9B936F67A226D6CD7086678A22FAB5029CA18D02C54C322` |
| `02-CONTRATOS-MQTT-V1.md` | `00_COMPARTILHADO/02-CONTRATOS-MQTT-V1.md` | `1FE62B16AC9C120F4FC795A5CA37F47D14DE42AAA005A9E7EB7C2C60A9086004` |
| `03-REGRAS-INTEGRACAO.md` | `00_COMPARTILHADO/03-REGRAS-INTEGRACAO.md` | `EB37F0133E68BAADDCDAFDF84A223292C9BEA7EFC8554779869C27504223D459` |
| `04-INVENTARIO-E-DECISOES-PENDENTES.md` | `00_COMPARTILHADO/04-INVENTARIO-E-DECISOES-PENDENTES.md` | `2B5F044AD7B5663D5F8BA87BEF4C4D7548FB4ACEEB16A97940A9DA14D752D54B` |
| `05-ADRS.md` | `00_COMPARTILHADO/05-ADRS.md` | `2CA41C81C51DCE05A97285E335179677FA4186107D4E04EE08E6F2440F0C64ED` |
| `06-PLANO-VALIDACAO-END-TO-END.md` | `00_COMPARTILHADO/06-PLANO-VALIDACAO-END-TO-END.md` | `16DAC0F84FEBEAF789425093DFE2F0E012E31F123EDE1E548D023042A9A393C7` |
| `07-REFERENCIAS.md` | `00_COMPARTILHADO/07-REFERENCIAS.md` | `5FF2CD1BF57B2C260F35884E1335D1980BB369CE6D55B6069E9671C7016A0DEF` |
| `08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md` | `00_COMPARTILHADO/08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md` | `28CF61BB8375EA59E41BAA1C3543453DA7135C96C91367BC8523EC19EE5E0A44` |

---

## 5. Revisão de privacidade e referências internas antes de futuro push público

Conforme exigido na seção 3 de `001-PACOTE-ANTIGRAVITY.md`, os seguintes itens foram identificados nos documentos copiados para avaliação do responsável antes de qualquer `git push` para repositórios públicos:

1. **Links para Google Drive / Google Docs / Google Sheets do projeto:**
   - `docs/shared/07-REFERENCIAS.md` (linhas 4–7): links diretos para Documento-mestre (`1dmzWpek3WRTsF_x4_bM7DVJKISk7QD5nbfvcbEsW7Yc`), Caderno técnico v1 (`1LtVVgmS2i6k_287rUmcHgY9g-zHG6XN6`), Memorial de alimentação/compras (`1pLkuwjZN2QlacFy9AoYJ7yVA9oAoWnL9`) e Especificação v1.0 (`1ZnS5v7TNlb8XM9r1SozBxz6nOeJ-O_JB`).
   - `docs/shared/08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md` (linhas 42, 88, 134, 580–583): link para pasta de diagramas no Drive (`11OELjUJqA1BLYLc3bCtcDPqhmC8M3UO3`) e planilha de orçamento (`1-_qH7HWt5Cq8LCcuDfRlj9WOBxbwGDcu`).
2. **Menções pessoais / familiares na matriz de responsabilidades:**
   - `docs/shared/08-ESPECIFICACAO-INTEGRADA-REFERENCIA-v1.0.md` (linhas 532–533): menções a `"Douglas — software"` e `"Responsável técnico de elétrica / pai"`.
3. **Caminhos locais de máquina (`C:\Users\ddoug\...`):**
   - `docs/execution/001-PACOTE-ANTIGRAVITY.md` (linhas 27, 54, 188) e este documento (`docs/decisions/001-ORGANIZACAO-REPOSITORIOS-E-MAPA-DOCUMENTAL.md`): contêm caminhos absolutos do ambiente local `C:\Users\ddoug\projetos\...`.
