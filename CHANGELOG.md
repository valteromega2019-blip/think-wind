## Ajuste após a prévia 0.18 — ativação da busca pela tecla Windows

- Acrescentada tentativa de ativação quando SetForegroundWindow direto é recusado: somente na solicitação explícita de foco, a thread de interface conecta temporariamente sua fila à thread em primeiro plano, solicita ativação e desconecta em finally antes de qualquer operação WPF.
- Nenhum processamento de texto, IO, espera ou layout ocorre durante a conexão. Mantido o foco no HWND e no TextBox após a tentativa; não há captura global de letras nem alteração permanente de foco.
- Hipótese tratada: falta de direito de ativação após abertura por hook, diferente de clique no menu. Ainda requer confirmação em uso. Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário.
## Ajuste após a prévia 0.18 — métricas na disposição original

- Removidos os blocos destacados de temperatura/uso no topo. Esses valores voltam às linhas originais de GPU/CPU, junto das demais métricas.
- Temperatura e uso passam a 14 DIP, apenas um pouco maiores que os demais valores de 13 DIP e com o mesmo tamanho entre si. Mantida a temperatura antes do uso dentro da linha, sem alterar a posição dos grupos.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. O instalador 0.18 já gerado não inclui este ajuste posterior.
## 0.18 Alfa — empacotamento local

Consolida as correções desta branch e os ajustes do overlay. Validação final: 47 testes aprovados, 0 falhos e 0 ignorados. Instalador local com PawnIO oficial opcional; sem publicação remota nesta etapa. [Notas](RELEASE-0.18-ALFA.md).

## Em desenvolvimento — foco nativo na pesquisa

- A solicitação de foco da pesquisa também aplica SetFocus ao HWND do menu, que pertence à própria thread de interface, antes de selecionar o TextBox no WPF.
- A ativação tardia faz a mesma sincronização. Não são compartilhadas filas de entrada com jogos/outros processos, nem capturados caracteres globalmente.
- A causa exata da falha relatada ainda depende de confirmação em uso. Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário.
## Em desenvolvimento — destaque de temperatura e uso no overlay

- CPU e GPU passam a abrir a lista do overlay, com temperatura primeiro e uso ao lado, ambos com números de 20 DIP em negrito. A temperatura utiliza a cor de destaque.
- Textos gerais do overlay passam a 13 DIP e seminegrito para melhorar legibilidade sobre o Acrylic. Métricas desmarcadas continuam ocultas e os valores destacados não se repetem nas linhas secundárias.
- Os valores principais usam distribuição com quebra para evitar recortes em larguras menores.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Revisão por leitura do código e diff; confirmação visual pendente.
## Em desenvolvimento — Acrylic no overlay de métricas

- O overlay agora utiliza BackdropHost e composição nativa Acrylic, como a ilha, em vez de somente pintar uma superfície semitransparente.
- A camada de material acompanha posição, dimensões, escala e cantos arredondados do overlay. É ocultada junto com ele e liberada no fechamento.
- Preservados os estilos sem ativação e com passagem de cliques, sem injeção no jogo. O estado do material só é atualizado quando tema ou alto contraste mudam.
- Se a composição não estiver disponível, utiliza fundo legível opaco; alto contraste usa superfície do sistema.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Revisão por leitura de código e diff; confirmação visual pendente.
## Em desenvolvimento — foco da pesquisa pelo teclado

- A abertura pela tecla Windows solicita explicitamente o foco da pesquisa depois de alternar o menu, incluindo quando a barra estava oculta.
- O campo recupera o foco após a ativação nativa da janela. A tentativa adiada considera também janela inativa, evitando tratar o foco interno antigo do TextBox como prova de que ele já recebe digitação.
- Busca iniciada pela caixa do Dock preserva o foco original; solicitações de foco são desarmadas ao fechar o menu.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Confirmação interativa pendente.
## Em desenvolvimento — posição personalizada do overlay

- Adicionados sliders de posição horizontal e vertical, de 0% a 100% da área disponível, com indicação decimal e ajuste de 0,1 ponto percentual pelas setas do teclado.
- Mantidos os quatro presets de canto. Selecionar um preset restaura o alinhamento e atualiza os sliders; ajustar um eixo mantém a coordenada do outro e ativa posição personalizada.
- Posição é salva automaticamente e aplicada em tempo real, considerando o tamanho atual do overlay e a escala do monitor, com margens para evitar posicionamento além da tela.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Revisão por leitura de código e diff; confirmação visual pendente.
## Em desenvolvimento — números do calendário sem recorte

- Removida a altura fixa de 224 DIP das grades mensal/anual. As linhas agora medem o conteúdo dos dias e períodos, com altura mínima dos dias e menor preenchimento interno.
- O calendário completo usa ajuste proporcional à largura disponível, evitando apertar números dentro de células menores que o conteúdo.
- A ilha solicita nova medição após carregar o calendário ou mudar o mês.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Revisão por leitura do código e diff; confirmação visual pendente.
## Em desenvolvimento — conteúdo dos blocos expandidos

- Títulos dos módulos, controles de conexão e cabeçalhos de áudio passam a quebrar linha; controles de conexão e grade da bandeja usam alturas mínimas em vez de alturas fixas. Percentuais de áudio podem crescer conforme a fonte.
- Mídia exibe título, artista e player com quebra de linha e solicita nova medição quando os metadados mudam, mesmo que a sessão continue ativa. O tempo também aceita quebra de linha.
- O painel completo invalida sua medição antes de calcular o tamanho natural dos blocos e arredonda a altura necessária para cima antes do ajuste à tela.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Revisão por leitura de código e diff; confirmação visual pendente.
## Em desenvolvimento — ilha secundária opcional

- Dynamic Island somente na tela principal do Windows por padrão. Nova opção Mostrar Dynamic Island nas telas secundárias em Personalizar > Monitores, desmarcada inclusive nos perfis existentes sem essa preferência.
- As barras inferiores continuam nos monitores selecionados. Ilhas secundárias desativadas não são criadas e não reservam a área superior nem oferecem alça fullscreen nessa tela.
- A opção aplica imediatamente e é salva automaticamente. Reativação, troca de tela principal e restauração após Modo Jogo respeitam as ilhas existentes.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Revisão por leitura do código e diff.
## Em desenvolvimento — digitação direta no Iniciar

- A abertura do Iniciar solicita ativação da janela e foco de teclado no campo de busca, repetindo uma única vez após a abertura nativa caso o foco inicial ainda não esteja aplicado.
- Texto digitado enquanto uma aba ou botão do menu estiver com foco é encaminhado à pesquisa, preservando a primeira letra e sem interceptação global de caracteres. Campos de texto, menus de contexto e combinações com Ctrl/Alt/Windows são preservados.
- A solicitação pendente é cancelada ao fechar o painel. Sem compilação, testes ou type check nesta etapa, conforme a preferência do usuário; confirmação interativa pendente.
## Em desenvolvimento — FPS e controles do overlay

- Corrigida a ausência de captura local de FPS: quando o launcher já está elevado, ele usa ETW diretamente, sem depender da tarefa de sensores. A sessão só inicia quando as métricas de frames são solicitadas e é encerrada quando deixam de ser necessárias.
- Os provedores DXGI/D3D9 são habilitados com nível Verbose; o filtro continua aceitando somente eventos Present do processo em primeiro plano. O coletor separado exige a revisão frames-v2, para atualizar sua cópia protegida pelo fluxo normal de autorização.
- O overlay informa quando está aguardando eventos ou quando a sessão ETW reporta erro, sem inventar FPS. Sua renderização é suspensa quando não está visível.
- A personalização do overlay identifica a seleção dos quatro cantos e oferece slider de tamanho de 70% a 150%, com aplicação imediata e salvamento automático.
- Revisão por leitura de código e diff. Não foram executados compilação, testes ou type check nesta etapa. A leitura de FPS ainda precisa de confirmação em um jogo DirectX compatível; outros caminhos gráficos podem não emitir os eventos capturados.
## Em desenvolvimento — Iniciar próprio também durante jogos

- Por solicitação do usuário, a tecla Windows isolada continua direcionada ao Think Wind durante jogos quando WindowsKeyOpensLauncher está habilitado. O atalho opcional do launcher também permanece disponível.
- Mantido o callback mínimo do hook em thread dedicada, encaminhando a abertura para o Dispatcher. Combinações de teclas do Windows preservam o comportamento existente.
- O menu pode abrir temporariamente sobre o jogo sem restaurar toda a interface do launcher; a detecção revalida a janela protegida enquanto o foco estiver no próprio launcher. O Dock temporário é ocultado ao fechar o menu.
- Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Não foram feitas novas medições de latência nem validação em jogos.
## Em desenvolvimento — tecla Windows durante vídeos fullscreen

- A tecla Windows isolada e o atalho opcional do Iniciar continuam habilitados durante apresentações fullscreen identificadas pelo launcher, respeitando a preferência WindowsKeyOpensLauncher. A suspensão dos atalhos durante jogos permanece.
- O Iniciar é aberto na tela principal mesmo quando o Dock está oculto pelo vídeo. Ao fechar o menu, o Dock mostrado temporariamente volta a ficar oculto. Ao sair do fullscreen, o comportamento normal é restaurado.
- Revisão por leitura do diff, sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário. Confirmação interativa pendente.
## Em desenvolvimento — acesso fullscreen e estabilidade do Iniciar

- A alça superior de vídeos em tela cheia acompanha o monitor da janela de apresentação, incluindo monitores secundários habilitados. Ela é recriada após mudanças de configuração dos monitores mesmo quando o modo fullscreen já estava ativo.
- Todas as ilhas podem devolver o acesso à alça após recolher, e seu estado temporário é encerrado ao sair do fullscreen.
- O Iniciar não fecha por uma desativação transitória durante Show/Activate nem pelo simples retorno de foco à janela que o abriu. Clique externo e Esc continuam fechando; mudanças para outra janela são confirmadas em duas amostras do temporizador já existente.
- Revisão somente por leitura e diff. Sem compilação, testes ou type check nesta etapa, conforme a preferência do usuário. Validação interativa pendente.
## Em desenvolvimento — monitor principal e abertura de aplicativos

- Iniciar e pesquisa aparecem somente no monitor identificado como principal pelo Windows; a largura das barras secundárias não reserva espaços para esses controles.
- A família da tela principal é priorizada, e a troca da tela principal invalida a configuração dos monitores mesmo quando suas dimensões não mudam. Os atalhos do Iniciar seguem a barra principal, quando ela está habilitada.
- Removidas, apenas do ambiente do processo do launcher, as variáveis internas `VSCODE_*`, `ELECTRON_RUN_AS_NODE` e `ELECTRON_NO_ASAR`. Elas podiam contaminar a abertura de aplicativos Electron ao iniciar o launcher a partir do IDE. As variáveis persistentes do usuário e do Windows são preservadas.
- Evidência: o ambiente desta sessão continha `ELECTRON_RUN_AS_NODE=1`; a relação com cada falha relatada ainda precisa de confirmação em uso.
- Revisão por leitura de código e diff. Sem compilação, testes ou type check nesta alteração, conforme a preferência do usuário.
## 0.17 Alfa — 0.17.0-alpha

[Baixar instalador](https://github.com/valteromega2019-blip/think-wind/releases/download/v0.17.0-alpha/ThinkWind-Setup-0.17.0-alpha.exe) · [Notas e limitações](RELEASE-0.17-ALFA.md)

Personalização em abas, fontes até 250%, expansão de mídia/áudio sem rolagem, monitoramento e overlay, múltiplos monitores e PawnIO opcional incluído. Sem novos testes ou type check separado nesta publicação.

## Em desenvolvimento — tela cheia, reserva superior e Dock no desktop

- Toggle de overlay passa a pílula de 128 × 34 DIP com identificação interna Overlay · ON/OFF, mantendo estados de cor e acessibilidade.
- Clique em aplicativo ativo do Dock preserva o foreground capturado antes do foco do botão, escolhe a janela ativa do grupo e solicita minimização, verificando o resultado com espera assíncrona limitada. Menus e miniaturas continuam restaurando/ativando. Auxiliar elevado também admite somente a ação adicional minimizar, com validação existente de janela/processo.
- Diagnóstico passivo: PawnIO encontrado em execução; tarefa de sensores instalada, porém LastTaskResult=1. Alteração de IPC para saída somente leitura (v3), retirando impersonação e ajuste de rótulo de integridade. Cliente valida identidade/caminho elevado e servidor valida token/sessão do cliente sem impersonação. Modo fixo sensores/frames é argumento da tarefa, com whitelist estrita; não há comandos de controle pelo pipe. Atualização protegida requer UAC novamente, solicitada na próxima abertura se configuração automática estiver habilitada.
- Falhas do worker passam a registrar exceção em sensor-worker-error.log na pasta protegida. Esta revisão ainda não foi compilada nem executada; causa específica do resultado 1 anterior não foi confirmada e leituras de CPU precisam de confirmação prática. Sem testes ou type check.

- Toggle de overlay circular de 38 DIP, ON com destaque colorido e OFF neutro, sincronizado entre painéis e editor. Tooltip explicita que habilitado não significa visível fora de jogos; mantém suporte a teclado e estado acessível. Sem compilação, testes ou type check nesta etapa.

- Ícone de monitoramento disponível no cabeçalho principal da ilha central, inclusive compacta/hover; abre diretamente a segunda área e, no painel completo, alterna com a principal. No modo barra permanece somente o acesso esquerdo. Sem compilação, testes ou type check nesta etapa.

- Modo barra mantém a área de monitoramento apenas no painel esquerdo: seletor e indicadores removidos do painel direito, que volta à área principal ao trocar de modo. Modo ilha preserva a troca entre as duas áreas.
- Grade do painel completo volta a usar a composição compacta de 760 DIP antes da redução proporcional. Janela passa a acompanhar a largura real do conteúdo após limitar a altura, eliminando sobras laterais produzidas por dupla redução/reflow. Sem compilação, testes ou type check; conferência visual pendente.

- Inicialização solicita configuração do coletor quando não houver autorização válida, inclusive após tentativa anterior malsucedida; uma tentativa por abertura, sem repetir na mesma sessão após cancelamento. Revogação explícita em Personalizar desativa essa configuração automática. UAC inicial continua obrigatório; tarefa previamente autorizada executa sem nova pergunta.
- Removidos texto de estado, avisos técnicos e botão Autorizar sensores abaixo dos blocos da ilha. Mantidos apenas toggle compacto de Overlay e acesso à personalização; autorização/diagnóstico permanecem em Personalizar. Sem compilação, testes ou type check nesta etapa.

- Design do monitoramento unificado à ilha: cards transparentes, texto e destaque usam os recursos do launcher, opacidade interna considera a transparência configurada e cantos seguem o arredondamento da ilha. Botões com template arredondado, realce/foco/pressionado/desativado e seleção dos controles do gráfico.
- Personalização do overlay reduzida para 620 × 640 DIP (limitada ao monitor), com aparência, métricas por grupo e ajuda recolhíveis. Editor usa o material da ilha; modo sincronizado também acompanha fundo, transparência e cantos, preservando valores personalizados para modo independente. Campos de cor ficam ocultos quando sincronizados e controles herdados são desabilitados. Sem compilação, testes ou type check nesta etapa.

- Overlay limitado a jogo reconhecido em primeiro plano (catálogo de instalação ou lista explícita, respeitando exceções), independente de vídeos/fullscreen genérico. Botão e Ctrl+Shift+O habilitam a exibição, sem forçar overlay no desktop. Detecção separada da proteção de tela cheia e do estado ativado do Modo Jogo.
- Configuração do coletor: tarefa ausente na primeira instalação passa a ser tratada por enumeração, evitando variações de tradução COM/FileNotFoundException de GetTask. Falhas exibem etapa, mensagem e HRESULT no configurador elevado; conta diferente/elevação ausente têm mensagens específicas, sem atribuir toda falha à conta administrativa. Causa real da captura anterior não confirmada porque a exceção era descartada. Sem compilação, testes ou type check; nenhuma nova tarefa foi instalada nesta etapa.

- Portados do código fornecido do Think Monitor: editor de aparência do overlay (cores, transparência por superfície, cantos e prévia) e catálogo dos 26 itens. Acrescentados seleção por grupo, presets, quatro posições, três escalas, modo manual/automático e atalho Ctrl+Shift+O via RegisterHotKey configurável, sem hook global.
- Overlay renderiza blocos GPU/VRAM/CPU/RAM e FPS, frametime, 1% low e gráfico quando disponíveis. Captura ETW adaptada do monitor, só no auxiliar elevado e mediante overlay ativo com métricas de quadros selecionadas; janela de 10 s, até 4096 eventos do processo em primeiro plano. São eventos Present DXGI/D3D9, não medição garantida de quadros exibidos nem garantia para toda API gráfica/anti-cheat. PerfCap ainda indisponível; demais sensores extras dependem do hardware.
- Blocos de resumo e componentes redesenhados no padrão visual fornecido: cards neutros com linha fina, títulos pequenos, valores grandes, histórico real CPU/GPU de 1/5/15 min e anéis de RAM/VRAM/disco. Sem importação de benchmark, resultados de testes, eventos ou biblioteca de jogos.
- Protocolo do coletor passa a v2, com handshake limitado a sensores ou sensores+ETW. Coletor protegido anterior precisa ser atualizado pelo botão Autorizar/atualizar sensores (UAC); a interface não substitui silenciosamente o payload elevado. Dados não disponíveis aparecem como —.
- Sem compilação, restore, testes ou type check nesta etapa. Reprodução visual, FPS/ETW, permissões e métricas precisam de confirmação prática.

- Iniciar com Windows habilitado por padrão, aplicado uma vez na primeira execução e restaurado pelo instalador conforme a preferência salva. Desativação em Personalizar é preservada e não é revertida a cada abertura. Coletor tem trigger de logon próprio após autorização UAC inicial, sem nova pergunta nas execuções da tarefa. Sem aprovação automática de UAC e sem elevar a interface. Sem compilação, testes, type check ou mudanças reais no Registro/tarefas nesta etapa.

- Sensores avançados: coletor isolado no modo --sensor-worker, instalado por configuração UAC única em Program Files com ACL de leitura para usuários. Tarefa agendada por usuário (InteractiveToken/Highest), ação fixa e registro protegido; inicia no logon ou sob demanda sem solicitar UAC novamente. Não armazena senha nem desativa UAC. A configuração recusa credenciais de outro usuário e caminhos com reparse points.
- IPC local com ACL por SID, verificação de processo/sessão, caminho do servidor protegido e token elevado. Protocolo envia somente amostras JSON limitadas a 64 KiB; sem comandos de escrita/controle. Sem cliente, o auxiliar encerra após 20 s sem abrir sensores; ao desconectar encerra a sessão e libera bibliotecas. GPU/CPU avançadas compartilham um único coletor; falha mantém fallback básico.
- Primeira execução normal tenta configurar uma vez; cancelamento não repete automaticamente. Botões em Personalizar permitem autorizar/atualizar ou revogar a tarefa. Atualizações do payload protegido exigem nova autorização, e não usam diretamente executável de diretório gravável pelo usuário. Drivers não são instalados automaticamente; todos os sensores dependem de suporte real do hardware/PawnIO.
- Sem compilação, testes, type check, instalação de tarefa ou alterações de permissões neste ambiente. Implementação revisada apenas por leitura; UAC inicial e confirmação prática ainda pendentes.

- Monitoramento independente nas duas novas áreas: blocos CPU/GPU/RAM/disco, nomes, uso, temperatura/clock/potência e VRAM quando publicados pelo sensor. RAM, carga total CPU e espaço livre vêm das APIs do Windows; sensores adicionais usam LibreHardwareMonitorLib 0.9.6. Nenhum arquivo/runtime/IPC do Think Monitor é utilizado.
- Coletor compartilhado a cada 2 segundos fora do Dispatcher, somente com painel visível ou overlay explicitamente ativado. Ao perder todos os consumidores, fecha os sensores. Não habilita controladores, placa-mãe/SMBus, rede nem sensores DIMM. CPU de baixo nível só é habilitada com processo elevado; sem instalação de drivers ou elevação automática. Valores ausentes são —.
- Overlay próprio, desligado por padrão, com toggle nos painéis e Personalizar; sem captura de cliques, teclado, injeção, benchmark ou FPS simulado. Opt-in permanece visível em jogos, mantendo a coleta reduzida apenas quando solicitado. Fullscreen exclusivo/hardware protegido pode impedir a exibição do overlay de desktop.
- Não foram executados compilação, restore de pacotes, testes nem type check. Integração de sensores, disponibilidade de temperatura e desenho dos painéis ainda precisam de confirmação prática após compilação solicitada.

- Margens dos painéis padronizadas: 12 DIP nas superfícies expandidas, painel lateral a 12 DIP da lateral (considerando DPI) e Área 2 sem margem superior extra. Grade usa a largura interna disponível em vez de mínimo virtual de 760 DIP; altura considera o padding real para evitar sobras assimétricas. Sem compilação, testes ou type check nesta etapa.

- Área experimental: abas textuais substituídas por ícone de monitoramento no cabeçalho e dois indicadores clicáveis. Contêiner da área principal é ocultado junto ao conteúdo para não deixar espaço residual. Painel esquerdo verifica saída do cursor e recolhe pelo atraso configurado, preservando captura do mouse e passagem pelo topo. Sem compilação, testes ou type check nesta etapa.

- Estrutura experimental vazia: seletor Área principal/Área 2 no painel completo da ilha e botão + à esquerda da barra superior abrindo painel independente abaixo do topo. Superfícies seguem o tema/Acrylic existente, com fechamento próprio e limites de tamanho. Sem compilação, testes ou type check nesta etapa.

- Foco recebido pela própria ilha não encerra mais o modo de apresentação: a detecção consulta novamente a janela original do vídeo, conferindo PID, visibilidade, minimização e desktop virtual. Alt+Tab para outro aplicativo ou saída real de fullscreen continuam encerrando a proteção. Sem compilação, testes ou type check nesta etapa.

- Expandir durante a revelação em fullscreen mantém a ilha centralizada e presa ao topo, sem faixa de status separada, deslocamento lateral ou passagem por uma janela de 6 DIP. Recolhimento continua retornando à alça. Sem compilação, testes ou type check nesta etapa.

- Alça em tela cheia revela a própria ilha no estágio hover (mídia/áudio), sem substituir por atalhos de Configurações. Saída do grupo recolhe conforme o atraso configurado e retorna à alça; controles e mini-painéis preservam a interação. Revelação não redimensiona o aplicativo fullscreen. Sem compilação, testes ou type check nesta etapa.

- Tela cheia sem moldura também é detectada quando mantém IsZoomed. Apresentação/vídeo protegido oculta as barras normais e oferece alça superior de 96 × 6 DIP, com relógio e atalhos nativos ao passar o mouse, recolhendo após sair. Jogos reconhecidos pelo catálogo/lista não recebem a alça. Sem compilação, testes ou type check; confirmação visual pendente.

- Proteção de tela cheia independente das exceções de navegadores e da ativação do Modo Jogo; oculta a interface e suspende os serviços já gerenciados pelo modo protegido. Troca do estado protegido precede o ajuste da ilha para não redimensionar o vídeo durante a entrada. Detecção atual usa aplicativo em primeiro plano sem estado maximizado e cobertura do monitor; outros modos de vídeo precisam de confirmação.
- Reserva superior AppBar de 28 DIP fixa e configurável (ativada por padrão), independente de hover, modo central, visibilidade e maximização. Usa a área útil nativa em lugar do ajuste individual enquanto ativa. Pode reorganizar o desktop ao ativar/desativar; aplicativos que ignoram a área útil ainda podem exigir tratamento específico. Jogos e vídeos em fullscreen usam limites do monitor.
- Opção de manter Dock visível sem janela ocupando sua região, desativada por padrão. Reutiliza a leitura de janelas a cada dois segundos, sem polling adicional. Preferências salvas e aplicadas em tempo real.
- Sem compilação, testes ou type check nesta etapa. Resultado interativo e ciclos de tela cheia/reserva ainda não validados.

## Em desenvolvimento — controle de janelas elevadas

- Ativação e fechamento recusados para aplicativos elevados podem solicitar auxiliar pontual com UAC. Aceita somente ativar ou WM_CLOSE, validando HWND, PID, início do processo e sessão; encerra após a ação, antes do mutex e inicialização do launcher.
- Launcher permanece asInvoker, sem servidor elevado persistente e sem TerminateProcess. Cancelamento do UAC respeitado. Sem compilação, testes ou type check; comportamento ainda pendente de confirmação.

## 0.16 Alfa — revisão 2 (0.16.2-alpha)

- Publicação com melhorias de ativação, ícones, alça inferior, teclado virtual opcional e reconhecimento do enquadramento compatível ao restaurar. Veja RELEASE-0.16.2-ALFA.md para escopo e limitações.
- Publish Release e instalador concluídos. Sem testes automatizados ou type check separado; últimas correções de margem/restauração aguardam confirmação visual.

## Em desenvolvimento — enquadramento e ativação de aplicativos

- Minimizar não apaga mais o registro de enquadramento compatível: a janela não conta como maximizada enquanto minimizada, mas volta a ser reconhecida ao restaurar. Retenção também se aplica quando a ilha retorna ao modo central por ausência de aplicativos visíveis; a margem pode ser medida novamente após restaurar. PID é conferido para evitar reaproveitar handles de outro processo. Dispose continua limpando os registros. Sem compilação, testes ou type check nesta etapa.

- Atalho opcional Teclado virtual na barra principal da ilha e barra superior de status, usando osk.exe. Oculto por padrão em perfis novos e anteriores; ativação pelo controle existente em Personalizar, com salvamento e atualização imediata. Sem compilação, testes ou type check nesta etapa.

- Reconhecimento lógico de maximização compatível aceita margens limitadas por DPI e aguarda 750 ms pela normalização assíncrona, evitando perder o modo superior por igualdade estrita de retângulos. Ajustes posteriores preservam largura/base efetivamente aceitas pelo aplicativo.
- Área cliente só aciona compatibilidade quando o deslocamento ultrapassa a tolerância de uma borda comum (16 DIP ou metade da barra); pequenas diferenças não devem normalizar Configurações. Alteração ainda pendente de confirmação visual; sem compilação, testes ou type check.

- Abertura elevada: menu Executar como administrador em atalhos/programas compatíveis na barra e biblioteca/Iniciar, usando o verbo runas com UAC. Erro 740 tenta elevação somente para o aplicativo solicitado; cancelar UAC não gera erro adicional. Launcher mantém manifesto asInvoker para preservar Drag & Drop. Isto não concede controle irrestrito sobre janelas já elevadas. Sem compilação, testes ou type check nesta etapa.

- Ícones do Dock: prioriza imagem do catálogo e identidade AppsFolder para aplicativos empacotados; Configurações também é reconhecido pelo executável. Imagens HICON totalmente transparentes não são consideradas sucesso nem entram no cache. Acrescentadas fontes ICON_SMALL2 e ícone pequeno da classe, preservando os handles emprestados. Engrenagem nativa serve como alternativa final para Configurações.
- Sem compilação, testes ou type check nesta revisão de ícones; resultado visual pendente.

- Nova revisão de enquadramento: margem visível DWM é conferida após o ajuste e corrigida no máximo duas vezes, em vez de depender exclusivamente da margem inicial. Negociação alternativa agora exige área cliente acima do próprio HWND, evitando acioná-la por uma borda invisível comum.
- Para área cliente ainda inválida após negociação, incluído fallback de janela normal dimensionada como maximizada, sem ativação (SW_SHOWNOACTIVATE), conservando o reconhecimento lógico pelo launcher. Limitação: nesse fallback, a janela deixa de estar maximizada nativamente; ao desativar o ajuste, restaura-se o retângulo original sem forçar maximização/foco. Requer conferência visual no Illustrator; não é correção validada.
- Diagnóstico passivo: Illustrator HWND top=28, cliente top=-1, maximizado; Configurações HWND/cliente top=8, não maximizado. Sem compilação, testes ou type check nesta etapa.

- Dock permanece aberto enquanto o cursor estiver na área da alça de ativação, inclusive após expandir. Essa região renova o tempo de interação e impede o recolhimento; o atraso configurado começa ao sair dela e do Dock. Sem compilação, testes ou type check nesta etapa.

- Alça inferior recolhida mantém 96 × 6 DIP e a posição anterior, com realce claro e contorno discreto para fundos escuros. Área de aproximação passa a 176 × 32 DIP, detectada pelo temporizador existente sem janela invisível bloqueando cliques. Sem compilação, testes ou type check nesta etapa.

- Illustrator: leitura da janela em execução mostrou HWND no topo 28 e área cliente ainda no topo -1. Incluída tentativa limitada de negociação normal do redimensionamento quando ClientToScreen identifica conteúdo acima da barra após o primeiro ajuste. Resultado visual ainda não confirmado; sem compilação ou testes nesta etapa.

- Abertura pela barra e menu de contexto usa o mesmo fluxo: restauração assíncrona com tentativas limitadas de ativação, reaproveitamento do atalho quando a janela anterior já fechou e prevenção de chamadas simultâneas. Recusa de ativação não cria outra instância silenciosamente.
- Execução de programas expande variáveis de ambiente, aceita caminhos entre aspas e usa a pasta do executável como diretório de trabalho. Sem compilação, testes ou type check nesta etapa.

- Ajuste superior solicita SWP_FRAMECHANGED para recalcular a área cliente, inclusive na primeira observação de uma janela já posicionada. Margem invisível do DWM é medida e mantida por ajuste para alinhar a borda visível sem acumular deslocamentos.
- Enumeração da barra aceita janelas WS_EX_APPWINDOW com proprietário. Ativação considera o último popup visível e solicita restauração assíncrona sem bloquear a interface; teclado osk.exe usa sua própria ativação pelo shell.
- Sem novos hooks, timers ou mudanças na área útil do desktop. Aplicativos elevados, UIAccess e aplicativos que impõem limites próprios ainda dependem de confirmação prática.
- Alterações revisadas por leitura e diff. Sem compilação, testes ou type check nesta etapa; a versão aberta permanece anterior.

# Histórico do Think Wind

## Em desenvolvimento — limite superior para molduras próprias

- Ajuste posiciona o HWND inteiro abaixo da barra, sem descontar a moldura invisível do DWM que podia deixar controles de títulos próprios sob a faixa.
- Considera janelas que ocupam a área útil sem estado IsZoomed e janelas explícitas WS_EX_APPWINDOW mesmo com proprietário. Mantém a identidade de janelas ajustadas para não alternar a ilha ao centro após o redimensionamento.
- Restauração respeita alteração manual dos limites e libera ajustes ao mudar o monitor da ilha. Não altera a área útil do desktop.
- Relato: Illustrator e outros aplicativos com corte superior. Revisão apenas por código; sem compilação, testes ou type check, e sem confirmação interativa nesses aplicativos nesta etapa. Aplicativos elevados ou que rejeitam limites continuam sujeitos às restrições do Windows.

## 0.16 Alfa — revisão 1 (0.16.1-alpha)

- Publicação das correções de contraste, menu Desinstalar, recolhimento, fixados e ajuste direto da janela maximizada descritas abaixo. Prévia aprovada pelo usuário.
- Sobre e segurança, COPYRIGHT.md e SECURITY.md adicionados; margens do ajuste direto passam a consultar o DWM para evitar acumulação ao reiniciar.
- Publish Release e instalador concluídos; sem novos testes automatizados ou type check separado nesta entrega. As notas Em desenvolvimento abaixo registram a implementação anterior à publicação.


## Em desenvolvimento — negociação do tamanho no navegador

- Ajuste direto de janela maximizada passa a usar SWP_NOSENDCHANGING: evita renegociação por WM_WINDOWPOSCHANGING, mantendo WM_WINDOWPOSCHANGED para atualizar o conteúdo. Continua assíncrono e sem foco/alteração da área útil.
- Relato do usuário: navegador permaneceu sem o ajuste esperado na primeira implementação. Esta mudança é uma tentativa de corrigir a negociação de limites; não foi validada no navegador nesta etapa.
- Sem compilação, testes ou type check. A versão aberta ainda usa o comando anterior.

## Em desenvolvimento — ajuste só da janela maximizada

- Janela maximizada no monitor da ilha é ajustada diretamente abaixo da barra superior, preservando largura, base e estado maximizado, sem alterar a área útil do desktop ou registrar AppBar.
- SetWindowPos assíncrono sem ativação ou alteração de ordem; reaproveita a atualização existente, sem timers/hooks novos. Guarda os limites originais e tenta restaurá-los ao ocultar/desativar a barra ou encerrar normalmente, respeitando movimentação/restauração manual.
- Snap e jogos em tela cheia sem estado maximizado não são redimensionados por esse mecanismo. Aplicativos elevados ou que impõem seus próprios limites podem recusar o ajuste; comportamento ainda precisa de conferência interativa.
- Sem compilação, testes ou type check nesta etapa. A versão aberta ainda não contém essa alteração.

## Em desenvolvimento — área de trabalho estável

- Ilha superior deixa de registrar/desregistrar reserva AppBar ao abrir, maximizar ou alternar aplicativos. A mudança dinâmica da área útil podia reorganizar os ícones do desktop e redimensionar outras janelas.
- Barra de status e painel continuam usando suas próprias posições de overlay, sem alterar a área útil do monitor durante essas transições. Não move nem restaura manualmente ícones já deslocados pelo Windows.
- Sem compilação, testes ou type check nesta etapa; mudança ainda não está na versão aberta.

## Em desenvolvimento — limpeza de fixados removidos

- Atualização existente do Dock verifica os destinos dos fixados da barra e do Iniciar no worker STA. Após ausência persistente por 30 segundos e sem janela correspondente em execução, remove o fixado inválido, sua ordem e a referência recente correspondente, salvando preferências.
- Arquivos são verificados somente em discos locais fixos disponíveis; falta de permissão, destino de rede/removível e ausência de ícone não são tratados como desinstalação. AppsFolder é reconsultado com cache de 15 segundos; falhas de consulta não confirmam remoção.
- Sem novos temporizadores nem exclusão de arquivos dos programas. Sem compilação, testes ou type check nesta etapa.

## Em desenvolvimento — recolhimento ocioso

- Removido bloqueio causado por foco de teclado residual após clique direito. Foco em um botão não mantém o Dock expandido; menus abertos, captura real, pesquisa/painel, arrasto e personalização continuam protegidos.
- Área de interação usa posição real do cursor sobre superfície/ícones, sem contar o espaço transparente de ampliação. Libera referência de menu que não abriu/já fechou e prévia órfã após o intervalo de passagem.
- Sem novos temporizadores. Revisão por leitura; sem compilação, testes ou type check nesta etapa.

## Em desenvolvimento — desinstalação e contraste claro

- Desinstalar… nos menus de aplicativos da gaveta/biblioteca e Dock, além das ações locais da bandeja de segundo plano. Abre o desinstalador registrado com confirmação; Store e identificações ambíguas usam gerenciamento nativo do Windows. Não remove arquivos ou atalhos como substituto da desinstalação.
- Consulta de identidade e registros somente ao solicitar a ação, fora do dispatcher; não adiciona monitoramento permanente. Nenhum aplicativo desinstalado durante esta implementação.
- Tema claro: texto principal/secundário mais escuro, subtítulo de personalização mais opaco e piso de 82% para a tinta clara da superfície Acrylic, preservando transparência e ícones.
- Sem compilação, testes ou type check nesta etapa; implementação ainda não está no aplicativo instalado.

## 0.16 Alfa — 0.16.0-alpha

- Clique direito permite ativação da janela proprietária antes de abrir o menu WPF. Mantém o comportamento sem ativação no hover e clique comum.
- Menu em abertura/aberto protege contra recolhimento, reposicionamento e reconstrução dos botões; alterações de layout aguardam seu fechamento. Prévias são interrompidas antes da abertura e não reaparecem durante a interação.
- Reinicia o intervalo de recolhimento ao fechar o menu, permitindo escolher as opções sem a barra desaparecer imediatamente.
- Correção do menu confirmada pelo usuário na prévia. Publish Release sem erros/avisos e instalador gerado para publicação. Sem nova suíte de testes nem type check separado nesta entrega; os resultados da 0.15.1 são históricos.

## 0.015 Alfa — revisão 1 (0.15.1-alpha)

- Leitura de System.AppUserModel.ID por janela via SHGetPropertyStoreForWindow; o identificador do processo permanece como alternativa. A identidade por janela é usada pelo Shell para agrupamento e não era consultada antes.
- Atalhos .lnk e AppsFolder resolvidos pelo Shell no worker STA já existente, com cache por versão do atalho/destino. Não depende da abertura prévia da biblioteca para conhecer a identidade de um fixado.
- Associação usa identidade do atalho/janela e executável; atualização também considera mudanças de identidade do fixado. Sem comparação aproximada por título ou imagem.
- 44 testes aprovados, zero falhas/ignorados. Compilação/verificação de tipos Release sem erros ou avisos e instalador gerado. Conferência visual final no Edge ainda pendente; consulte VALIDACAO-0.15.1.md.

## 0.015 Alfa — 0.15.0-alpha

- Publicação das alterações detalhadas abaixo: Dock, ilha, tema, fontes, instalação e detecção de jogos.
- Publish Release win-x64 self-contained com avisos tratados como erros e instalador Inno Setup compilados. Sem novos testes ou type check separado nesta entrega. As notas Em desenvolvimento abaixo registram as etapas de implementação anteriores à publicação.


## Em desenvolvimento — tamanho das fontes

- Controle Tamanho das fontes em Personalizar → Aparência, de 80% a 130%, com pré-visualização imediata e salvamento automático.
- Escala aplicada aos textos das janelas do launcher, incluindo elementos criados depois; mantém tamanhos de ícones em fontes de símbolos. Usa tamanhos originais para evitar ampliação acumulada, sem polling ou redimensionar a janela de personalização.
- Sem compilação, testes ou type check nesta etapa; alteração ainda não está na versão aberta.

## Em desenvolvimento — tema do Windows e launcher

- Botão claro/escuro da ilha e seletor da personalização aplicam a preferência aos aplicativos e ao sistema Windows (AppsUseLightTheme/SystemUsesLightTheme em HKCU), com notificação WM_SETTINGCHANGE fora do dispatcher e sem reiniciar Explorer.
- Automático continua acompanhando o Windows sem alterar sua preferência. Escrita apenas em ações explícitas do usuário, nunca ao iniciar/atualizar aparência; falhas de acesso são informadas e alterações parciais têm tentativa de restauração.
- Sem compilação, testes, type check ou execução nesta etapa. A versão aberta ainda não contém essa integração.

## Em desenvolvimento — alinhamento e fixados do Dock

- Ícones, Iniciar e pesquisa centralizados verticalmente no fundo Acrylic; indicadores deslocados para baixo sem puxar o centro visual dos ícones para cima.
- Associação de janelas a fixados compara caminhos normalizados e AppUserModelID, evita aliases repetidos e reavalia alterações de identidade/catálogo. Consulta do executável usa acesso limitado em lugar de leitura de MainModule.
- Sem compilação, testes ou type check nesta etapa; mudanças ainda não estão no aplicativo aberto.

## Em desenvolvimento — barras ao alternar janelas comuns

- Removida a regra que tratava qualquer aplicativo em primeiro plano como jogo. O perfil inspecionado mantinha GameModeProtectAllForeground=true; esse campo legado não participa mais da decisão, inclusive na sondagem inicial.
- Removida a opção Proteção ampla da personalização. Mantidos reconhecimento por instalação, tela cheia, lista complementar e exceções.
- Revisão por leitura; sem compilação, testes ou type check nesta etapa. A versão instalada ainda usa a regra anterior.

## Em desenvolvimento — convivência e estado da ilha

- Think Monitor (processos ThinkExplode/ThinkMonitor) não ativa Modo Jogo, inclusive em perfis existentes com proteção ampla habilitada. Essa opção estava ativa no perfil inspecionado e podia ocultar/restaurar o launcher ao alternar o foco. Isso não demonstra a causa dos crashes internos do Think Monitor registrados pelo Windows.
- Estado maximizado nativo passa a prevalecer sobre divergências temporárias da área útil. A busca de janelas em segundo plano também considera tela cheia sem bordas; uma janela menor à frente não deve retornar a ilha ao centro enquanto houver maximizada/Snap/tela cheia no mesmo monitor.
- Reserva da barra superior evita reenviar ABM_SETPOS para um retângulo negociado idêntico, reduzindo notificações redundantes a outros aplicativos.
- Revisão por código e registros existentes; sem compilação, testes, type check ou novo instalador nesta etapa. Alterações ainda não estão na versão instalada.

## Em desenvolvimento — configuração na instalação

- Instalador executa `ThinkWind.exe --configure-installation` antes de oferecer a abertura do launcher, inclusive na instalação silenciosa.
- Ativa a preferência Usar somente a barra do Think Wind e a ocultação automática nativa via SHAppBarMessage (ABM_GETSTATE/ABM_SETSTATE), preservando demais preferências e sem reiniciar o Explorer.
- Comando executa sem overlays, hooks ou serviços do launcher. Instância aberta ou falha de leitura/configuração é reportada; configurações ilegíveis não são sobrescritas. Se a gravação falhar, desfaz a alteração de ocultação automática realizada pelo comando.
- A ocultação automática é uma preferência do Windows e pode ser desativada nas configurações da barra. Esta etapa altera somente o código; sem compilação, testes, type check, novo instalador ou publicação.

## 0.11 Alfa — 0.11.0-alpha

- Corrigida falha de inicialização com aplicativo maximizado: criar o HWND da ilha antes de atribuir a janela de status como dependente.

- Modo Jogo reconhece executáveis nas instalações Steam (incluindo bibliotecas adicionais), Epic e GOG, além da detecção existente de tela cheia. A lista manual passa a ser complementar e novos perfis não contêm um jogo específico.
- Opção de proteção ampla cobre qualquer aplicativo em primeiro plano fora do Shell e das exceções, para jogos não reconhecidos; também oculta o launcher em aplicativos comuns quando ativada.
- Metadados locais somente para leitura, com cache de um minuto, sem varredura recursiva, novos hooks ou temporizadores. Caminhos de instalação são heurística: software instalado nas mesmas bibliotecas pode ser reconhecido, e jogos portáteis/de outras lojas podem precisar da proteção ampla ou lista complementar.
- Distribuição Release win-x64 independente e instalador gerados para publicação. Sem novos testes nem type check separado nesta entrega, conforme preferência do usuário.

## 0.10 Alfa — 0.10.0-alpha

- Tecla Windows isolada abre o launcher por hook em thread dedicada, desativado durante jogos. Atalho Ctrl+Alt+Espaço opcional.
- Modo Jogo por primeiro plano, tela cheia e lista de jogos/exceções; esconde interfaces e suspende tarefas não essenciais.
- Enumeração de janelas assíncrona e atualizações coalescidas.
- Miniaturas DWM sob demanda, hover configurável e `WM_CLOSE` individual.
- Painel Bluetooth nativo com descoberta Classic/LE, estado, rádio e pareamento; fallback explícito para perfis sem conexão genérica.
- Ícones `.lnk` recuperados do destino/recurso, sem overlay, com invalidação de cache.
- Distribuição e instalador 0.10.0-alpha publicados. Bateria atual: 42 aprovados, 1 falha por timeout na consulta Bluetooth. Comparação histórica curta no Wardogs: usuário sem atraso percebido no Modo Jogo; sem medição física de latência.

## 0.08 Alfa (0.8.0-alpha) — 2026-10-29

- Reserva superior medida, camada de ícones sem recorte e botão Iniciar dimensionado para ícones grandes.
- Superfícies internas translúcidas, com opacidade global configurável em tempo real; texto e alto contraste preservados.
- 37 checks aprovados, publicação Release com avisos tratados como erros e instalador solicitado. Release privada e site público atualizados.

## 0.07 Alfa (0.7.0-alpha) — 2026-10-29

- Microfone na primeira expansão, reutilizando o controle de entrada do painel completo.
- Tecla Windows isolada alterna o Iniciar próprio enquanto o app estiver em execução; combinações encaminhadas ao sistema, sem histórico de teclado.
- Compilação de tipos C#/XAML com avisos tratados como erros: zero erros/avisos. Publicação e instalador solicitados. Sem suíte de testes ou validação interativa do novo hook nesta etapa.

## v0.06 (0.6.0-dev) — 2026-10-29

- Encaixe da ilha expandida: cartões preenchem suas células; calendário/bandeja têm altura alinhada e coluna de blocos menores termina na mesma base, com intervalos uniformes. Sem novo build ou testes.

- Após o build: camada transparente independente para o Dock, fundo Acrylic compacto, grades uniformes de aplicativos sem cartões e recuperação adicional de bandeja por MSAA. Extração de ícones maiores em worker com cache. Revisão somente de código; sem nova compilação, execução, testes ou type check.

- Compilação Release posteriormente autorizada: zero erros e zero avisos. Entrega para teste em `artifacts/preview-v0.06/ThinkWind.exe`, preservando a cópia anterior em execução. Sem testes, type check separado, execução, publish ou instalador.

- Ampliação estilo Dock para fixados/abertos: campo contínuo de distância, mola compartilhada, deslocamento sem sobreposição, reserva fixa contra cortes e personalização instantânea de intensidade, velocidade e vizinhos. Sem compilação ou testes nesta etapa.

- Refinamento posterior: removido o encaminhamento da área de segundo plano à bandeja do Windows; imagens reais com cache/renderização do host e ações de acessibilidade quando disponíveis.
- Opção persistente para ocultar as barras originais em todos os monitores, com auxiliar de restauração ao encerrar/desativar ou detectar saída do launcher. Não encerra Explorer nem bloqueia a tecla Windows. Limites da enumeração de ícones ocultos documentados.

- Bandeja 6 × 4 com paginação, nomes compactos, favoritos persistentes e prioridade por uso local/recência.
- Calendário mensal aberto no painel completo e configuração do Windows no cabeçalho compacto; personalização do launcher somente no painel completo.
- Mini-ilha WLAN com redes, conexão/desconexão, senha segura e rádio conforme suporte/permissões do Windows.
- Saída padrão de áudio por PolicyConfig isolado, confirmação/recuperação e notificações Core Audio para volume, mute e dispositivos.
- Recolhimento protegido durante interação, ativação por aproximação e transições sincronizadas aos quadros do WPF, com cache de recortes/geometria.
- Cores de destaque suaves e adaptadas ao tema, preservando a composição Acrylic existente.
- Símbolo vetorial do Windows no botão Iniciar da barra inferior, preservando o menu próprio.
- Revisão por leitura de código e diff. Testes, type check, compilação, execução e publish não realizados. Sem instalador; executável anterior preservado.

Detalhes e limites em `REFINAMENTOS-0.06.md`.

## v0.05 (0.5.0-dev) — 2026-10-29

- Menu Iniciar próprio acima da barra, com fixados reordenáveis, recentes, todos os aplicativos, informações do usuário e opções reais de energia com confirmação.
- Biblioteca por AppsFolder e atalhos do usuário/máquina, ícones do Shell, categorias/ordem alfabética, adição em um clique e alternativa manual.
- Pesquisa compacta/lupa com espaço reservado na barra e painel próprio; integração Windows Search/ADO, aplicativos, recentes e configurações nativas.
- Bandeja em mini-ilha superior independente, botões transparentes e acesso à bandeja original preservado.
- Recolhimento padrão de 0,8 s configurável; estado recolhido independente na borda física inferior, com distância final de zero pixels.
- Drag & Drop OLE: revelar na borda, manter aberto durante a operação, ativar janela por hover e negociar entrega com receptores nativos, sem simular recebimento nem mover arquivos de origem.
- Compilação Release posteriormente solicitada pelo usuário: zero erros e zero avisos; aplicativo atualizado iniciado. Testes, type check separado e publish não executados. Nenhum instalador gerado.

Detalhes e limitações em `REFINAMENTOS-0.05.md`.

## v0.04 (0.4.0-dev) — 2026-10-29

- Sete módulos na mesma grade: três principais e quatro blocos secundários responsivos, respeitando ordem e visibilidade; removida a paginação dos módulos.
- Mídia e volume disponíveis no hover, reutilizando os mesmos controles e mantendo seleção de dispositivo e reprodução.
- Engrenagem no cabeçalho da ilha; removidos os acessos duplicados de personalização no hover e na barra inferior.
- Recolhimento automático persistente de 0 a 10 segundos e opção de desativar; proteção para sliders, menus, teclado, edição e mini-ilhas associadas.
- Calendário em mini-ilha, áudio e bandeja compactos; seletores reais e controles nativos preservados.
- Redimensionamento com aceleração/desaceleração, duração estável durante a transição e movimento discreto dos cartões sem reiniciar destinos iguais.
- Revisão por leitura de código e diff. Testes, type check, build e publish não executados; nenhum instalador gerado. Executável anterior preservado.

Detalhes em `REFINAMENTOS-0.04.md`.

## v0.03 (0.3.0-dev) — 2026-10-29

- Dimensionamento proporcional e paginação da barra inferior, sem ScrollViewer ou deformação dos ícones.
- Ilha com três estados preservados, seções paginadas e limites de altura/largura; mini-ilhas sem rolagem.
- Cartão de mídia por sessões nativas do Windows, com metadados, capa, transporte e linha do tempo quando disponibilizados pelo player.
- Área de processos substituída por controles acessíveis da bandeja, ícones visíveis e acesso a ações/menus nativos quando expostos pelo Explorer.
- Blocos arredondados, sliders preenchidos na cor de destaque e brilho via DDC/CI em monitores compatíveis.
- Atualização seletiva de preferências e janela de configurações independente, sem expansão automática da ilha ao personalizar.
- Compilação Release posteriormente solicitada pelo usuário: zero erros e zero avisos. Corrigidas referências de UI Automation e removido fallback LegacyIAccessiblePattern incompatível. Testes, type check separado e publish não executados; nenhum instalador gerado.

Detalhes e limites em `REFINAMENTOS-0.03.md`.

## 0.21.0-dev — 2026-10-29

- Conteúdo WPF com transparência por pixel, template sem fundo de tema e máscara alfa; material separado em HWND nativo sem bitmap GDI.
- Acrylic por HostBackdropBrush/Windows Composition, independente do foco da janela, com recorte e posição sincronizados.
- Estágios compacto (relógio/rede/volume), hover com intervalos de entrada/saída e painel completo explícito.
- Mini-ilhas independentes de volume e rede, com fechamento por Esc/perda de foco e proteção contra conflitos de hover/clique.
- Snapshot real da rede, consulta WLAN quando autorizada pelo Windows, processos reais da sessão e acesso à central nativa de notificações.
- Build sem erros/avisos e 29 checks aprovados no escopo autorizado. Transparência confirmada visualmente pelo usuário. Nenhum instalador gerado.

Detalhes em `VALIDACAO-0.21.md`.

## 2.0.0-dev — 2026-10-29

- Identidade visual baseada no Think Monitor: grafite, ciano e recursos dinâmicos para temas claro, escuro e automático.
- Ícones sem fundos; recuperação pelo Shell/janelas; resolução de atalhos; agrupamento de janelas e indicadores de minimização.
- Ilha integrada ao topo, com acessos configuráveis, estados proporcionais, cartões translúcidos e calendário com tema.
- Material, tonalidade, escala, espaçamento, raio e posicionamento independentes por componente; contorno DWM desativado e recorte nativo.
- Editor visual com arraste de ícones, controles e módulos; linhas de inserção/alinhamento, listas acessíveis e restauração individual/completa.
- Pré-visualização instantânea, salvamento automático com debounce e migração das preferências antigas.

Revisão por leitura de código e diff. Sem testes, type check, build, publish ou novos instaladores, conforme preferências do usuário. Consulte `REFINAMENTOS-2.0.md` para limitações e verificação pendente.

## 0.2.0 — 2026-10-29

- Integração com Core Audio para enumerar saídas e entradas ativas, ler e alterar volume e mudo por dispositivo.
- Controles de áudio na Dynamic Island com opção de acompanhar o padrão multimídia do Windows e mensagens de indisponibilidade.
- Recuperação periódica após conexão/desconexão de dispositivos; consultas suspensas com a ilha recolhida, oculta ou o módulo desativado.
- Preferência persistente para exibir o módulo de áudio.
- Rolagem do conteúdo expandido e limite de altura pela área útil do monitor.
- Atualização da versão do aplicativo e do script do instalador.

Primeira distribuição experimental local, gerada após autorização explícita para testes: 18 checks aprovados, compilação Release sem avisos/erros, publish independente e instalador compilado. Corrigidas ambiguidades de tipo, espaçamento inválido, identificação legível no seletor de áudio, contraste local do calendário e tratamento de HRESULT de endpoints inválidos. Detalhes e limites em `TESTES.md`. Ainda sem release publicado no GitHub.

## 0.1.0 — 2026-10-29

- Base independente em WPF: dock, Dynamic Island, atalhos, calendário, preferências, bandeja e script de instalador.
