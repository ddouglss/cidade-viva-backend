# Correção de inicialização no Visual Studio

## Cancelamento do dispatcher

O depurador pode parar em `WaitForNextTickAsync` quando o host cancela o serviço, inclusive após uma falha de bind. A instância de smoke usava a mesma porta 5080 da IDE. Liberar a porta e tratar o cancelamento pelo token de encerramento tanto na espera do timer quanto no ciclo de trabalho. Preservar o registro de falhas do ciclo. Verificar compilação e encerramento do dispatcher sem task cancelada.

Validação: 40 testes unitários aprovados, incluindo encerramento após criação do timer. A API compilada iniciou na porta 5080 e retornou HTTP 200 em readiness e Swagger. A instância de validação foi encerrada para liberar a porta para o F5.

Revisão após recorrência no Visual Studio: a linha 113 comprova execução do código atualizado, e a captura confirma o perfil CidadeViva.Api. User Secrets não resolveu esse contexto. Adicionar em Debug uma fonte local do `.env`, localizado apenas em ancestral com `CidadeViva.sln`, pesquisando content root e diretório do assembly. Importar somente chaves hierárquicas `__`, sem exportar variáveis globais. Precedência: configuração padrão, `.env`, User Secrets, ambiente e argumentos. Sem arquivo, manter validação obrigatória; Release não carrega `.env`. Testar descoberta, parsing e ausência de arquivo sem usar segredos reais nos testes.

Evidência desta revisão: `dotnet test tests/CidadeViva.UnitTests --no-restore -o .build/config-validation --verbosity quiet -p:UseAppHost=false` aprovou 39 testes (3 novos). API Debug executada com ambiente Production e APPDATA apontado para perfil vazio, sem importar variáveis do `.env` no processo: autenticação MQTT e readiness HTTP 200. Isso valida o fallback independente de User Secrets. Processo temporário encerrado. Visual Studio ainda exige parar a depuração aberta e recompilar para carregar esta revisão; não foi afirmada validação dentro da IDE.

Problema: F5 inicia a API diretamente, sem o carregamento de `.env` realizado por `Run-Api.ps1`. A validação obrigatória de Database/Mqtt falha corretamente.

Decisão: usar o provedor nativo User Secrets do ASP.NET Core em Development. Registrar somente UserSecretsId no projeto; copiar a configuração local existente para o armazenamento de segredos do usuário, sem colocar valores no Git. Variáveis de ambiente continuam tendo precedência. Produção continua exigindo configuração externa.

Correção complementar: em compilações Debug, carregar User Secrets explicitamente mesmo sem perfil Development. Reaplicar ambiente e argumentos após esse provedor para preservar sua precedência. O bloco é excluído de Release. Isso elimina a dependência do perfil de execução para a depuração local; não coloca segredos no assembly.

Validação complementar executada: build Debug isolado sem avisos/erros; DLL iniciada sem `.env` e com ambiente explicitamente Production, carregando os segredos locais pelo bloco Debug; MQTT autenticado com clientId de validação separado e readiness HTTP 200 na porta 5083. Processo de validação encerrado. 36 testes unitários existentes aprovados com `--no-build`. A execução dentro do Visual Studio não foi validada visualmente: a captura da ferramenta retornou outra janela. O processo de depuração existente manteve as DLLs bloqueadas; é necessário encerrá-lo e recompilar para usar o novo código.

Alinhar launchSettings à URL local 127.0.0.1:5080 usada pelo frontend. Não alterar contratos, persistência ou credenciais. Validar build e inicialização direta sem exportação de variáveis do `.env`.

Validação executada: 15 entradas importadas para User Secrets, sem impressão dos valores; build isolado em `.build/vs-validation` aprovado com zero avisos/erros. Inicialização direta da DLL em Development, sem carregar `.env`, aprovada na porta temporária 5082; sessão MQTT autenticada e health/ready HTTP 200. Instância temporária encerrada após o teste. Build no diretório padrão encontrou DLL bloqueada pela depuração ativa do Visual Studio; parar a depuração antes de recompilar. Nenhum segredo foi adicionado ao Git.
