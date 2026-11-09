# Produção Cidade Viva

Publicar `cidadevivasmartlab.com.br` em Coolify com as imagens da main em GHCR. Usar projeto e rede próprios. Nginx serve Angular e encaminha REST e SignalR para a API na rede interna. Neon usa TLS com verificação de nome. Migração e seed usam a conexão direta antes de iniciar a API. MQTT autenticado permanece na rede do Compose. Provisionamento Wi-Fi do laboratório só está disponível em Development.

CI valida cada repositório. Backend e frontend publicam tags main e SHA após testes. CD usa o webhook do recurso Coolify somente após publicação, com credenciais em GitHub Secrets. Primeira inicialização admite 40 segundos de carência no health check da API. Medir a duração observada após disponibilizar o servidor.
