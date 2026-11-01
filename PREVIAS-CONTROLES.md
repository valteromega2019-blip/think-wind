# Controles das prévias integradas

As prévias de música e chamadas agora incluem minimizar e fechar a janela do
aplicativo, com os mesmos ícones e recursos de tema das miniaturas existentes.
Os controles de reprodução, microfone e saída da chamada continuam separados.

- Spotify e outros players: ações restritas às janelas associadas ao ícone.
- Discord, Tretalk e demais chamadas: alvo da chamada identificada.
- Meet e música no navegador: controles junto ao cartão integrado. Fechar
  solicita o fechamento da janela do navegador, incluindo suas abas; não é
  o comando de sair da chamada ou fechar somente uma aba.
- Mais de uma janela de player: menu para escolher o alvo, sem fechar todas.
- Sem janela disponível: ações desabilitadas, sem encerrar processos à força.

O fechamento usa WM_CLOSE. Minimizar preserva a execução do aplicativo.
O auxílio elevado existente continua disponível quando necessário. Menus e
ações pendentes mantêm a interação da prévia protegida do fechamento por hover.

Arquivos: `Views/WindowPreview.Actions.cs`, `WindowPreview.cs`,
`WindowPreview.Browser.cs`, `DockWindow.Previews.cs` (encaminhamento das janelas
do Spotify na versão já registrada). O rascunho de integração genérica de
players permanece preservado no workspace.

Revisão por leitura do código e diff. Testes, type check e compilação não foram
executados nesta alteração, conforme preferência permanente do usuário.
