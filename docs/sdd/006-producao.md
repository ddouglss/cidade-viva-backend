# Produção Cidade Viva

Publicar `cidadevivasmartlab.com` em Coolify com as imagens da main em GHCR. Usar projeto e rede próprios. Nginx serve Angular e encaminha REST e SignalR para a API na rede interna. Neon usa TLS com verificação de nome. Migração e seed usam a conexão direta antes de iniciar a API. MQTT autenticado permanece na rede do Compose. Provisionamento Wi-Fi do laboratório só está disponível em Development.

CI valida cada repositório. Backend e frontend publicam tags main e SHA após testes. CD usa o webhook do recurso Coolify somente após publicação, com credenciais em GitHub Secrets. Primeira inicialização admite 40 segundos de carência no health check da API. Medir a duração observada após disponibilizar o servidor.

## Operação e evidências

- Recurso Compose `infra/compose.production.yaml`, branch `main`, projeto Cidade Viva Smart Lab no Coolify existente. Servidor Netcup `159.195.240.96`. Rota HTTPS configurada apenas para `web:8080`.
- Configuração privada no ambiente de produção do Coolify. `CV_DB_HOST` é o endpoint Neon com pool, `CV_DB_DIRECT_HOST` é o endpoint de migração. `CV_GEMINI_MODEL=gemini-3.1-flash-lite`. Usuários bootstrap viewer/operator e credenciais da aplicação ficam em ambiente privado. Cópia local ignorada em `.env.production`.
- Deploy de 09/11/2026 validado pelo proxy interno. Readiness 200, login de ambos os perfis, dois dispositivos semeados e resposta Gemini com duas fontes. O primeiro deploy exigiu corrigir o restart da migração. O redeploy de `67583bd` terminou em aproximadamente 24 segundos com imagens já disponíveis.
- Cloudflare tem zona para `cidadevivasmartlab.com`. A raiz e `www` apontam para `159.195.240.96`, ambos DNS only durante emissão do certificado. A GoDaddy mostra os nameservers `tadeo.ns.cloudflare.com` e `zoe.ns.cloudflare.com`; validar delegação pública, DNS e HTTPS e então avaliar ativar o proxy Cloudflare com Full (strict). A zona antiga `.com.br` não faz parte deste deploy.
- CI executada com sucesso para os três repositórios. Os dois GHCR foram publicados e baixados no servidor. A URL `COOLIFY_DEPLOY_URL` está nos segredos do frontend e backend. Para ativar CD, autorizar um token Coolify com permissão `deploy`, armazenar em `COOLIFY_DEPLOY_TOKEN` nos dois repositórios e definir `CV_DEPLOY_ENABLED=true`. Após configurar, executar os workflows e confirmar a fila no recurso Cidade Viva. O timeout de 40 segundos é da solicitação de deploy. O tempo total varia com pull e inicialização.

[GitHub Actions no Coolify](https://coolify.io/docs/applications/sources/github/actions) · [Delegação de nameservers](https://developers.cloudflare.com/dns/nameservers/update-nameservers/)
