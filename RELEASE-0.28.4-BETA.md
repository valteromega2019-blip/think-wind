# Orby Launcher 0.28.4 Beta

- Atualizador detecta outra instancia do Orby antes de instalar e informa o conflito, sem encerrar processos a força.
- Após a instalação, abre o executável da pasta registrada pelo instalador, mesmo quando a atualização partiu de uma prévia em outra pasta.
- O launcher confirma sua inicialização ao auxiliar por um evento local exclusivo. A atualização só registra sucesso completo após essa confirmação; falhas exibem aviso e são registradas.
- Mensagem de instância duplicada explica que outra cópia pode estar aberta em outra pasta e retorna código de conflito.
- Preservados o progresso visível, validação do pacote, configurações e recursos anteriores.

Compilação Release e empacotamento concluídos. Não foram executados testes ou type check separados, conforme preferência do usuário. Transições entre cópias e atualização real ainda precisam de validação em uso. Não há encerramento forçado de outras cópias.

A correção do auxiliar só passa a valer depois de instalar esta versão. Quem ainda usa um auxiliar antigo pode observar o comportamento anterior durante a atualização para 0.28.4; a instalação manual recebe a correção diretamente.

Instalador x64 com runtime e PawnIO oficial opcional, em sete idiomas. Interface do launcher em português. SHA-256 e assinatura RSA separada acompanham o pacote para o atualizador; não é certificado Authenticode e não remove avisos de editor desconhecido do Windows.