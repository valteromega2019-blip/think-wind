# Abas do navegador e chamada lateral

## Apresentação

- Até quatro abas acessíveis em um leque vertical, com abertura por clique e animação curta que respeita movimento reduzido.
- Cartões alinhados pelo mesmo eixo, com inclinação progressiva discreta somente nos cartões traseiros. Cada cartão tem superfície translúcida arredondada sobre o Acrylic nativo da prévia, acompanhando cor e opacidade configuradas. A área reserva espaço para os cantos inclinados. O cartão frontal e a área de comandos do Meet ficam alinhados pela base.
- A inclinação traseira é em profundidade vertical, em torno do eixo X, com `Viewport2DVisual3D`; não é uma rotação lateral no plano da tela. A miniatura frontal continua reta, preservando o posicionamento nativo DWM.
- Seleção por UI Automation (`SelectionItemPattern`), usando o identificador de runtime e conferindo o processo proprietário. Abas ARIA dentro de páginas são descartadas.
- As abas selecionadas e a aba reconhecida como Meet têm prioridade dentro do limite de quatro.
- Um cartão lateral do Meet preserva o navegador. Os controles só aparecem após reconhecer a chamada real. Se apenas a aba for encontrada, aparece uma opção de abrir a reunião.
- Estados de microfone e áudio são inferidos das ações acessíveis nativas: ativo usa destaque; mudo usa vermelho, ícone riscado e texto explícito. Estado não reconhecido permanece neutro.

## Limitação de imagens por aba

DWM disponibiliza imagens por HWND, não por aba interna do navegador. Apenas a aba ativa à frente do leque usa a imagem ao vivo da janela; as demais mostram título e opção de abertura. A funcionalidade que exigia instalar extensão foi removida por solicitação do usuário, incluindo o conector, o cache de imagens e as opções de registro.

O cartão de comandos do Meet usa uma área de 320 × 160 DIP, proporcional à miniatura ao lado, com redução uniforme em escalas maiores para preservar o conteúdo. Os comandos da chamada ficam visíveis; as opções de áudio do Windows ficam no menu de três pontos. O cartão individual do Discord mantém a apresentação completa.

Não são trocadas abas automaticamente para capturar imagens. O clique do usuário seleciona a aba e ativa a janela. Não são armazenadas capturas ou histórico das abas em disco.

Se o navegador não expuser abas por acessibilidade, permanece uma prévia da janela com opção de abertura. Idiomas, abas recolhidas em grupos e políticas de acessibilidade podem limitar a listagem.

Ao minimizar, o launcher conserva em memória as últimas quatro abas identificadas por janela (até 16 janelas), verificando a identidade do processo e descartando janelas encerradas. O tooltip identifica a lista em cache. A atualização aproveita a consulta já existente do Dock quando muda o foco ou título do navegador, sem novo temporizador. Hover não restaura o navegador; somente um clique restaura e seleciona a aba, com recuperação por título único caso o identificador de acessibilidade tenha mudado. Se o navegador já estava minimizado antes de qualquer identificação e não expõe sua árvore, a lista ainda não pode ser reconstruída sem abri-lo uma vez.

As consultas ocorrem ao abrir e a cada dois segundos com a prévia visível; são serializadas fora do Dispatcher e recebem cancelamento ao fechar. Recursos DWM são liberados ao trocar ou fechar a prévia. Provedores UIA individuais podem demorar a responder e não oferecem cancelamento forçado.

Revisado por leitura e diff, sem compilação, testes ou type check nesta alteração. O funcionamento ainda precisa de conferência em execução. A confirmação anterior do usuário valida a integração inicial do Discord, não as alterações deste documento.

Referências: [DWM](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail), [UI Automation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.selectionitempattern.select), [Chrome tabs](https://developer.chrome.com/docs/extensions/reference/api/tabs).
