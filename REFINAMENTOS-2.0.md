# Think Wind 2.0 — refinamentos em desenvolvimento

**Documento histórico da etapa anterior.** A atualização de composição/interação seguinte é a v0.21, descrita e validada em [VALIDACAO-0.21.md](VALIDACAO-0.21.md). As afirmações de ausência de compilação abaixo se referem ao momento desta etapa, antes dos pedidos posteriores de execução/validação.

Esta etapa altera somente o código-fonte. Não foram executados testes, type check, build, publish ou inspeção da interface em execução. Nenhum instalador foi gerado. Os binários existentes em `artifacts` continuam sendo os da versão experimental 0.2.0.

## Direção visual

Referência local: `D:\think-monitor\docs\styles.css`, identidade Think: grafite `#111318`, superfícies `#1A1E25`, destaque `#60CDFF`, Segoe UI Variable, geometria simples e espaçamentos consistentes. A implementação é independente do Think Monitor; não importa seus arquivos nem depende do outro aplicativo.

Os temas claro, escuro e automático usam recursos dinâmicos compartilhados. Alterações do tema do Windows atualizam o modo automático. Alto contraste usa cores do sistema e fundo sólido; a configuração de animações do Windows é respeitada.

## Barra inferior

- Botões de ícones transparentes, sem fundos individuais nem placas no hover. Foco de teclado mantém um contorno de acessibilidade.
- Ícones do Shell para executáveis e atalhos; busca em diretórios do Windows/PATH para nomes como `explorer.exe`; resolução de `.lnk` para associar atalhos aos processos. O ícone de Configurações é solicitado ao namespace AppsFolder.
- Para aplicativos abertos, consulta ao ícone da janela e de sua classe, com timeout; ícones emprestados não são destruídos e ícones retornados pelo Shell são liberados.
- Janelas do mesmo executável agrupadas no mesmo item, com acesso a cada janela pelo menu de contexto. Aplicativos protegidos podem não expor caminho/ícone; nesse caso permanece um pictograma neutro, sem inventar uma marca.
- Indicadores distintos para foco, execução e minimização; largura medida pelo conteúdo, altura incluindo barra de rolagem quando houver transbordamento.
- Ordem persistente para ícones, aplicativos e comandos do sistema, por arraste no modo de edição ou pelos menus. Atalhos continuam podendo ser adicionados, fixados e removidos.

## Ilha superior

- Posicionamento na borda física superior do monitor, sem margem; cantos superiores retos e arredondamento somente na base.
- Relógio e acessos rápidos configuráveis: Wi-Fi, Bluetooth, áudio, notificações, calendário, modo avião e personalização. Wi-Fi/Bluetooth/modo avião abrem as configurações nativas. O acesso rápido a notificações envia o atalho nativo Win+N.
- Estados recolhido, expandido e calendário; tamanho calculado pelo conteúdo com limite da área útil e rolagem. A animação redimensiona a janela, sem esticar fontes ou imagens.
- Ordem/visibilidade dos acessos e módulos editáveis. O relógio também pode ser movido ou ocultado. Os comandos para expandir e concluir edição permanecem disponíveis.
- Volume, microfone, mídia, memória, bateria e calendário preservados. Controles de áudio são reutilizados ao aplicar preferências para manter a seleção temporária de dispositivo.
- Calendário WPF com templates próprios para acompanhar os temas e preservar navegação/seleção nativas.

## Material e geometria

Uma única camada de tonalidade cobre cada janela. Os cartões internos da ilha usam tonalidades translúcidas; textos e ícones permanecem nítidos. Contornos explícitos do WPF foram removidos e o DWM recebe `DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE`. O recorte nativo acompanha tamanho, raio e DPI, usando uma região própria com topo reto na ilha.

Materiais: Acrylic, Mica e sólido. A API documentada do DWM não oferece ajuste arbitrário do raio do desfoque; por isso o painel distingue a escolha de material da intensidade/opacidade da tonalidade. O efeito depende das configurações de transparência do Windows. Em caso de indisponibilidade, o fundo é sólido. Isso precisa de conferência visual futura em cada combinação de material, tema, DPI e raio; não há alegação de eliminação de artefatos verificada em execução.

O posicionamento usa retângulos físicos dos monitores e DPI efetivo para evitar converter a origem de um monitor com a escala de outro. A identificação do monitor é persistida com fallback para o índice salvo. Essa adaptação ainda não foi verificada em hardware nesta etapa.

## Personalização e editor

Abra **Personalizar → Editor de layout → Editar layout na tela**, ou use o menu da bandeja. Arraste ícones da barra e acessos da ilha; arraste os títulos dos módulos. Linhas ciano indicam inserção e o centro dos componentes. Os puxadores laterais ajustam a posição horizontal, mantendo a ilha presa ao topo. Na personalização, as listas também oferecem arraste e botões subir/descer.

Use as caixas de seleção para ocultar ou adicionar novamente controles/módulos. **Restaurar barra**, **Restaurar ilha** e **Restaurar tudo** recuperam os padrões do aplicativo; não alteram por conta própria a inicialização configurada no Windows.

Não há botão Aplicar. Mudanças atualizam os componentes e são salvas após 500 ms sem novas alterações. Fechar a personalização ou sair do aplicativo força a gravação pendente. Erros de gravação aparecem no rodapé da personalização, sem afirmar que os dados foram salvos.

Enquanto a personalização está aberta, a ilha permanece expandida para pré-visualização e a barra não se oculta automaticamente. Fechar o painel recolhe a ilha, exceto quando o modo de edição permanece ativo.

Os campos antigos continuam sendo lidos. `TintOpacity` é usado como valor inicial quando não há opacidade específica da barra/ilha; as novas listas são normalizadas, preservando IDs válidos e acrescentando controles conhecidos. O modo de edição é temporário; as ordens, posições e opções visuais são persistentes.

## Revisão ainda necessária quando solicitada

Compilação e checks; interação de arraste/cancelamento/teclado; carregamento dos templates de calendário; transições em tela pequena; materiais e recortes em DPI misto; ícones de aplicativos protegidos/empacotados; persistência após reinício; consumo e desconexão de monitores. O relatório `TESTES.md` é histórico da versão 0.2.0 e não valida estas alterações.

Referências das APIs: [atributos DWM](https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute), [DPI por monitor](https://learn.microsoft.com/windows/win32/hidpi/declaring-managed-apps-dpi-aware) e [partes do calendário WPF](https://learn.microsoft.com/dotnet/desktop/wpf/controls/calendar).
