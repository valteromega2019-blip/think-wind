# Alarmes, monitoramento compacto e integração com o shell

Branch: `feature/alarmes-monitoramento`, criada a partir de `feature/relogio-presets`.

## Relógio → Alarmes

- Adicionar, editar, habilitar/desabilitar e excluir horários com nome, repetição por dia da semana e som do sistema (padrão, aviso, bipe ou sem som).
- Lista paginada de dois alarmes; sem dias marcados, o alarme toca uma vez na próxima ocorrência e desativa.
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

## Personalizar → Widgets → Monitoramento

- Selecionar de zero a três blocos: CPU, GPU, RAM e armazenamento. Padrão: RAM.
- `HardwareSummaryCard` é compartilhado com os blocos do resumo completo. Mantém temperatura/uso ou memória/armazenamento, ícones, cores e progresso do tema.
- O widget adapta sua altura à quantidade escolhida. Não reconstrói os cartões a cada amostra; atualiza seus textos e barras.
- Usa `HardwareTelemetry.Latest` e `Changed`, sem criar outro coletor, consultas ou timers. Registra consumo somente enquanto carregado/visível; desregistra ao descarregar e ocultar. A seleção fica em `CompactMonitoringMetrics` nas configurações.
- Opções anteriores de RAM e bateria seguem disponíveis para os demais indicadores da central; a seleção deste widget é independente.
- Personalização inserida logo abaixo do interruptor do módulo Monitoramento, nos editores Widgets e Editor de layout. Aparece apenas com o módulo habilitado; desativar preserva a seleção para quando reativar. A antiga página separada de Monitoramento compacto foi removida.
- Valores do widget aumentados de 20 para 30 DIP, títulos de 11 para 14, detalhes de 10 para 12 e ícones de 13 para 16. Texto principal pode quebrar em vez de cortar em fontes maiores. A tipografia do painel completo permanece nos tamanhos anteriores; preferências globais de fonte/cor continuam aplicadas.
- Armazenamento recebe uma faixa de seleção acima do bloco, compartilhada entre o resumo completo e o widget compacto. Mostra até quatro unidades por letra (C:, D: etc.) quando houver mais de uma. Usa os botões e as cores do tema, sem rolagem; com uma unidade, a faixa permanece oculta.
- A unidade selecionada é salva em `MonitoringDiskId` e sincroniza valores, progresso e demais informações de armazenamento do monitoramento. Se estiver indisponível, usa a unidade do sistema; a preferência é preservada para quando ela retornar.
- Snapshot de até quatro unidades em `HardwareSample.Disks`, com reutilização da leitura existente da unidade do sistema e atualização das demais capacidades em segundo plano a cada cinco segundos, apenas enquanto o coletor estiver ativo. Sem consultas feitas pelos cartões, timers adicionais ou necessidade de privilégios. Unidades locais fixas/removíveis prontas são incluídas; rede e mídia óptica ficam fora da sondagem.
- Coletor administrativo atualizado fornece o snapshot pela conexão existente; versões anteriores continuam compatíveis e a interface complementa os dados em seu loop de coleta. Não exige reinstalar ou reautorizar os sensores apenas para ler espaço em disco. Lista alterada invalida a medida dos painéis uma vez, evitando recálculo de tamanho a cada amostra.

## Site → Atualizações

- Lista selecionável com 30 releases públicas obtidas da API oficial do GitHub, mantendo títulos, datas e notas históricas.
- Filtros Publicadas e Em desenvolvimento. Próximas funções são marcadas como planos, sem data/versão prometida e sem alterar o download estável atual.
- Dados estáticos em `docs/updates.json`: não depende de token, API em tempo de visita ou biblioteca adicional. Atualizar este arquivo ao publicar futuras versões.
- Descrições inseridas com `textContent`, sem renderizar HTML vindo das notas. Seleção por teclado, região de conteúdo anunciada e links para as notas oficiais. Funciona independentemente do Motion; sem JavaScript há link alternativo ao GitHub.

## Estado de validação

### Refinamento visual do relógio e dos alarmes

- Horários em destaque na cor configurada, ações de edição/exclusão com ícones e nomes longos com reticências/tooltip.
- Editor substitui a lista no mesmo espaço; dias em cápsulas selecionáveis, som identificado por ícone e ações padronizadas.
- Interruptor vetorial inspirado no exemplo líquido: mola amortecida (ω=16, ζ=0,5), alongamento com a velocidade, duas gotas com atraso e compressão nos extremos. Responde somente a alterações reais; não alterna sozinho.
- Usa os recursos existentes `Think.Control`, `Think.AccentSurface`, `Think.Accent` e `Think.Text`; não introduz uma paleta violeta/ciano fixa nem altera opacidade/material da janela. A composição existente de Acrylic permanece.
- Adaptação leve com elipses sobrepostas, sem filtro blur/threshold, glow em bitmap ou efeitos de tela inteira. Os frames seguem `CompositionTarget.Rendering` somente durante a interação e param ao estabilizar, ocultar ou descarregar.
- Transições curtas de opacidade/deslocamento entre abas, respeitando a duração configurada e a opção do Windows de reduzir animações. Clicar na aba já selecionada não reinicia a transição.
- Medição compartilhada de Cronômetro, Temporizador e Alarmes: abas inativas usam `Hidden`, sem alterar o tamanho ao alternar. Lista e editor também participam da mesma medição. Continua sujeito ao ajuste de escala do painel para a tela/DPI/fonte escolhidos.
- Barra do temporizador em cápsula, sem marcador branco, com cor de destaque e atualização animada curta. Presets, contagem e agendamento permanecem os mesmos.
- A janela do relógio usa a altura natural do conteúdo em vez do limite genérico de 520 DIP. Quando a tela/fonte ainda exigir redução, a margem inferior compensa o espaço lateral criado pelo Viewbox. As três abas mantêm sua medição compartilhada; os demais painéis não mudam.
- Ícones dos painéis internos ficam na cor de destaque enquanto seu painel estiver visível, inclusive ao retirar o mouse. Aplica-se às mini-ilhas, ferramentas, monitoramento, expansão, personalização e teclado próprio; a ampliação continua restrita ao hover. Estado reaplicado ao reconstruir as linhas e mudar o tema, sem novo timer nem hooks. Aplicativos externos, como Configurações do Windows ou o teclado nativo, não são monitorados por esta indicação.
- Interruptores padronizados conforme a referência: cápsula compacta sem ON/OFF, círculo com a mesma altura do trilho, posição à esquerda/desligado e à direita/ligado. O círculo usa a cor de destaque escurecida em 28%, como o thumb do volume; o trilho ativo usa a cor de destaque existente e o inativo usa `Think.Control`. Os ícones existentes permanecem dentro do círculo. Seletores de personalização, seleção de módulos, otimizador, alarmes e overlay compartilham o desenho; textos descritivos das opções e acessibilidade são preservados. O overlay passa a usar apenas o ícone e tooltip de estado. Não altera sliders de áudio, opções salvas nem a lógica de seleção.
- Esc recolhe completamente a área expandida e fecha seus painéis associados. A abertura explícita por clique solicita foco para receber a tecla; a abertura automática por hover permanece passiva. Após Esc, o hover fica inibido até o mouse sair da área, evitando reabertura imediata. Tenta devolver o foco ao aplicativo anterior pelo mecanismo existente, sem restaurar/maximizar sua janela. Funciona também a partir da barra de status e fecha Personalizar/personalização do overlay quando essas janelas recebem a tecla. Controles internos têm prioridade para fechar suas próprias listas/popups. Sem atalhos globais, novos hooks ou interceptação de Esc de outros aplicativos.
- Corrigido fechamento ao abrir Personalizar: evento .NET Runtime 1026 registrou `MissingMethodException: No parameterless constructor defined for type OrbyLauncher.Views.LiquidToggle` em `FrameworkElementFactory.CreateDependencyObject`. O construtor sem argumentos era `internal`; passou a `public` para permitir a instanciação por reflexão do template. A classe continua interna. Correção por leitura do registro e do código, sem nova execução ou testes nesta etapa.
- Texto “Fechar” substituído pelo ícone X padrão no cabeçalho compartilhado de calendário, relógio, clima, som, rede, Bluetooth e bandeja. Tooltip e nome acessível continuam descrevendo o fechamento. Personalização, ferramentas e prévias já utilizavam X; ações de encerrar aplicativos selecionados permanecem identificadas para não confundir com fechar o painel.

O refinamento foi revisado por leitura/diff, sem nova compilação, testes, type check ou prévia nesta etapa. A prévia compilada anteriormente não contém estas mudanças visuais.

Revisão por leitura dos arquivos e diffs. **Não foram executados testes, type check, lint, compilação, prévia ou instalação nesta tarefa**, conforme as preferências do usuário e o escopo atual. A integração com Gerenciador de Tarefas elevado, retorno do UAC, jogos, alarmes após suspensão e composição Acrylic ainda precisa de conferência em execução. Nenhuma release/instalação foi substituída; não foi realizado merge nem publicação desta branch ou do site.
