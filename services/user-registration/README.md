# Cadastro opcional do Orby

Serviço independente do Orby desktop e dos outros aplicativos. Não é login
autenticado: não verifica se a pessoa controla o e-mail. A contagem é aproximada,
apenas dos participantes, com deduplicação pelo e-mail normalizado.

## Dados mínimos

- O nome e o e-mail ficam no perfil local `registration.json`, separado das
  preferências e dos backups automáticos do Orby.
- O e-mail é enviado por HTTPS na inscrição. O Worker calcula HMAC-SHA256 com
  chave secreta do servidor e não grava o endereço nem o nome.
- O banco guarda identificador aleatório de instalação, identificador derivado do
  e-mail, hash do recibo de exclusão, criação, última atividade e versão.
- O cliente informa atividade ao abrir, no máximo uma vez a cada 24 horas. Não há
  monitoramento contínuo nem coleta de aplicativos, hardware ou localização.
- O identificador derivado é pseudônimo, não uma garantia de anonimato. Tokens e
  chaves nunca devem ser enviados a logs, repositórios ou ao instalador.
- O limite antiabuso utiliza IP recebido pela plataforma, derivado com HMAC apenas
  como chave temporária do rate limiter. O banco não armazena IPs. A plataforma
  de hospedagem recebe os metadados técnicos normais das requisições.
- Excluir em Personalizar → Cadastro remove esta instalação e para os envios.
  Havendo outras instalações do participante, elas permanecem até serem excluídas.

## Ativação (não executada nesta implementação)

1. Autorizar a conta Cloudflare a usar um Worker e um banco D1 próprios.
2. Criar o banco `orby-users` e preencher `database_id` em `wrangler.jsonc`.
3. Aplicar a migração em `migrations/0001_registrations.sql` nesse banco novo.
4. Definir os secrets `EMAIL_HASH_KEY` e `ADMIN_TOKEN`, aleatórios e independentes
   (ao menos 32 caracteres). Não usar uma chave de outro projeto.
5. Publicar o Worker e configurar o endereço HTTPS em
   `src/ThinkWind/Services/UserRegistration.cs`, `ServiceUrl`.
6. O administrador consulta `GET /admin/stats` com Authorization Bearer contendo
   o `ADMIN_TOKEN`. Essa chave nunca entra no cliente desktop. O endpoint retorna
   participantes distintos, instalações e participantes ativos nos últimos 30 dias.

O endpoint permanece vazio no cliente até essa ativação. Não é criado um cadastro
falso/local nem exibida uma confirmação de envio quando o servidor não respondeu.
A opção Pular continua disponível mesmo sem conexão.

Referências: https://developers.cloudflare.com/d1/get-started/ e
https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/.

## Estado

Implementação inicial revisada por leitura. Não foram executados build, testes,
type check, provisionamento de recursos ou publicação desse serviço. Não foram
enviados cadastros reais. Ainda depende de ativação online e validação autorizada.
