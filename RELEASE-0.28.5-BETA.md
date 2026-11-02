# Orby Launcher 0.28.5 Beta

- Janela auxiliar independente para a atualização, com tentativa de Acrylic nativo do Windows, cantos arredondados, cor de destaque e opacidade da ilha conforme as preferências salvas.
- Exibe preparação, instalação, abertura e conclusão, com ícones e barra de atividade. Não apresenta porcentagens simuladas: durante a instalação a barra é indeterminada; só conclui após a confirmação de abertura do launcher.
- Se o painel não abrir, mantém a janela padrão de progresso do instalador. Erros permanecem visíveis; sucesso fecha o painel após três segundos. Alto contraste e alternativa opaca são preservados.
- Personalização do overlay exibe valores demonstrativos de FPS, temperaturas, uso, memória e gráfico, identificados como “Prévia · dados simulados”. A amostra existe somente na renderização do editor e não entra no histórico ou na coleta. Ao fechar o editor, voltam as leituras reais.
- Preservadas as correções de instâncias, validação do pacote, configurações e recursos anteriores.

Compilação Release e empacotamento concluídos. Não foram executados testes ou type check separados, conforme preferência do usuário. O Acrylic depende do suporte e das preferências do Windows. O painel independente e o fluxo de instalação ainda precisam de conferência durante uma atualização real.

A nova interface do auxiliar passa a valer depois de instalar esta versão. A atualização que chega à 0.28.5 ainda usa o auxiliar da versão instalada anteriormente.

Instalador x64 com runtime e PawnIO oficial opcional, em sete idiomas. Interface do launcher em português. SHA-256 e assinatura RSA separada acompanham o pacote para o atualizador; não é certificado Authenticode e não remove avisos de editor desconhecido do Windows.