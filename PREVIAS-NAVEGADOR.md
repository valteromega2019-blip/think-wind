# Abas do navegador e chamada lateral

## Apresentação

- Até quatro abas acessíveis em um leque vertical, com abertura por clique e animação curta que respeita movimento reduzido.
- Seleção por UI Automation (`SelectionItemPattern`), usando o identificador de runtime e conferindo o processo proprietário. Abas ARIA dentro de páginas são descartadas.
- As abas selecionadas e a aba reconhecida como Meet têm prioridade dentro do limite de quatro.
- Um cartão lateral do Meet preserva o navegador. Os controles só aparecem após reconhecer a chamada real. Se apenas a aba for encontrada, aparece uma opção de abrir a reunião.
- Estados de microfone e áudio são inferidos das ações acessíveis nativas: ativo usa destaque; mudo usa vermelho, ícone riscado e texto explícito. Estado não reconhecido permanece neutro.

## Limitação de imagens por aba

DWM disponibiliza imagens por HWND, não por aba interna do navegador. Sem extensão, apenas a aba ativa à frente do leque usa a imagem ao vivo da janela; as demais mostram título e opção de abertura. A integração opcional em `extensions/orby-tabs` fornece a última captura de abas visitadas. Veja `EXTENSAO-NAVEGADOR.md` para instalação, autorização e limitações.

Não são trocadas abas automaticamente para capturar imagens. O clique do usuário seleciona a aba e ativa a janela. Não são armazenadas capturas ou histórico das abas em disco.

Se o navegador não expuser abas por acessibilidade, permanece uma prévia da janela com opção de abertura. Idiomas, abas recolhidas em grupos e políticas de acessibilidade podem limitar a listagem.

As consultas ocorrem ao abrir e a cada dois segundos com a prévia visível; são serializadas fora do Dispatcher e recebem cancelamento ao fechar. Recursos DWM são liberados ao trocar ou fechar a prévia. Provedores UIA individuais podem demorar a responder e não oferecem cancelamento forçado.

Revisado por leitura e diff, sem compilação, testes ou type check nesta alteração. O funcionamento ainda precisa de conferência em execução. A confirmação anterior do usuário valida a integração inicial do Discord, não as alterações deste documento.

Referências: [DWM](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail), [UI Automation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.selectionitempattern.select), [Chrome tabs](https://developer.chrome.com/docs/extensions/reference/api/tabs).
