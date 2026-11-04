# Prévia temporária de janelas no desktop

Implementação em `feature/player-persistente`.

- Permanecer 300 ms sobre a imagem de uma janela mostra uma reprodução DWM no desktop, na posição da janela. Para janelas minimizadas, usa a posição de restauração informada pelo Windows, convertida de coordenadas da área útil quando necessário.
- A janela real nunca é restaurada, ativada, movida ou reordenada no hover. O clique mantém a ativação existente.
- Sair da imagem, clicar, usar o teclado, abrir o menu de contexto ou fechar/recriar o painel cancela a prévia e libera o recurso nativo.
- A reprodução não recebe foco nem cliques e fica na faixa de janelas normais, abaixo de todos os cartões e materiais do launcher. Uma borda de 2 pixels usa a cor de destaque. Não há hook global, captura periódica de tela ou serviço adicional. O temporizador só é armado durante o hover e para ao disparar ou cancelar.
- Aplica-se às janelas de aplicativos e à imagem da aba selecionada de cada janela do navegador. Não transforma abas em segundo plano em janelas independentes.
- O hover do navegador também é reconhecido pelo cartão, usando os limites físicos do popup para não depender de `IsMouseOver` sobre um HWND nativo. Abas sem imagem salva permanecem recolhidas.

Limitações: a reprodução depende do conteúdo que o DWM disponibiliza. Janelas minimizadas podem mostrar a última imagem; superfícies protegidas podem não fornecer conteúdo. Não equivale a ativar a janela real nem usa APIs privadas de Aero Peek.

Referências oficiais:
- https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowplacement
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-windowplacement

Revisão por leitura de código e diff. Compilação, testes e type check não executados nesta alteração, conforme a preferência do usuário. Composição, ordem visual e posição em múltiplos monitores ainda precisam de conferência em execução.
