# Interface Swagger local

Solicitação: disponibilizar Swagger como interface de exploração da API. Reutilizar `/openapi/v1.json` gerado pelo ASP.NET Core; adicionar apenas o middleware SwaggerUI, sem segundo gerador de contratos. Rota `/swagger`, título Cidade Viva API e abertura automática no perfil Visual Studio. Interface disponível em Development e compilações Debug; Release fora de Development não habilita UI. Não persistir autorização no navegador. Validar build e respostas HTTP de HTML, assets e OpenAPI.

Validação executada: build aprovado, zero avisos/erros; `/swagger/index.html`, `/swagger/swagger-ui-bundle.js` e `/openapi/v1.json` retornaram HTTP 200 na instância temporária 5083, encerrada após verificação. Pacote SwaggerUI 10.3.1, sem SwaggerGen adicional. Abertura automática configurada em launchSettings; F5 dentro da IDE não foi exercitado nesta rodada.
