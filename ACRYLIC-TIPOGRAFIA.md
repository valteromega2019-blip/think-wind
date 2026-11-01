# Acrylic, contraste e tipografia

Branch: `feature/acrylic-tipografia`. Data: 01/11/2026.

## Causa identificada

`FloatingWindow.ApplyAppearance`, `WindowPreview.ConfigurePlayerSurface` e
`BrowserTabFan` impunham opacidade mínima de 82% somente no tema claro. Isso
encobria o backdrop nativo e fazia o ajuste de transparência parecer inoperante.
O recurso `Think.Popup` também era completamente opaco, e os popups WPF não
possuíam o HWND de composição independente utilizado pelas barras.

Foi removido o limite específico do tema claro. Dock, ilha, painéis derivados de
`FloatingWindow`, menus, tooltips, seletor de cores, lista de fontes e tutorial
utilizam `BackdropHost` / `BackdropComposition`, com a camada WPF em alfa e
arredondamento correspondente ao contêiner. O material acompanha abertura,
movimento, DPI e fechamento; menus liberam seus recursos após o evento Closed.
No alto contraste do Windows ou sem suporte nativo, o fallback sólido é
intencional, para manter a interface utilizável.

## Aparência e tipografia

- Material e opacidade das duas barras estão reunidos em Aparência, em grupos
  individuais. Os blocos usam sombreamento neutro translúcido, evitando o véu
  esbranquiçado e tonalidades excessivas sobre fundos coloridos.
- Tipografia inclui busca entre fontes instaladas, HEX, RGB, prévia imediata,
  contraste automático e restauração. Padrões: Segoe UI Variable, `#F4F6FA`,
  contraste automático ligado, escala 100%.
- Preferências são normalizadas e salvas pelo SettingsStore existente. Fontes
  ausentes usam fallback Segoe UI Variable / Segoe UI. Não há alteração das
  fontes ou cores de texto globais do Windows.
- Texto secundário usa a mesma tinta legível do texto principal. O contraste
  automático preserva a cor escolhida se atingir 4,5:1; caso contrário escolhe
  branco ou preto. Considera a tonalidade da superfície e a composição dos
  blocos ancestrais. A opção manual preserva a cor escolhida.
- Recursos `Think.FontFamily`, `Think.Text` e `Think.Muted` são dinâmicos.
  Fontes de símbolos permanecem independentes para não corromper ícones.
  Mudanças de família/escala invalidam o layout das barras; não há novo timer
  de coleta ou polling de entrada. O comportamento do Modo Jogo foi preservado.
- O tutorial solicita a área de trabalho via `Shell.Application.MinimizeAll`
  ao iniciar, sem fechar aplicativos; os documentos continuam abertos.

## Arquivos

- `Services/ThemeService.cs`, `Typography.cs`, `FontSizing.cs`,
  `PreferenceStamp.cs`, `SettingsStore.cs`, `OverlayAppearanceOptions.cs`.
- `Native/BackdropHost.cs`, `Views/PopupBackdrop.cs`, `FloatingWindow.cs`,
  `IslandStatusWindow.cs`, `WindowPreview.cs`, `BrowserTabFan.cs`, `MediaCard.cs`.
- `App.xaml`, `Views/SettingsWindow.cs`, `SettingsWindow.Colors.cs`,
  `SettingsWindow.Typography.cs`, `ColorPickerPopup.cs`, `TutorialWindow.cs`,
  `HardwareOverlayWindow.cs`.
- `checks/ThinkWind.Checks/AppearanceChecks.cs`, `LayoutChecks.cs`,
  `OverlayChecks.cs`, `Program.cs`.

As alterações pendentes de integração de players foram preservadas fora deste
commit; em WindowPreview foi registrada somente a remoção do limite de opacidade,
e em MediaCard somente a atualização dos recursos de texto do tema.

## Validação

Executados conforme solicitação explícita nesta tarefa:

- Build Release de app e checks: zero erros. O workspace contém um aviso CS8602
  em `WindowPreview.cs:68`, pertencente ao rascunho anterior da integração de mídia.
- Bateria de regressão: 54 aprovados, zero falhas, zero ignorados.
- HWNDs nativos, alfa central de 30% em ambos os temas, cantos externos
  transparentes e sincronização durante expansão do Dock e ilha.
- Composição nativa dos menus nos dois temas e liberação após fechamento.
- Contraste em 125 fundos RGB e preservação da cor no modo manual.
- Persistência, fonte inválida/ausente e cor inválida.
- Dois temas, três fundos, Arial/Consolas/Segoe UI Variable quando instaladas,
  medidas de texto com escalas 100%, 125%, 150%, 200% e 250%.
- Regressão de áudio, calendário, layouts, miniaturas, políticas de jogo,
  identidade de aplicativos e tutorial.

Os testes de escala são medidas WPF em DIP, não uma mudança física de DPI de
todos os monitores. O contraste calculado usa as tonalidades configuradas; um
papel de parede ou vídeo real atrás de transparência muito baixa pode mudar a
cor percebida. A conferência visual desses fundos, de todas as fontes possíveis
e do tutorial interativo continua necessária. Os testes não medem latência em
jogo ou qualidade perceptiva do desfoque. Nenhum instalador foi gerado e esta
branch não foi publicada.

Referências: [HostBackdropBrush](https://learn.microsoft.com/en-us/uwp/api/windows.ui.composition.compositor.createhostbackdropbrush)
e [recursos dinâmicos WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/systems/xaml-resources-how-to-use-system).

## Organização posterior de Aparência

A página foi separada em Tema e cores, Superfícies e transparência, Tipografia
e Animações. Os ajustes das duas barras e dos blocos internos têm identificação
própria, com explicações curtas. Fonte, escala, cor, contraste e prévia ficaram
agrupados dentro de Tipografia. O template da lista de fontes e dos demais
seletores reutiliza `Think.SettingsScrollViewer`, incluindo o acabamento e a
barra de rolagem usados nas configurações, com a altura máxima do seletor.

Arquivos desta revisão: `App.xaml`, `Views/SettingsWindow.cs`,
`SettingsWindow.Appearance.cs` e `SettingsWindow.Typography.cs`.
Esta revisão recebeu apenas leitura de código e diff; não foram executados
novos testes, type check ou compilação. Os resultados anteriores acima não
representam validação destas alterações posteriores.
