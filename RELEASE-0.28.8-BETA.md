# Orby Launcher 0.28.8 Beta

- Corrige o fechamento de Personalizar ao clicar na confirmação de atualização. O clique fora fica suspenso durante a confirmação, operações de atualização e diálogos modais da janela.
- Depois da autorização e validação do pacote, o auxiliar solicita ao Windows minimizar as janelas, antes de abrir o painel independente de progresso. Nenhum aplicativo é encerrado; as janelas podem ser restauradas normalmente. Se o Windows não permitir minimizar, a instalação continua.
- Preservados o atualizador com Acrylic, os seletores de horário, voltas do cronômetro e demais recursos anteriores.

Compilação Release e empacotamento concluídos. Não foram executados novos testes ou type check, conforme preferência do usuário. Confirmação, minimização e instalação ainda precisam de conferência em atualização real.

A correção da confirmação e o novo comportamento do auxiliar passam a valer depois de instalar esta versão. A atualização que chega à 0.28.8 ainda utiliza o auxiliar e a interface anteriores. Se o clique na confirmação impedir a atualização na versão antiga, use o instalador manual desta release; suas configurações são preservadas.

Instalador x64 com runtime incluído, PawnIO oficial opcional e sete idiomas. SHA-256 e assinatura RSA separada acompanham o pacote para o atualizador do Orby; não é certificado Authenticode e não remove avisos de editor desconhecido do Windows.