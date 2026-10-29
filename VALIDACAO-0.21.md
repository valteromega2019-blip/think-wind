# Think Wind v0.21 — composição e interação

Data: 29/10/2026. Versão de desenvolvimento `0.21.0-dev`. O usuário autorizou expressamente a validação desta atualização. Nenhum instalador nem pacote de publicação foi gerado.

## Causa e correção da transparência

Foram tratados dois problemas distintos:

1. `Background=Transparent` em uma janela WPF convencional não habilita transparência por pixel. A janela de conteúdo agora usa `AllowsTransparency=true`, alvo com fundo transparente, template sem recipiente opaco de tema e recorte alfa aplicado ao conteúdo inteiro.
2. O Acrylic por `DWMWA_SYSTEMBACKDROP_TYPE` entra em fallback sólido quando sua janela está inativa. Um launcher não pode depender de roubar foco para manter transparência. O Acrylic agora usa `Windows.UI.Composition.Compositor.CreateHostBackdropBrush` em um `DesktopWindowTarget`, habilitado por `DWMWA_USE_HOSTBACKDROPBRUSH`.

A camada de material possui um HWND nativo independente, sem pincel de fundo GDI, sem bitmap de redirecionamento (`WS_EX_NOREDIRECTIONBITMAP`) e sem ativação. A janela WPF de controles fica imediatamente acima dela. Posição, tamanho e recorte acompanham a animação. O material não recebe cliques, e suas áreas externas são excluídas por região; os pixels WPF externos possuem alfa zero. Não há sobreposição de um fundo retangular opaco com um painel arredondado.

Mica continua disponível como opção explicitamente opaca, baseada no papel de parede. Em alto contraste ou indisponibilidade do material, o fallback é sólido. As opções de transparência do Windows são preservadas; o aplicativo não muda políticas nem configurações de privacidade.

A integração usa as projeções oficiais do SDK Windows via TFM `net10.0-windows10.0.19041.0`, sem biblioteca de Acrylic de terceiros. O compositor e a fila do dispatcher são compartilhados e finalizados ao encerrar o aplicativo.

## Três estágios

- **Compacto:** relógio, rede e volume, integrado ao topo. Esses três acessos essenciais permanecem presentes; a personalização dos demais acessos vale para a expansão e para o editor.
- **Hover:** após 180 ms com o ponteiro na ilha, apresenta os demais atalhos configurados, conexão ativa, RAM opcional e botão Painel completo. A saída do mouse aguarda 450 ms. Não abre os módulos completos por conta própria.
- **Painel completo:** aberto explicitamente pelo botão ou pelo relógio/calendário. Reúne os módulos preservados, processos da sessão e acesso às notificações nativas. Fechar retorna ao estágio anterior; mover o mouse para fora do painel completo não o recolhe enquanto ele está sendo usado.

Durante personalização/edição, o painel completo é mantido para pré-visualização, conforme o comportamento já existente. A visualização de processos é real e somente leitura; não equivale a reproduzir os ícones da bandeja. Notificações abrem a central nativa com Win+N: não foi criado um feed próprio de notificações nem solicitado acesso aos alertas de outros aplicativos.

## Mini-ilhas

- **Volume:** volume real do endpoint, mudo, lista de dispositivos a controlar e acesso à configuração de som. A seleção não muda o dispositivo padrão do Windows, preservando a semântica anterior.
- **Rede:** adaptador ativo, tipo de conexão e IP local. Em Wi-Fi, tenta obter SSID e sinal pela API WLAN; se a política do Windows negar a consulta, mantém as informações do adaptador sem solicitar permissões. Não afirma que conexão ativa garante acesso à internet.
- São janelas independentes com o mesmo material, tema e geometria; abrir uma delas não promove a ilha principal a painel completo. O hover fica suspenso durante seu uso. Fecham por Esc ou perda de foco, sem fechar a ilha principal.
- Há rolagem para conteúdo em telas menores e ancoragem ao destino da animação para evitar saltos. Ao trocar de mini-ilha, a anterior é fechada. Um intervalo de proteção evita reabertura involuntária ao clicar no mesmo ícone para fechá-la.

## Validação executada

- Build Release: **zero erros e zero avisos**.
- Runner de checks: **29 aprovados, zero falhos, zero ignorados** na execução final.
- Regressão: leitura/gravação de preferências isoladas, JSON inválido, atalhos, falhas de escrita, enumeração de janelas e memória.
- Transições: compacto/hover, retorno do painel completo ao estágio anterior e bloqueio de expansão automática com mini-ilha aberta.
- Composição em quatro combinações (dock/ilha × claro/escuro): conteúdo layered, material separado não-layered e sem bitmap GDI, limites coincidentes, recorte nativo, centro com alfa parcial e canto externo com alfa zero.
- As quatro combinações também amostraram os limites das superfícies durante a animação e verificaram as dimensões finais.
- Mini de volume: evento Esc fecha e notifica o proprietário uma única vez. Mini de rede: ativar outra janela de teste fecha o painel.
- Rede: snapshot real do sistema. Áudio: cinco saídas e três entradas enumeradas; leitura de todos e regravação dos valores atuais do primeiro endpoint de cada tipo, sem alterar seu nível/mudo.
- Inspeção visual pela skill Computer Use: compacto, hover, painel completo e abertura independente da mini-ilha de volume observados em execução.
- Foi aberto um fundo colorido temporário para conferência. O usuário confirmou: **“As cores aparecem através dos componentes”**. A captura por janela não foi usada isoladamente como prova de composição do desktop.

Não foram realizados ensaios exaustivos em outros computadores, todas as escalas/DPI, monitores HDR, desconexão física de Wi-Fi/áudio ou com todas as políticas de acessibilidade/energia. O sinal/SSID em hardware Wi-Fi e cada seletor interativo de dispositivo ainda precisam de conferência nos respectivos equipamentos.

## Executar localmente

O build desta atualização está em `src/ThinkWind/bin/Release/net10.0-windows10.0.19041.0/ThinkWind.exe`. Usa o runtime .NET 10 instalado no ambiente. Os binários antigos em `artifacts` continuam pertencendo à entrega 0.2.0.

Os checks só devem ser repetidos quando solicitados:

```powershell
dotnet run --project checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release --no-launch-profile
```

O argumento `--visual-background` do runner abre somente o fundo temporário de verificação; não faz parte do launcher. `--inspect` continua sendo um modo de inspeção do launcher e expõe os HWNDs para as ferramentas; o modo normal não adiciona as superfícies à barra original.

Referências oficiais: [transparência WPF](https://learn.microsoft.com/dotnet/api/system.windows.window.allowstransparency), [HostBackdropBrush](https://learn.microsoft.com/uwp/api/windows.ui.composition.compositor.createhostbackdropbrush), [composição com WPF](https://learn.microsoft.com/windows/uwp/composition/using-the-visual-layer-with-wpf) e [fila de dispatcher](https://learn.microsoft.com/windows/win32/api/dispatcherqueue/nf-dispatcherqueue-createdispatcherqueuecontroller).
