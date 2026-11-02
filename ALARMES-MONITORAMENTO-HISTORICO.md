# Alarmes, monitoramento compacto e integração com o shell

Branch: `feature/alarmes-monitoramento`, criada a partir de `feature/relogio-presets`.

## Relógio → Alarmes

- Adicionar, editar, habilitar/desabilitar e excluir horários com nome, repetição por dia da semana e som do sistema (padrão, aviso, bipe ou sem som).
- Lista paginada de três alarmes; sem dias marcados, o alarme toca uma vez na próxima ocorrência e desativa.
- Um agendador por aplicativo; funciona com o painel fechado. Preferências ficam em `%LOCALAPPDATA%\OrbyLauncher\alarms.json`, com gravação temporária e troca do arquivo.
- Notificação Acrylic não ativa a janela: Adiar por cinco minutos ou Dispensar. Alertas simultâneos são enfileirados. O som repete a cada cinco segundos por até um minuto; a notificação permanece até a ação do usuário.
- Novos avisos ficam enfileirados enquanto o Modo Jogo/tela cheia estiver ativo, para preservar a ocultação e o foco; são apresentados ao retornar. Avisos já abertos seguem a ocultação/restauração existente do Modo Jogo.
- Não desperta nem liga o computador. Após suspensão, recupera ocorrências das últimas 24 horas durante as quais o aplicativo continuou aberto. Não recupera alarmes ocorridos enquanto o aplicativo estava encerrado. Adiamentos são desta sessão.
- A data da última ocorrência evita repetição no mesmo dia após ajuste regressivo do relógio/horário de verão. O temporizador do agendador para quando não há alarmes ativos ou adiamentos.

## Barra do Windows e tecla Windows

- Mantido o processo de recuperação da barra, com restauração quando o processo principal encerra.
- Adicionada cooperação com o estado de ocultação automática usando `SHAppBarMessage` (`ABM_GETSTATE`/`ABM_SETSTATE`). Preserva o estado anterior e não sobrescreve uma alteração externa detectada ao encerrar.
- Adicionado evento `EVENT_SYSTEM_DESKTOPSWITCH` para reaplicar a ocultação ao retornar da autorização do UAC ou de outra área de trabalho.
- Nova tarefa `OrbyLauncher-ShellInput-<SID>` com finalidade fixa, instalada pelo fluxo de autorização existente. Usa cópia protegida em Program Files, mesma conta/sessão, PID e horário de criação, nonce aleatório, ACL da pipe restrita ao usuário e verificação da identidade dos processos conectados.
- O auxiliar usa a implementação existente da tecla Windows isolada e publica a ação para a interface normal. Não aceita executáveis nem mensagens arbitrárias. O hook normal é desativado antes de habilitar o auxiliar; há fallback quando a conexão cai. Os atalhos combinados continuam pelo tratamento existente, sem novos hooks de mouse ou polling de teclado.
- A tarefa contínua é separada da tarefa de operações individuais para não bloquear ativação, minimização e fechamento de janelas administrativas. A revisão da autorização muda de `action-v2` para `action-v3`; pode exigir consentimento administrativo para atualizar a cópia protegida. Revogar remove ambas as tarefas.
- O auxiliar encerra quando a conexão ou o processo pai encerra. A interface continua com integridade normal para preservar Drag & Drop.
- Não encerra Explorer, não altera políticas do UAC, não desenha na área protegida nem bloqueia atalhos de segurança. O Windows controla a exibição durante o consentimento protegido; não há promessa de ocultação nessa tela ou de ausência absoluta de flashes do Explorer.

Referências oficiais: [estado da barra](https://learn.microsoft.com/en-us/windows/win32/shell/abm-setstate), [isolamento de privilégios da interface](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview), [funcionamento do UAC](https://learn.microsoft.com/en-us/windows/security/identity-protection/user-account-control/how-user-account-control-works).

## Personalizar → Desempenho → Monitoramento compacto

- Selecionar de zero a três blocos: CPU, GPU, RAM e armazenamento. Padrão: RAM.
- `HardwareSummaryCard` é compartilhado com os blocos do resumo completo. Mantém temperatura/uso ou memória/armazenamento, ícones, cores e progresso do tema.
- O widget adapta sua altura à quantidade escolhida. Não reconstrói os cartões a cada amostra; atualiza seus textos e barras.
- Usa `HardwareTelemetry.Latest` e `Changed`, sem criar outro coletor, consultas ou timers. Registra consumo somente enquanto carregado/visível; desregistra ao descarregar e ocultar. A seleção fica em `CompactMonitoringMetrics` nas configurações.
- Opções anteriores de RAM e bateria seguem disponíveis para os demais indicadores da central; a seleção deste widget é independente.

## Site → Atualizações

- Lista selecionável com 30 releases públicas obtidas da API oficial do GitHub, mantendo títulos, datas e notas históricas.
- Filtros Publicadas e Em desenvolvimento. Próximas funções são marcadas como planos, sem data/versão prometida e sem alterar o download estável atual.
- Dados estáticos em `docs/updates.json`: não depende de token, API em tempo de visita ou biblioteca adicional. Atualizar este arquivo ao publicar futuras versões.
- Descrições inseridas com `textContent`, sem renderizar HTML vindo das notas. Seleção por teclado, região de conteúdo anunciada e links para as notas oficiais. Funciona independentemente do Motion; sem JavaScript há link alternativo ao GitHub.

## Estado de validação

Revisão por leitura dos arquivos e diffs. **Não foram executados testes, type check, lint, compilação, prévia ou instalação nesta tarefa**, conforme as preferências do usuário e o escopo atual. A integração com Gerenciador de Tarefas elevado, retorno do UAC, jogos, alarmes após suspensão e composição Acrylic ainda precisa de conferência em execução. Nenhuma release/instalação foi substituída; não foi realizado merge nem publicação desta branch ou do site.
