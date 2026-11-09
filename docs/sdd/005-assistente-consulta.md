# Assistente Cidade Viva — primeiro recorte v1.2

AI-001/002/003/005/006. Viewer e operator consultam o Gemini sobre o último snapshot e alertas ativos. GET /api/v1/ai/availability indica configuração. POST /api/v1/ai/chat recebe pergunta e setor opcional e retorna texto, instante da consulta e fontes selecionadas no servidor. Uma pergunta por requisição. O histórico visual é local à tela.

O operador do ambiente habilita o recurso após decidir quais dados do laboratório pode compartilhar com o Google. Configuração `Gemini__Enabled=true`, `Gemini__Model` e `Gemini__ApiKey` no backend. Pergunta, estado observado, origem, qualidade e causas de alerta são o contexto enviado. Excluir credenciais, identidades, auditoria e comandos. O modelo pode sugerir hipóteses, indicando evidência e forma de validação. O fluxo não oferece ferramentas ao modelo nem persiste propostas.

Limite de seis solicitações por minuto por usuário, pergunta até mil caracteres, timeout de vinte segundos e resposta textual limitada. Em falha de configuração ou provedor, a operação da cidade continua disponível. O modelo é escolhido na configuração da conta.

Para habilitar, edite o `.env` local do backend, preencha `Gemini__Model` com o identificador disponível na conta, `Gemini__ApiKey` com a chave e defina `Gemini__Enabled=true`. Reinicie a API. O Debug do Visual Studio carrega o arquivo local. Em outros ambientes, forneça as mesmas chaves por configuração do servidor. A interface verifica a disponibilidade no dashboard.

[Referência da API Gemini](https://ai.google.dev/api/generate-content) · [Configuração de chaves](https://ai.google.dev/gemini-api/docs/api-key)

Validação local em 09/11/2026. Sem autenticação, disponibilidade retornou 401. Com viewer, disponibilidade retornou false e consulta válida retornou 503 com a integração desativada. Pergunta inválida retornou 400. O teste com transporte simulado verificou a chave no cabeçalho, filtragem do contexto e retorno de fontes pelo backend. 41 testes unitários aprovados. Uma chamada ao Google depende de chave e modelo configurados na conta.
