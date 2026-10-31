# Arrastar e soltar e permissões

A interface que recebe OLE Drag & Drop deve executar no mesmo nível de permissões do Explorador. Nesta branch, escolher Executar como administrador para o Think Wind inicia apenas o bootstrap elevado; ele reabre a interface no contexto normal do desktop e encerra antes de criar janelas ou hooks.

Os sensores continuam no coletor protegido em Program Files, iniciado pela tarefa autorizada. Ações sobre janelas administrativas usam o auxiliar de uma operação já existente. A permissão desses auxiliares não é transferida para a barra que recebe arquivos.

O relançamento aceita somente o executável atual, a mesma conta e a mesma sessão. Não altera UAC, níveis de integridade, filtros de mensagens ou políticas. Se não existir um desktop compatível ou a API negar a operação, informa o erro; não reinicia repetidamente.

APIs oficiais: [CreateProcessWithTokenW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithtokenw), [CreateEnvironmentBlock](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-createenvironmentblock) e [GetShellWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getshellwindow).

Esta alteração ainda não foi compilada ou validada em execução. Arrastar do desktop normal diretamente para um aplicativo de destino elevado permanece sujeito à proteção do Windows.