# Think Wind — atualização v0.05

Versão técnica `0.5.0-dev`, sobre a arquitetura WPF/Win32 existente. Implementação em código na branch `developer`. Não há imagem de referência anexada nesta etapa; os componentes seguem os recursos visuais já definidos no projeto.

## Menu Iniciar próprio

O botão Iniciar da barra inferior abre `LauncherPanelWindow`, uma janela própria com o mesmo host Acrylic e recorte transparente dos overlays. Esse botão não envia a tecla Windows nem abre o Menu Iniciar original. O atalho Windows do teclado continua disponível para o sistema.

O painel fica acima da barra, com espaço de 8 DIP e limites de monitor/área útil. Oferece Fixados, Recentes, Todos e Biblioteca, pesquisa e resultados paginados, evitando barras de rolagem na superfície principal. Em áreas úteis pequenas, o conteúdo pode ser reduzido proporcionalmente. A conta exibida é a do usuário atual.

- Fixar e remover atalhos do Iniciar pelo menu de contexto.
- Reorganizar fixados por arraste ou pelos comandos de mover.
- Fixar/remover atalhos da barra sem desinstalar aplicativos.
- Recentes combinam aberturas realizadas pelo launcher e atalhos da pasta Recent do Windows, quando disponíveis.
- Opções reais de suspender, reiniciar e desligar, com confirmação no aplicativo. Não usam encerramento forçado. Permissões, suporte de suspensão e aplicativos com documentos não salvos continuam sujeitos ao Windows.

Nenhuma ação de energia foi executada durante o desenvolvimento.

## Biblioteca de Aplicativos

`ApplicationCatalog` consulta AppsFolder e os atalhos do Menu Iniciar do usuário e da máquina em uma thread STA separada. A biblioteca reúne aplicativos tradicionais com atalhos registrados e aplicativos Store/MSIX expostos pelo Shell, com nomes e ícones recuperados do Windows. Não é uma varredura de todos os executáveis do disco nem inclui obrigatoriamente instalações sem atalhos ou registro no Shell.

As categorias usam as pastas reais do Menu Iniciar e o tipo Store/MSIX. Também existe ordem alfabética. Identidades, destinos e entradas repetidas das fontes são conciliados quando disponíveis. Entradas distintas com nomes semelhantes podem continuar aparecendo; versões/perfis com argumentos diferentes exigem atenção ao fixar.

A busca da Biblioteca se limita aos aplicativos. O botão `+` de cada resultado adiciona o atalho à barra com um clique e indica quando ele já está fixado. A opção de selecionar um executável/atalho manualmente continua no painel e no menu do botão da Biblioteca.

Mudanças nos diretórios de atalhos acionam atualização com debounce. AppsFolder também é consultado ao abrir o painel e a cada 30 segundos enquanto estiver visível, abrangendo instalações e remoções Store/MSIX. A identidade AUMID do processo é usada para associar janelas a aplicativos fixados quando o Windows a disponibiliza; processos hospedeiros ou sem identidade podem exigir associação pelo executável e ainda aparecer separados.

## Pesquisa integrada

Há uma área reservada ao lado do Iniciar, configurável como **barra compacta** ou **apenas lupa**. A largura externa dessa área permanece fixa durante o foco; somente o campo interno anima. Isso impede que digitar provoque redimensionamento dos demais itens da barra.

O painel de pesquisa é do Think Wind. Reúne aplicativos, atalhos recentes, destinos conhecidos de configurações do Windows e arquivos/documentos consultados por nome no `SYSTEMINDEX`, via provedor oficial Windows Search/ADO. Os resultados recebem ícones do Shell e são abertos pelo sistema. Enter abre o primeiro resultado, seta para baixo transfere o foco ao painel e Esc fecha.

A consulta ao índice tem debounce de 250 ms, cancelamento de resultados obsoletos, execução serial fora da thread visual e limite de 60 arquivos por consulta. A busca respeita a cobertura e as permissões do índice. Se o serviço/provedor estiver indisponível, o painel informa essa situação e continua pesquisando aplicativos, recentes e configurações. As configurações pesquisáveis são os destinos implementados no catálogo, não todos os campos internos do aplicativo Configurações. Não há dependência visual do painel tradicional de pesquisa do Windows.

## Bandeja na ilha superior

O ícone de bandeja do cabeçalho abre uma mini-ilha independente, sem promover a ilha ao painel completo. Os ícones expostos pelo Explorer são atualizados periodicamente enquanto a mini-ilha está aberta. Os botões usam estilo transparente, sem fundos individuais; clique e botão direito continuam usando as interações nativas que o controle disponibilizar.

Não há API pública geral para recuperar todos os HICONs de terceiros da área de notificação. O serviço existente utiliza acessibilidade e captura da região do ícone visível, com remoção conservadora de tonalidade uniforme conectada às extremidades. Isso não equivale a extrair o HICON original; fundos não uniformes, ícones ocultos e provedores sem Invoke/foco podem limitar o resultado. A bandeja original por Win+B continua acessível. Não há leitura de memória, injeção ou reparenting do Explorer.

## Barra expandida e recolhida

O novo tempo padrão é **0,8 segundo**, ajustável de 0 a 10 segundos. A preferência de ocultação automática já salva é preservada; instalações novas e a restauração da barra habilitam a ocultação por padrão.

O estado recolhido é uma **faixa visível de 6 DIP em toda a largura do monitor selecionado**. Sua coordenada inferior é calculada diretamente de `Screen.Bounds.Bottom`, em pixels físicos: distância final de **0 pixels da borda inferior**, inclusive se a barra original reservar uma área de trabalho menor. A faixa não é uma janela invisível maior que sua superfície visível.

O estado expandido continua flutuante na área útil, com escala, tamanho, arredondamento, deslocamento e espaçamento configuráveis. Esses ajustes não mudam a geometria recolhida. Personalizar dimensões não força a expansão de uma barra que já esteja recolhida. Desativar a ocultação ou entrar explicitamente no editor pode expandi-la. A janela de configurações continua independente.

## Drag & Drop de arquivos

O suporte usa o Drag & Drop OLE do WPF e o objeto de dados original do Windows (`CF_HDROP`). Não consulta o conteúdo da área de transferência nem adivinha que qualquer movimento com o botão pressionado seja um arrasto de arquivos.

1. Ao entrar na faixa inferior visível com arquivos, a barra se revela imediatamente, sem clique.
2. Ela permanece aberta enquanto o botão esquerdo estiver pressionado durante essa operação, mesmo quando o cursor sair da barra para ir ao aplicativo.
3. Sobre um aplicativo aberto, após 450 ms, tenta restaurar e ativar a janela, sem alterná-la para minimizada. O indicador informa que é um alvo de ativação. O usuário continua o arrasto e solta **dentro da interface do aplicativo**; o launcher não confirma uma entrega que não realizou.
4. Sobre um aplicativo fechado, consulta o `IDropTarget` nativo do item do Shell por `IShellItem.BindToHandler`. Somente um receptor que anuncie aceitação permite o drop. O objeto original preserva múltiplos arquivos e formatos do Shell. São permitidos efeitos de abrir/copiar/vincular, nunca mover ou apagar os arquivos de origem.
5. As setas de páginas da barra aceitam hover de arquivo para acessar itens de outras páginas. Após o drop/cancelamento, o recolhimento volta a respeitar o tempo configurado.

Aceitação pelo receptor não garante que o programa entenda cada formato de arquivo ou que tenha concluído seu processamento. Aplicativos sem receptor compatível devem ser abertos e receber o arrasto dentro de sua própria janela. Arquivos virtuais de e-mail ou outras fontes que não ofereçam `CF_HDROP` não estão cobertos. Restrições de integridade/UIPI, programas elevados e bloqueios de ativação do Windows são preservados. Não é usado WM_DROPFILES nem envio artificial de teclas para simular recebimento.

## Estado da entrega

Revisão por leitura de código e diff. **Testes, type check, build, publish e validação visual em execução não foram realizados nesta atualização**, conforme a preferência permanente do usuário. Nenhum instalador foi gerado. O executável local consultado ainda identifica a compilação `0.3.0-dev`; não contém a v0.04 nem a v0.05.

Ficam pendentes de validação em execução, quando solicitada: carregamento real do catálogo, disponibilidade do índice, ativação Store/MSIX, foco entre barra e painel, menus da bandeja, posição em DPI/monitores diferentes e negociação OLE com cada programa de destino. Nenhum resultado histórico de compilação ou checks valida esta atualização.

Referências: [AppsFolder/AUMID](https://learn.microsoft.com/en-us/windows/configuration/store/find-aumid), [Windows Search SQL/ADO](https://learn.microsoft.com/windows/win32/search/using-sql-and-aqs-to-query-the-index), [destinos de configurações](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings), [BindToHandler](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitem-bindtohandler), [objeto de dados do Shell](https://learn.microsoft.com/en-us/windows/win32/shell/dataobject).
