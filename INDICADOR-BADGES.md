# Indicador de atividade — sinais adicionais

O indicador original dependia exclusivamente de HSHELL_FLASH. Aplicativos que usam contadores/badges sem solicitar o piscar da barra podiam ficar sem a bolinha. Essa causa foi identificada no código; não foi reproduzida a chegada de uma mensagem real do WhatsApp nesta etapa.

A implementação mantém o sinal original e complementa com:

- Contadores numéricos no título para mensageiros identificados pelo executável/AppUserModelID, como WhatsApp, Telegram, Discord, Signal, Tretalk, Slack e Teams. Não interpreta um número no título de qualquer navegador como notificação.
- Rótulos e ItemStatus dos botões da barra nativa, quando o provedor de acessibilidade expõe contagens explícitas de notificações/mensagens não lidas. Reconhecimento em português/inglês e associação pelo nome do aplicativo/janela; inclui atalhos fixados sem janela visível, se o badge exposto puder ser associado.
- A bolinha continua usando a cor de destaque. O sinal de atenção original é limpo ao ativar a janela; badges/contadores permanecem enquanto a origem informa pendência e são limpos na próxima atualização após a contagem desaparecer.

Não lê mensagens, bancos de notificações, arquivos privados nem memória de outros processos. Não abre a barra original nem altera suas preferências. Não pede acesso global às notificações. O acesso oficial a todos os toasts pelo UserNotificationListener exigiria identidade/capacidade de pacote e consentimento explícito do usuário; isso não foi acrescentado silenciosamente.

Desempenho: usa o catálogo de janelas já consultado pelo Dock. O provedor de badges é consultado fora do Dispatcher no máximo a cada oito segundos, com cache e apenas uma consulta em voo compartilhada entre monitores. A travessia tem limites de nós/profundidade/tempo cooperativos; um provedor travado não lança novos workers continuamente. A validade do cache é de vinte segundos. Não inicia novas consultas enquanto o Dock está suspenso pelo Modo Jogo. O limite de tempo não interrompe à força uma chamada de acessibilidade travada.

**Limite:** não garante identificar toda mensagem do WhatsApp. Se a versão do aplicativo ou o Explorer não expuser contador/badge e não emitir HSHELL_FLASH, não há sinal confirmado nesta implementação. Não se fabrica uma bolinha a partir de qualquer mudança de título. É necessária conferência em uso real com mensagens recebidas; o intervalo de badges pode causar alguns segundos de atraso.

Arquivos: Services/TaskbarBadges.cs, Views/DockWindow.Attention.cs e Views/DockWindow.cs.

Revisão por leitura e diff. Compilação, testes e type check não executados nesta solicitação.

Referências: [RegisterShellHookWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registershellhookwindow), [Notification listener e consentimento](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener).
