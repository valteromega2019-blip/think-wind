# Indicador de atividade no Dock

Uma bolinha de 7 DIP aparece acima do ícone quando sua janela solicita atenção
ao Windows (`HSHELL_FLASH`). A cor usa o recurso dinâmico `Think.Accent`, incluindo
alterações de destaque em tempo real e o tema de alto contraste.

O indicador é removido quando a janela é ativada ou destruída. Aplicativos com
várias janelas mantêm a marca enquanto alguma delas ainda possui um alerta
pendente. O agrupamento entre fixados e abertos é preservado. Cada Dock recebe
os eventos da Shell, inclusive quando a interface está temporariamente oculta;
não é preciso expandir ou roubar o foco para registrar o alerta.

Não há nova consulta periódica, interceptação de entrada, injeção em aplicativos,
leitura de mensagens particulares ou animação contínua. A marca acompanha as
transformações do ícone sem modificar o tamanho do fundo Acrylic. Os eventos
atualizam apenas os indicadores; o registro é liberado ao encerrar o Dock.

Limites: a aplicação precisa solicitar atenção ao Windows. Uma nova aba,
mudança no título, notificação toast ou badge proprietário não necessariamente
produz esse evento. Alertas anteriores à abertura do Orby não são recuperados.
O recurso representa atividade pendente; não é uma contagem de mensagens não
lidas nem remove notificações dentro do aplicativo.

Arquivos: `src/ThinkWind/Views/DockWindow.Attention.cs` e `DockWindow.cs`.
Revisão por leitura de código e diff. Testes, type check e compilação não
executados nesta alteração, conforme preferência permanente do usuário.

API: [RegisterShellHookWindow e mensagens HSHELL](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registershellhookwindow).
