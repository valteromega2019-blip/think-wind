# Think Wind / Think Launcher 0.10 Alfa

Data: 29/10/2026. Ordem de implementação: investigação de entradas → remoção do hook → Modo Jogo → comparação com Wardogs → confirmação do usuário → Dock/miniaturas/Bluetooth/ícones → regressão.

## 1. Evidências e hipótese de input lag

**Evidência de código:** a 0.07/0.08 instalava `WH_KEYBOARD_LL` com `SetWindowsHookEx` na thread WPF. Todas as teclas passavam pelo callback, inclusive as de outros processos. A liberação isolada de Win era interceptada e `SendInput` injetava Ctrl/Win para impedir o Iniciar nativo. Essa mesma thread executava enumeração de janelas, leituras COM e processamento visual. Segundo a documentação do Windows, o callback de baixo nível depende da thread instaladora e de sua fila de mensagens.

**Hipótese sustentada pela arquitetura:** uma ocupação dessa thread podia atrasar a passagem de entradas para jogos. Não foi demonstrado que o launcher era a causa exclusiva, nem medido um atraso físico tecla–tela em milissegundos.

**Correção estrutural:** removidos `SetWindowsHookEx`, `WH_KEYBOARD_LL`, monitoramento de todas as teclas e reinjeção Ctrl/Win. A tecla Windows isolada volta ao Windows. Há um atalho opcional **Ctrl+Alt+Espaço**, desligado por padrão, implementado com `RegisterHotKey`/`MOD_NOREPEAT` e desregistrado durante jogos. O botão Iniciar do Dock continua funcionando. Não há hook de mouse, Raw Input ou prioridade de tempo real introduzidos pelo launcher.

`GetAsyncKeyState` restante só é usado para estado de botão/Esc durante uma operação de arrastar arquivos; o timer responsável fica parado no Modo Jogo.

## 2. Modo Jogo

- Ativado por jogo em primeiro plano cadastrado, inclusive em janela. Wardogs está na lista inicial pelo processo `WardogsClient-Win64-Shipping`.
- Há fallback conservador para janelas que cobrem o monitor. Shell e o próprio launcher são excluídos; navegadores comuns, VS Code e interfaces da Steam estão nas exceções iniciais. Não é um classificador infalível de jogos: nomes adicionais e exceções podem ser editados em Personalizar → Sistema, separados por ponto e vírgula.
- Observação assíncrona de mudanças de primeiro plano com `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)`, sem interceptar teclas/mouse. Consulta de recuperação a cada 1,5 s cobre redimensionamentos sem troca de foco. Há uma leitura por vez e um resultado pendente por vez; snapshots de foco obsoletos são descartados.
- Antes de mostrar as janelas na inicialização, verifica se já existe jogo elegível em primeiro plano. Durante o modo, esconde Dock, ilha, personalização, menus próprios e miniaturas. Ao voltar, restaura somente as janelas anteriormente visíveis com `ShowActivated=false`.
- Suspende timers de Dock/ilha/painéis, magnificação, animações, mídia, descoberta Bluetooth e observadores de arquivos do catálogo. Miniaturas DWM são liberadas e o atalho desregistrado. O auxiliar de ocultação da barra nativa é encerrado/restaurado durante jogos, voltando somente quando necessário fora deles.
- Enumeração de janelas transferida para tarefa em segundo plano, com concorrência limitada. Atualizações não essenciais não se acumulam no Dispatcher durante o jogo.
- O processo permanece residente para detectar o retorno ao desktop; runtime, cache e threads ociosas não equivalem a execução contínua. Não se promete consumo nulo nem se encerram processos de terceiros.

## 3. Comparação efetiva no Wardogs

Usuário informou Steam/janela sem bordas. O processo do jogo foi encontrado aberto. Runner: `checks/ThinkWind.Checks/PerformanceComparison.cs`, modo `--performance-comparison`. Nenhuma tecla, clique, movimento ou comando de gameplay foi sintetizado; o runner apenas alternou o primeiro plano para as janelas de teste e consultou contadores.

Uma execução com o Wardogs em primeiro plano em **todas** as amostras produziu:

| Cenário | Amostras de 1 s | CPU média do launcher (% de um núcleo) | Pico (% de um núcleo) | Working set médio |
|---|---:|---:|---:|---:|
| Launcher encerrado | 8 | 0 | 0 | 0 |
| Launcher normal | 12 | 5,90 | 35,43 | 169,41 MB |
| Launcher em Modo Jogo | 12 | 0,128 | 1,537 | 92,87 MB |

Houve 3 s de espera após abrir cada instância. São amostras curtas, em instâncias separadas, com inicialização/JIT/cache influenciando memória e CPU. Não são benchmarks de FPS, nem prova de redução da memória de uma instância já carregada ao entrar no modo. A carga do próprio jogo variou entre cenários; não se pode atribuir essa variação ao launcher.

Outras execuções mostraram CPU normal entre aproximadamente 5,89% e 13,06% de um núcleo e Modo Jogo entre abaixo da resolução de contabilização e 0,13%. Na última repetição, apenas 3/8 amostras da fase “encerrado” permaneceram com o jogo em primeiro plano; essa fase não deve ser usada para comparação quantitativa entre cargas do jogo. A tentativa automatizada de alternar para a janela de controle não comprovou todas as transições de foco.

Nos snapshots do Modo Jogo: **0 janelas do launcher visíveis, 0 miniaturas DWM, 0 watchers Bluetooth, 0 atalhos registrados e nenhum hook de teclado**. As interfaces normais tinham duas janelas visíveis.

**Validação de uso fornecida pelo usuário:** “Não percebi atraso no Modo Jogo”. Na conferência posterior, o usuário esclareceu “NENHUM” ao ser questionado sobre problemas ao entrar/sair do Wardogs. Registrado como confirmação de uso da sessão, sem extrapolar para todos os jogos.

### Limites de medição

WPR instalado. `wpr -status` não mostrava gravação ativa. `wpr -start CPU -filemode` falhou com `0xc5585011`, sem permissão para habilitar o perfil de desempenho. WPA não foi localizado. Não houve ETL, medição de GPU/composição, filas de agendamento nem distribuição de latência física de entrada. A política não foi alterada e não foi iniciada uma gravação residual. Estatísticas locais ficam em `artifacts/performance-v0.10/`, ignorado pelo Git.

## 4. Dock e ícones

Largura continua derivada da soma dos botões, pesquisa, Iniciar, espaçamento e padding; redimensionamento horizontal animado, agrupamento por AppUserModelID/executável e paginação quando o monitor não comporta os grupos. O fundo não cresce verticalmente durante o hover. Camada transparente independente, reserva por altura medida com overshoot e conteúdo sem recorte preservados.

Ícones alinhados com indicadores a 7 DIP da borda inferior do ícone em repouso. Magnificação usa tempo real e apresentação WPF, não um FPS fixo; é suspensa em jogos. Não houve validação física em monitor de 300 Hz, e não se promete renderização a 300 FPS.

Atalhos `.lnk`: resolução do destino, leitura de `IconLocation` e extração do recurso com `ExtractIconEx`, sem pedir sobreposição de atalho. Apps Store usam AppUserModelID/AppsFolder quando disponível. Cache de resolução e de imagens invalidado por timestamp do link/destino; nenhuma alteração de Registro para remover setas globalmente. Teste comparou exatamente os pixels do ícone original e do `.lnk`, depois editou o link e conferiu invalidação.

## 5. Miniaturas e fechamento individual

`WindowPreview` abre após hover configurável (padrão 400 ms), com até quatro janelas por página, título/ícone, restauração e botão X. O Dock permanece expandido durante a interação e há tolerância de 350 ms para atravessar o espaço entre ele e o painel. Nada é criado para atalhos sem janelas abertas.

Cada miniatura utiliza um destino nativo top-level, não layered e sem ativação, separado do conteúdo WPF. APIs: `DwmRegisterThumbnail`, `DwmQueryThumbnailSourceSize`, `DwmUpdateThumbnailProperties`, `DwmUnregisterThumbnail`. O painel ajusta-se proporcionalmente à área disponível e os destinos usam coordenadas físicas obtidas do visual. Os recursos são liberados ao esconder/fechar ou entrar no Modo Jogo.

Seleção usa restauração/ativação nativas. X envia `WM_CLOSE` por `PostMessage`, sem terminar processos; o aplicativo mantém seus prompts de documento não salvo. Há mensagem se o Windows bloquear o pedido. Janelas protegidas, minimizadas sem conteúdo exposto ou provedores que não aceitem DWM podem ter prévia limitada/indisponível; o acesso pelo título continua disponível.

## 6. Bluetooth

Mini-ilha própria com `Windows.Devices.Radios`, `Radio.GetRadiosAsync`, `RequestAccessAsync`/`SetStateAsync`, `DeviceInformation`, watchers de endpoints Classic/LE e `DeviceInformationPairing.PairAsync`/`UnpairAsync`. A seleção AEP segue a amostra oficial da Microsoft. Atualizações por eventos, apenas com painel aberto; ao fechar ou entrar no jogo, para watchers, remove handlers e solicita cancelamento da cerimônia pendente.

Exibe nome, estado pareado/conectado e bateria se o Windows disponibilizar percentual válido. Pareamento usa a cerimônia do Windows, incluindo confirmação/PIN; não aceita dispositivos automaticamente. Remover pareamento exige confirmação. Não há API genérica adotada para conectar/desconectar todo tipo de perfil Bluetooth: nesses casos, o botão abre Configurações Bluetooth do Windows. Não se simula uma conexão bem-sucedida.

Consulta local: **1 adaptador**, **1 endpoint Classic e 4 LE** disponibilizados. O usuário limitou a validação a adaptador/listagem; não houve alternância de rádio, pareamento, remoção de pareamento nem desconexão de periféricos. Cerimônias reais e suporte por dispositivo ainda requerem conferência com hardware de teste escolhido pelo usuário.

## 7. Resultados e arquivos

Regressão: **42 aprovados, 0 falhos, 0 ignorados**. Inclui configuração, estados da ilha, composição/alfa/recorte, áudio, layout, cenários de 5/10/15/20 itens, janelas minimizadas, `WM_CLOSE` individual, liberação DWM, consulta Bluetooth, cache de `.lnk` e política de Modo Jogo. As contagens de itens são matrizes de layout; não equivalem a abrir vinte aplicativos de terceiros simultaneamente.

Build Release com `-warnaserror`: zero erros/avisos. Prévia local em `artifacts/preview-v0.10-alpha/ThinkWind.exe`. Nenhum novo instalador ou release foi gerado nesta etapa.

Componentes principais:

- `App.xaml.cs`, `Native/WindowsStartKey.cs`, `Services/GameModeService.cs`: ciclo de vida, atalho e Modo Jogo.
- `Views/DockWindow*.cs`, `Services/ApplicationCatalog.cs`, `Views/IslandWindow.cs`, `LauncherPanelWindow.cs`: tarefas assíncronas, suspensão, layout e hover.
- `Native/WindowThumbnail.cs`, `Views/WindowPreview.cs`: miniaturas DWM e fechamento normal.
- `Views/BluetoothControl.cs`, `MiniIslandWindow.cs`: Bluetooth e ciclo de descoberta.
- `Native/ShellIcons.cs`: ícones originais e cache.
- `Services/SettingsStore.cs`, `Views/SettingsWindow.cs`: opções persistentes.
- `checks/ThinkWind.Checks/*`: regressão e runner comparativo.

## Referências primárias

- [LowLevelKeyboardProc e dependência da thread instaladora](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
- [DwmRegisterThumbnail](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail)
- [Pareamento nativo de dispositivos](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/pair-devices)
- [Seletores Bluetooth Classic/LE — amostra Microsoft](https://github.com/microsoft/Windows-universal-samples/blob/main/Samples/DeviceEnumerationAndPairing/cppwinrt/DeviceHelpers.cpp)

## Confirmação final da sessão

O usuário esclareceu que não houve problema na transição entre o Wardogs e o desktop. A confirmação é qualitativa e não substitui medição de latência física.

Após a preferência permanente mais recente, não foram executados novos testes, type check ou compilação. Os 42 resultados e builds acima são históricos da sessão; alterações posteriores de ciclo de descoberta Bluetooth, hover e descarte de estados antigos ainda não têm uma nova validação completa confirmada. O teste adicional de ciclo Bluetooth foi incluído no código, mas seu resultado final não está confirmado neste relatório.

## Correções após as capturas da prévia 0.10

O usuário demonstrou que o registro DWM aprovado anteriormente não comprovava conteúdo visual: a prévia estava vazia, havia uma faixa branca e cortes nos ícones. Essa validação visual permanece pendente.

Alterações no código: destino DWM com superfície de redirecionamento (sem WS_EX_NOREDIRECTIONBITMAP), área cliente sem moldura e pintura de base preta, tratamento de falha na atualização DWM; remoção do tooltip redundante sobre a miniatura e tema consistente para os demais tooltips; camada do Dock com altura medida estável e sem recorte de layout, revelação somente quando a janela comporta a linha e magnificação suspensa durante o redimensionamento.

Não foram executados testes, type check ou compilação para estas alterações, conforme a preferência permanente. O executável atualmente aberto ainda corresponde à compilação anterior.

## Tecla Windows — restauração solicitada após a prévia

Adicionada preferência WindowsKeyOpensLauncher, habilitada por padrão. A tecla Windows isolada abre o menu do Think Wind; Ctrl+Alt+Espaço continua opcional. O hook WH_KEYBOARD_LL é instalado em uma thread dedicada com message pump próprio, não no Dispatcher WPF. O callback não enumera janelas nem executa trabalho de interface: encaminha o pedido por mensagem. Para preservar combinações Win+tecla, reproduz somente o modificador e o keydown da combinação via SendInput; restrições UIPI podem impedir combinações dirigidas a aplicações elevadas. Não há polling de teclado.

Ao entrar no Modo Jogo, o hook é descartado e sua thread termina; os pedidos de abertura pendentes são ignorados. Ao sair ou desativar a preferência, o comportamento acompanha a configuração. O diagnóstico agora registra o estado real do hook.

As medições anteriores foram feitas antes desta restauração e não validam o novo caminho. Não foram executados testes, type check ou compilação para essa alteração. A versão em execução permanece anterior.

## Ilha adaptativa sobre aplicativos maximizados

Adicionada a opção IslandReduceOverMaximized, habilitada por padrão em Personalizar. Ao detectar aplicativo maximizado ou cobrindo o monitor da ilha, ela reduz seu estado compacto a uma alça de 96 × 6 DIP junto à borda superior. A aproximação do mouse permite usar os três estágios existentes. Captura de controles, mini-painéis, edição e personalização preservam a interação; após sair, aplica-se o tempo configurado de recolhimento. No desktop ou em janela restaurada, volta ao formato compacto normal. O Modo Jogo mantém prioridade e oculta as interfaces.

A leitura usa GetForegroundWindow, IsZoomed, GetWindowRect e a classe da janela para distinguir desktop; não enumera processos ou janelas. Reutiliza o aviso de primeiro plano e a recuperação de 1,5 s do serviço existente. Janelas do próprio launcher preservam o contexto anterior, e aplicativos em outro monitor não reduzem a ilha deste monitor.

Somente revisão de código nesta etapa: sem testes, type check ou compilação. O executável aberto ainda não contém estas alterações.

## Revisão da prévia e da barra de status superior

Prévia: identificada área vazia com largura desejada zero dentro do ContentPresenter centralizado do estilo IconButton. Cada destino agora recebe largura explícita antes de medir/posicionar o painel. As miniaturas aparecem em uma única linha, com 1–4 itens conforme o espaço e paginação para excedentes. Não foi feita nova validação visual do conteúdo DWM nesta etapa.

Ilha sobre aplicativos maximizados: substituída a alça de 6 DIP por barra de status visível de 28 DIP, ocupando a largura do monitor, com horário à esquerda e rede, volume, notificações e abertura da central à direita. O hover sobre essa faixa não abre automaticamente a central: os botões dão acesso deliberado. Desktop mantém o formato normal. Modo Jogo continua ocultando a ilha.

A faixa reserva temporariamente espaço de trabalho pela API oficial SHAppBarMessage (ABM_NEW/QUERYPOS/SETPOS/REMOVE), para que aplicativos maximizados respeitem a altura. Ao esconder ou encerrar, a reserva é removida. Não há alteração permanente do Registro nem temporizador adicional. O posicionamento junto de outras barras de terceiros na borda superior ainda requer conferência visual.

Não foram executados testes, type check ou compilação para estas alterações. O executável aberto permanece na compilação anterior.

## Transições da biblioteca e gaveta de aplicativos

Adicionadas transições de entrada do painel, troca de abas, paginação por botão/roda e atualização de resultados. Opacidade e TranslateTransform são animados no contêiner, preservando células e dimensões da janela. Trocas de página seguem a direção da navegação; filtros usam deslocamento vertical discreto. Abas recebem indicação visual de seleção com as cores do tema.

A troca de aba limpa a pesquisa sem provocar uma recarga intermediária duplicada. Uma assinatura dos itens visíveis evita reconstruções e reinício de animações quando o resultado permanece igual. Clocks são substituídos em navegação rápida, não acumulados, e removidos ao ocultar o painel; não há novo timer. Respeita AnimationMilliseconds e a preferência ClientAreaAnimation do Windows.

Somente revisão por leitura e diff: não foram executados testes, type check ou compilação nesta etapa.

## Nova estratégia para a bandeja de aplicativos

Problemas encontrados por leitura: itens eram descartados se a captura de imagem falhasse; PrintWindow podia bloquear a leitura de hosts XAML/ocultos; leitores independentes repetiam operações; uma leitura concluída após ocultar o painel ainda atualizava a interface.

Implementado TrayIconCatalog: leitura somente de NotifyIconSettings para recuperar IconSnapshot original e ExecutablePath, com suporte a caminhos Known Folder. Esse detalhe de armazenamento é complementar e pode variar entre versões do Windows. O histórico sozinho nunca é exibido como aplicativo ativo: exige elemento de acessibilidade vivo ou confirmação do GUID por Shell_NotifyIconGetRect. Sem confirmação, o item não é incluído. Ícone do executável original serve como alternativa quando o snapshot não pode ser decodificado.

Removido PrintWindow da leitura da bandeja. Cópia de pixels só é usada como último recurso para um ícone visível e comprovadamente descoberto; caso contrário usa snapshot/cache. Leituras concorrentes compartilham uma tarefa, metadados possuem cache passivo de 30 s e painéis ocultos descartam a atualização visual recebida. Nenhum temporizador adicional foi criado.

Ações de acessibilidade nativas continuam prioritárias. Quando não expostas, a interface informa a limitação e oferece ações explícitas, como abrir o executável identificado e fixar o ícone, sem simular um menu nativo. Não foi implementada leitura de memória do Explorer, injeção em processos nem revelação implícita da bandeja original.

Não foram executados testes, type check ou compilação. Não há confirmação de que todos os ícones ou menus do Windows 11 já funcionem com a nova estratégia; a versão aberta ainda é anterior.

## Abertura inicial da ilha e área de widgets

A preparação de layout passa a ocorrer antes da primeira exibição, inclusive ao sair de uma inicialização em Modo Jogo. O HWND é criado e dimensionado ainda oculto. A política de janela maximizada agora recolhe o painel somente na entrada nesse contexto, e não a cada snapshot periódico: abrir a central deliberadamente não provoca um novo recolhimento por essa mesma verificação.

Criada seção Widgets no painel completo, com linha própria abaixo dos controles. O primeiro widget é Clima: a pessoa digita uma cidade, pesquisa, seleciona entre até cinco resultados e a escolha é salva. Não usa localização automática, IP ou GPS. Não há cidade padrão nem dados fictícios.

Integração assíncrona com APIs de geocodificação e previsão Open-Meteo, temperatura em °C, sensação, mínima/máxima e descrição das condições do modelo. Fonte visível no widget. Atualização a cada 15 minutos apenas enquanto o painel completo estiver visível; em falhas há indicação e nova tentativa após um minuto. Sem temporizador adicional, consultas concorrentes ou requisições durante jogos/ocultação; as pendentes recebem cancelamento. Seleção da cidade e edição impedem recolhimento enquanto em uso.

Revisado por leitura. Não foram executados testes, type check, compilação nem chamadas de teste às APIs. Tanto o layout inicial quanto a integração de clima ainda precisam de validação na versão compilada.

Referências de implementação: https://open-meteo.com/en/docs e https://open-meteo.com/en/docs/geocoding-api.

## Lado do painel completo e uso com monitores secundários

O painel completo da Dynamic Island passa a abrir junto à direita da área útil do monitor configurado, com margem de 8 pixels físicos. Personalizar → Dynamic Island → Lado do painel completo permite Direita ou Esquerda, com aplicação imediata e persistência; restaurar o layout repõe Direita. Compacto, primeira expansão e barra de status mantêm seus comportamentos próprios.

Enquanto o painel completo estiver aberto e o cursor estiver em outro monitor, o recolhimento é suspenso. Reutiliza-se o temporizador existente para observar o retorno; ao retornar fora da ilha, o tempo configurado começa novamente. Esc e o botão de fechar continuam funcionando. A política de entrada em janela maximizada também respeita esse contexto, sem capturar foco ou mover o painel de monitor.

O Modo Jogo continua tendo prioridade para ocultar a interface e suspender tarefas. Não foram executados testes, type check ou compilação nesta alteração; a versão atualmente aberta ainda não inclui esse comportamento.

## Barra superior preservada durante a expansão

Separadas as superfícies em duas janelas: IslandStatusWindow mantém a faixa de 28 DIP no topo; IslandWindow apresenta o painel completo 36 DIP abaixo da borda superior (faixa mais intervalo de 8 DIP), no lado escolhido. Não há grande janela transparente ligando os dois componentes. A reserva de espaço do Shell acompanha o HWND real da faixa, e é transferida/removida ao mudar de estado, ocultar ou encerrar.

O painel completo aberto explicitamente agora só recolhe pelo botão ou Esc, permanecendo estável mesmo ao mover cursor/foco entre monitores. A detecção de primeiro plano deixa de alterar seu contexto enquanto aberto. O recolhimento automático permanece nos estados menores. A barra superior acompanha aparência e Modo Jogo, que continua ocultando ambas as janelas.

Também excluídas janelas comuns com IsZoomed verdadeiro da detecção genérica de jogo por geometria de tela cheia, evitando confundir uma janela maximizada em outro monitor com um jogo. Nomes de jogos configurados explicitamente continuam elegíveis.

Não foram executados testes, type check ou compilação; estas mudanças ainda não estão no executável aberto e precisam de conferência visual após compilar.

## Alinhamento à direita e estabilidade dos cliques

Relógio e controles da faixa superior agora ficam juntos, alinhados à direita. A mesma composição é usada quando a faixa está na janela principal compacta ou preservada acima do painel completo.

Corrigida a condição que retirava a faixa reduzida quando MiniOpen ou uma interação se tornava verdadeira: abrir volume/rede não troca mais o layout sob o cursor. Mini-ilhas acionadas pelos botões da faixa usam as coordenadas físicas do próprio botão como âncora, respeitando limites do monitor, em vez do centro de toda a tela. O lado configurável do painel completo foi preservado.

Não foram executados testes, type check ou compilação nesta etapa. A versão em execução ainda não contém a alteração.

## Correção do deslocamento da barra ao expandir

A captura do usuário mostrou faixa superior afastada e cabeçalho repetindo os acessos. A faixa agora usa permanentemente IslandStatusWindow tanto no compacto maximizado quanto durante o painel completo. O mesmo HWND mantém a reserva AppBar: não há transferência do registro entre a ilha e a faixa ao expandir/recolher.

A janela principal fica sem superfície/material no estado de faixa, enquanto a janela de status pinta o topo. Ao abrir o painel, sua animação começa com a largura final e altura mínima já abaixo da faixa, sem transformar o retângulo da barra superior no retângulo do painel. O cabeçalho completo mantém apenas Personalizar e Fechar, eliminando a segunda linha de relógio/rede/volume.

FloatingWindow também confere o retângulo nativo quando o alvo não muda: se Shell/WPF deslocar uma janela após Show, o cache de destino não impede reposicioná-la no local correto.

Revisão somente por leitura, sem testes, type check ou compilação. A correção visual ainda depende de conferência após compilar; a instância aberta permanece anterior.

## Busca de clima somente na configuração inicial

O widget mostra busca e seleção de cidade apenas quando ainda não existe uma localização válida salva. Depois da primeira seleção, esses controles são ocultados: permanecem nome da cidade, dados do clima, atualização e fonte. Ao reiniciar, a cidade persistida mantém esse estado.

Adicionada seção Clima em Personalizar para pesquisar e trocar a cidade posteriormente. Reutiliza o seletor com modo de configuração, sem iniciar consultas de previsão nesse painel. Alterações são salvas automaticamente e atualizam o widget. Restaurar todas as preferências retorna à configuração inicial.

Não foram executados testes, type check ou compilação. A instância aberta permanece na compilação anterior.

## Bandeja sem GUID e largura horizontal — compilação solicitada

Adicionado suporte aos ícones registrados por HWND + UID, além dos registrados por GUID. LegacyTrayIcons associa o executável do cadastro a janelas nativas (inclusive message-only) e exige confirmação de Shell_NotifyIconGetRect antes de incluir o item. Consulta o caminho do processo com acesso limitado, sem ler memória do Explorer ou modificar o Think Monitor. A presença e os menus do Think Monitor ainda dependem de conferência em execução e das permissões do seu processo.

O próprio Think Wind registra diretamente seu ícone original e suas ações de abrir Personalizar/menu nativo no catálogo local, sem depender da descoberta da sua própria bandeja pelo Explorer.

Dock: removido o bloqueio da atualização por simples hover; novos aplicativos podem atualizar a largura mesmo com o mouse sobre a barra. A largura é calculada pela soma medida das células e margens, e a linha recebe essa largura explicitamente, sem manter o espaço do layout anterior. Ajuste proporcional e paginação existentes permanecem ativos.

Compilação Release solicitada pelo usuário concluída com zero avisos e zero erros: artifacts/preview-v0.10-trayfit/ThinkWind.exe. Não foram executados testes adicionais, type check separado ou gerado instalador. Este build inclui as alterações pendentes da barra superior, seleção de clima e bandeja. A validação visual permanece a fazer.

## Revisão de ícones ausentes, Dock recolhido e recolhimento automático

Bandeja: o ícone local do Think Wind é incluído antes de aguardar qualquer leitura externa. TrayCard deixa de aguardar indefinidamente uma leitura lenta. A acessibilidade roda em uma tarefa independente, com uma operação em voo e espera limitada a 350 ms na composição do resultado; os itens nativos já confirmados permanecem disponíveis. Resultados de acessibilidade antigos expiram após 15 s. A busca HWND + UID agora também ocorre quando o cache possui GUID; quando UID não é informado, limita a consulta a IDs 0–8 e só inclui o item se Shell_NotifyIconGetRect confirmar sua existência. Não houve nova conferência do Think Monitor, portanto sua presença ainda não é declarada garantida.

Dock recolhido: substituída a faixa de largura total por cápsula de 96 × 6 DIP centralizada, cantos com raio de 3 DIP e distância física de 4 pixels da borda inferior. Personalizações de posição/tamanho expandido não deslocam a cápsula.

Dynamic Island: removida a exceção que fixava o estágio completo indefinidamente. MouseLeave da janela independente de status também aciona o temporizador. A passagem pelo intervalo de 8 DIP entre status/painel mantém uma ponte de interação; ao sair do grupo, aplica-se a espera configurada. Edição, personalização e controles em uso continuam protegidos. Respeita a opção de recolhimento automático.

Não foram executados testes, type check ou compilação. As mudanças ainda não estão na instância aberta e precisam de validação visual após compilar.

## Dois modos de expansão: desktop e aplicativo em uso

A expansão no desktop (incluindo aplicativos minimizados) fica centralizada no topo, sem criar a barra de status de largura total. Com aplicativo em uso no monitor da ilha, o painel abre abaixo da barra superior no lado configurado, Direita por padrão. O cabeçalho completo no desktop conserva seus acessos, enquanto no modo lateral evita duplicar os controles da barra de status.

A escolha é baseada na janela em primeiro plano, não em processos em segundo plano. A interação com janelas do próprio launcher conserva o contexto anterior. Janelas de outro monitor não substituem o contexto do monitor da ilha; o modo é preservado durante a expansão para não saltar entre centro e lateral.

Mantida a escolha prévia Direita/Esquerda apenas para o modo sobre aplicativos. Não foram executados testes, type check ou compilação; a versão aberta permanece anterior.

## Microfone sincronizado com o dispositivo padrão do Windows

Causa por leitura: a seleção de entrada apenas mudava o endpoint controlado pelo launcher; somente a saída chamava o adaptador de política padrão. O controle de captura podia continuar vinculado a um endpoint explícito, diferente do padrão do sistema.

Adicionado SetDefaultInput para captura, alterando Console, Multimedia e Communications. Reutiliza o adaptador PolicyConfig existente, isolado por ser uma interface desktop não documentada para essa operação. Confere cada padrão com IMMDeviceEnumerator.GetDefaultAudioEndpoint e tenta restaurar os valores anteriores se a alteração falhar ou não for confirmada.

Seleção, observação de mudanças, volume e mute agora seguem o microfone padrão atual do Windows. O seletor reflete o sistema após mudanças externas; volume é relido após escrita, e falhas ficam visíveis também no bloco compacto. Não abre fluxo de gravação nem altera permissões de privacidade. Aplicativos com um dispositivo explicitamente escolhido podem manter essa escolha própria.

Não foram executados testes, type check, compilação ou alterações de hardware para validação. A instância aberta ainda não contém esta correção.

## Conferência por leitura do contexto em monitores secundários

A revisão confirmou que janelas comuns de outro monitor retornam contexto indeterminado (preservar anterior), e o painel completo mantém sua escolha central/lateral durante a abertura. Encontrada e corrigida exceção: classes da barra de tarefas eram tratadas como desktop, e o desktop do monitor secundário podia limpar o contexto do monitor da ilha. Agora barras de tarefas preservam o contexto, e Progman/WorkerW só indicam desktop quando o cursor está no monitor da ilha.

O recolhimento automático ao sair com o mouse continua ativo conforme a última solicitação e preferências; preservação de modo não significa manter o painel aberto indefinidamente. O Modo Jogo continua tendo prioridade.

Conferência somente por leitura de código, sem testes, type check ou compilação; não representa validação de uso real entre telas. A instância aberta ainda é anterior.

## Cor da barra superior a partir do aplicativo abaixo

IslandStatusWindow agora amostra exatamente um pixel na coordenada física imediatamente abaixo da faixa, em seu centro horizontal. GetDC/GetPixel/ReleaseDC limitados a uma leitura por segundo, usando o ciclo existente da ilha, sem novo temporizador, captura de imagem completa ou armazenamento de tela. Ignora ponto fora do monitor, leituras inválidas e superfícies do próprio processo para evitar realimentação da cor.

A cor amostrada passa a ser a tonalidade da faixa, mantendo o material Acrylic e a opacidade configurada. A cor final composta pode diferir do pixel por essa transparência. Transição de cor limitada a 180 ms, com remoção da animação ao ocultar a faixa. Texto/ícones recebem contraste claro/escuro local à barra; o tema dos outros painéis é preservado. Alto contraste utiliza as cores do sistema.

Personalizar → Dynamic Island contém a opção Barra superior acompanha a cor do aplicativo abaixo, habilitada por padrão. Em Modo Jogo/ocultação não há novas amostras. Sem testes, type check ou compilação nesta etapa; a instância aberta permanece anterior.

## Expansão lateral somente com aplicativo maximizado

Corrigida a condição que tratava qualquer aplicativo em uso como motivo para abrir na lateral. Posicionamento, deslocamento vertical, barra de status e cabeçalho agora usam o mesmo estado de aplicativo maximizado no monitor da ilha. No desktop, com apps minimizados ou janelas restauradas, a expansão fica centralizada. O lado escolhido continua válido somente para o caso maximizado.

Ao clicar no launcher, a leitura pode consultar a última janela externa daquele monitor para verificar se ainda está maximizada, evitando preservar um valor antigo logo após restaurá-la. Isso não enumera janelas nem cria temporizador. Durante o painel já aberto, mantém o contexto para evitar mudanças por foco em outra tela.

Não foram executados testes, type check ou compilação nesta etapa. A versão aberta ainda é anterior.

## Correção da identificação do monitor de uma janela

A checagem anterior considerava qualquer sobreposição do retângulo da janela com o monitor da ilha. Bordas invisíveis de redimensionamento de janelas maximizadas podem atravessar alguns pixels do monitor vizinho, fazendo a janela secundária ser interpretada como pertencente ao principal e limpando o modo lateral.

Substituída a interseção pela identificação do monitor da janela por Screen.FromHandle (monitor com maior área da janela). Janelas de outro monitor preservam o contexto existente. Ausência momentânea de primeiro plano, minimização transitória ou falha de leitura também preservam o estado até um contexto válido, sem centralizar por um resultado indeterminado.

Não foram executados testes, type check ou compilação. A versão aberta ainda é anterior, portanto a correção exige nova compilação para conferência visual.

## Retorno ao centro ao minimizar/restaurar o aplicativo

Removido o congelamento incondicional do contexto enquanto o painel completo estava aberto. Uma mudança real do estado da janela do monitor da ilha agora atualiza cabeçalho, faixa, reserva de espaço e posição sem exigir recolher primeiro o painel.

Ao usar outra tela ou clicar no launcher/barra de tarefas, a leitura consulta o estado atual da última janela do monitor da ilha. Assim, minimizar, restaurar, fechar ou mover essa janela é reconhecido, sem usar a janela da tela secundária para decidir a posição. Desktop daquele monitor limpa o contexto lateral.

Não foram executados testes, type check ou compilação. A versão em execução ainda não contém esta correção.

## Microfone na mini-ilha de áudio da barra superior

Adicionado AudioEndpointControl de captura ao painel aberto pelo ícone de áudio, com seletor de microfone padrão, nível de entrada e mute. Compartilha o AudioService com a saída e reutiliza a sincronização nativa já implementada. Ambos os controles são atualizados pelo temporizador existente; interação com o microfone impede fechar/recolher o grupo durante o ajuste. O Viewbox e os limites atuais de altura continuam adaptando o painel ao monitor.

Não foram executados testes, type check ou compilação. A versão aberta ainda não contém esta alteração.

## Janela menor sobre aplicativo maximizado no mesmo monitor

A leitura anterior usava apenas a janela em primeiro plano ou a última janela selecionada. Agora, quando essa janela não ocupa a tela, verifica se ainda há outra janela maximizada visível no mesmo monitor. Janelas minimizadas, ocultas, de outro desktop virtual (cloaked), ferramentas e janelas do launcher são excluídas.

Uma janela maximizada confirmada é reutilizada por HWND, independentemente de quem tem foco. Só refaz a enumeração leve se essa referência deixar de ser válida; tentativas negativas são limitadas a uma a cada 250 ms, sem novo temporizador, acesso a módulos de processos ou extração de ícones. A varredura usa o ciclo/eventos existentes.

Assim, usar uma janela pequena à frente não muda para o centro enquanto houver aplicativo maximizado atrás. Quando não houver mais janela maximizada visível, o modo central volta a ser elegível. Não foram executados testes, type check ou compilação; a instância aberta ainda é anterior.

## Tela dividida do Windows Snap

A detecção agora inclui o estado arranged da API IsWindowArranged, além de maximização. Janelas encaixadas em metades/quadrantes pelo Windows Snap mantêm o modo de aplicativo expandido no monitor da ilha. O mesmo critério vale para a janela em primeiro plano e a busca de outras janelas visíveis atrás, sem depender de coincidência de tamanho da janela.

Janelas minimizadas, ocultas, de outro desktop virtual ou de outro monitor continuam excluídas. Ao sair do Snap e não haver outra janela maximizada/encaixada visível, volta ao modo central. Não foi acrescentado temporizador; há fallback para maximização se a exportação da API não existir no sistema. Snap de ferramentas externas que não marquem o estado arranged não foi validado.

Referência: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowarranged.

Não foram executados testes, type check ou compilação; a instância aberta ainda é anterior.

## Menu Iniciar fechando logo após a tecla Windows

O monitor de visibilidade fechava o menu quando GetForegroundWindow ainda apontava para o aplicativo anterior, mesmo sem interação de fechamento. Agora a abertura registra a janela anterior e só trata perda de foco como fechamento após aquisição confirmada do menu ou mudança real de aplicativo.

Adicionado OutsideClickObserver com Raw Input de mouse, registrado somente enquanto o menu estiver visível e removido ao ocultar/entrar no Modo Jogo. Observa botões sem hooks, captura de mouse ou supressão de entrada; usa buffer nativo reutilizável e só encaminha trabalho para o Dispatcher em clique fora. Não cria polling adicional nem um monitor residente quando o menu está fechado. GetRawInputData continua sendo processado também em movimento, com retorno imediato e sem alocação por movimento.

Esc, seleção de aplicativo, troca de janela e clique fora continuam fechando o menu. Não foram executados testes, type check ou compilação; a instância aberta ainda não contém esta correção. Não foi validada a concessão de primeiro plano em todos os aplicativos e níveis de privilégio.

## Entrega pública 0.10.0-alpha — 29/10/2026

Solicitação explícita: compilar, executar testes e publicar versão e instalador.

- `dotnet run --project checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release --no-launch-profile`: **42 aprovados, 1 falha, 0 ignorados**, saída 1. Log anexado à release.
- A enumeração Bluetooth `DeviceInformation.FindAllAsync` não concluiu no prazo de 20 segundos. O teste foi movido para contexto assíncrono e recebeu cancelamento para impedir espera indefinida. A falha continua registrada; não é aprovação nem teste ignorado.
- Composição em claro/escuro, layout, capacidade do Dock 5/10/15/20 itens, miniaturas DWM e fechamento individual, preferências, ícones e leitura/escrita preservando valores atuais de áudio passaram.
- Lifecycle Bluetooth: o painel retornou 0 watchers ativos nesta execução; o check confirma liberação, não comprova descoberta funcional. Pareamento, conexão e rádio não foram alterados.
- `dotnet publish` Release, win-x64, self-contained, single-file, avisos tratados como erros: concluído sem erros/avisos. Inno Setup 6.7.3: compilação concluída.
- Instalador: `ThinkWind-Setup-0.10.0-alpha.exe`. SHA-256: `0407D16D0B5CC1FE90212436C7FCA2FB09F629DA19E8152E70F21ECC7DB0ACB5`.
- Inclui a correção recente do fechamento do menu pela tecla Windows; ainda sem confirmação interativa específica após essa correção. Testes de composição/DWM não comprovam todas as interações do desktop.
- Sem nova comparação de jogos/ETW nesta entrega. Os números históricos acima não medem a latência física da versão final. Sem teste completo de instalação/desinstalação, assinatura digital ou validação exaustiva de DPI/múltiplos monitores.