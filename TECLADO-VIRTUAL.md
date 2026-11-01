# Teclado personalizado

Implementação isolada na branch `feature/teclado-virtual`, a partir da base 0.22 beta (`9462dd1`). O atualizador permanece em `feature/atualizador-github`; a branch `feature/acrylic-tipografia` não foi modificada.

## Acesso e aparência

- O controle existente `keyboard` agora chama `orby:keyboard`, tratado internamente, sem registrar protocolo no Windows. Funciona na barra superior e nos controles secundários que usam o catálogo.
- Em Personalizar → Teclado é possível escolher teclado próprio/nativo, layout PT-BR ou US, escala e painel numérico. Preferências são persistidas pelo SettingsStore existente.
- A janela própria reutiliza FloatingWindow e o compositor Acrylic, recursos de fonte, cores, opacidade e arredondamento do launcher.
- Posicionamento inicial centralizado na área útil inferior do monitor configurado, usando coordenadas nativas e DPI. Pode ser movida pelo título. Não reserva nem modifica a área de trabalho.
- O ícone permanece opcional, respeitando as configurações existentes de visibilidade. A seção Teclado também oferece acesso direto.

## Entrada

- Letras, números, pontuação, símbolos e acentos; Shift, Caps, Ctrl, Alt, AltGr e Win; Esc, Tab, Backspace, Enter, F1–F12, Insert/Delete, Print Screen, Home/End, Page Up/Down e setas. Painel numérico opcional.
- Modificadores são selecionados para a próxima tecla e enviados em um único lote com liberação imediata. Caps é um estado local do teclado virtual, sem alterar o Caps Lock físico. Acentos podem ser combinados com letras; Espaço ou repetição do acento insere o caractere literal.
- Texto usa SendInput/KEYEVENTF_UNICODE; atalhos usam VkKeyScanEx com o layout da janela de destino. A seleção visual PT-BR/US não modifica o layout global do Windows.
- WS_EX_NOACTIVATE e WM_MOUSEACTIVATE impedem a janela do teclado de tomar foco durante cliques. O botão da ilha também preserva o foco antes da ativação. O último destino externo é observado pelos eventos de primeiro plano já existentes, sem novos hooks globais.
- Não registra, armazena nem transmite o conteúdo digitado. O reconhecimento de modificadores físicos é uma consulta pontual por clique, sem polling em loop.
- Nenhum serviço ou temporizador próprio. Letras e estados são atualizados sem reconstruir a grade a cada tecla. A janela é criada sob demanda, ocultada pelo Modo Jogo existente e libera modificadores locais ao ocultar.

## Limitações explícitas

Este é um teclado próprio, não uma alteração visual do executável osk.exe. Não existe nesta implementação equivalência integral com os recursos de acessibilidade do teclado nativo. Predição, varredura/seleção por permanência, tela de entrada, área de trabalho segura/UAC e outras funções protegidas continuam disponíveis pelo botão **Teclado Windows** e pela opção de usar o nativo por padrão.

SendInput respeita UIPI: uma interface normal não injeta entrada em aplicativos de integridade superior. Não há bypass de permissões, elevação da interface nem envio de Ctrl+Alt+Del. Não foi acrescentado acesso a campos protegidos. O retorno bem-sucedido da API informa inserção no fluxo de entrada, não garante que todo aplicativo aceite Unicode ou execute um atalho específico.

O teclado não foi compilado nem executado nesta etapa. Testes e type check não foram executados, conforme a preferência permanente do usuário. Revisão realizada somente por leitura do código e do diff. A prévia do atualizador anteriormente aberta não recebe estas alterações até uma compilação e execução desta branch.

## Arquivos

- `Native/VirtualKeyboardInput.cs`: entrada sob demanda e preservação de modificadores físicos.
- `Views/VirtualKeyboardWindow.cs`: teclado e interface Acrylic.
- `Views/IslandWindow.Keyboard.cs` e chamada em IslandWindow.cs: preservação do destino ao clicar no ícone.
- `Views/SettingsWindow.Keyboard.cs` e chamada em SettingsWindow.cs: preferências.
- `Services/LayoutCatalog.cs` e SystemActions.cs: roteamento interno.
- `Services/SettingsStore.cs`: persistência/normalização.
- `App.xaml.cs`: criação, alternância, observação pelo serviço existente e descarte.

Referências: [SendInput e UIPI](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput), [WM_MOUSEACTIVATE](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mouseactivate), [teclados personalizados sem ativação](https://learn.microsoft.com/en-us/archive/msdn-magazine/2012/january/screen-based-input-customized-on-screen-keyboards-with-the-net-framework).
