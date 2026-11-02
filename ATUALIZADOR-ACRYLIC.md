# Janela do atualizador com Acrylic

Branch: feature/visual-atualizador.

UpdateProgress.ps1 é um processo WPF independente, iniciado em STA antes da confirmação que permite fechar o launcher. O script é copiado para a pasta da atualização para não depender dos arquivos substituídos. Ele não tem comandos de instalação nem recebe chaves privadas.

Material: DwmSetWindowAttribute (DWMWA_SYSTEMBACKDROP_TYPE=38, valor 3/Desktop Acrylic), DwmExtendFrameIntoClientArea e WindowChrome. Janela sem AllowsTransparency para permitir o backdrop nativo, cantos do DWM e superfície com opacidade da ilha e cor de fundo do usuário. Usa cor de destaque, contraste de texto e alternativa opaca/alto contraste quando necessário. O desfoque depende do suporte e das preferências do Windows.

A janela acompanha estados publicados pelo auxiliar em um arquivo local substituído atomicamente, consultado a cada 500 ms. A barra fica indeterminada durante tarefas sem porcentagem disponível; só atinge 100 após confirmação de abertura do launcher instalado. Não estima porcentagens do instalador por logs. Erros permanecem visíveis e permitem fechar; sucesso fecha a janela após três segundos. Fechar durante a instalação não interrompe o instalador. Se o auxiliar morrer, o painel informa a interrupção. Ao fechar, o painel remove seus arquivos temporários.

A prontidão do painel é confirmada antes de ocultar o instalador nativo. Se a janela não abrir em oito segundos, o instalador usa o progresso padrão visível da versão anterior. Integridade, assinatura, autorização explícita e registro final permanecem sob responsabilidade do auxiliar original.

Não foram executados testes, type check, compilação ou instalação nesta alteração. Acrylic, comportamento de DPI, fluxo de instalação e tratamento de falhas precisam de validação real antes de publicação.