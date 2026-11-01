# Bloqueio da barra original nas transições de jogos

Revisão: WindowsTaskbar.RunGuard já permanecia ativo durante jogos, mas escutava apenas EVENT_OBJECT_SHOW e usava uma varredura de recuperação a cada 250 ms. Alterações de estado/posicionamento da janela existente e transições de primeiro plano não tinham reação específica. Isso deixa uma hipótese plausível para reaparecimentos breves; não houve reprodução ou medição em jogo nesta tarefa.

Agora o mesmo auxiliar reage também a EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_SWITCHSTART/END e EVENT_OBJECT_STATECHANGE/LOCATIONCHANGE. Eventos de estado/localização de HWNDs não registrados são descartados imediatamente, evitando trabalho de consulta para janelas dos jogos. Eventos de exibição continuam descobrindo barras novas, inclusive após reinício do Explorer. Supressão usa SetWindowPos com SWP_HIDEWINDOW/NOACTIVATE/NOMOVE/NOSIZE/NOZORDER, com ShowWindow como fallback.

Sem novos hooks de teclado/mouse, sem mudanças de foco, sem acelerar o timer e sem alterações permanentes no Explorer. A opção existente Usar somente a barra do Orby Launcher continua controlando o recurso. O auxiliar restaura os estados originais ao desativar a opção ou detectar saída do launcher, inclusive falha da interface. Eventos são assíncronos; ausência absoluta de frames visíveis precisa ser avaliada no Windows real, não pode ser garantida apenas pela leitura.

Arquivo alterado: src/ThinkWind/Services/WindowsTaskbar.cs. Revisão por leitura/diff; compilação, testes e type check não executados conforme preferência permanente.

APIs oficiais: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook e https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
