# Orby Launcher 0.31.2 Beta

- Biblioteca: fecha ao clicar fora e recolhe após aproximadamente 650 ms com o mouse fora da Biblioteca e do Dock. O foco residual da pesquisa não impede mais o clique externo. Menus, seletores, arrastes e edição em lote ficam protegidos do recolhimento por saída do mouse.
- Atualizar e desligar: deixa de solicitar desligamento imediatamente ao detectar um pacote baixado. Abre um auxiliar com acompanhamento e permissão administrativa para instalar as atualizações de software já baixadas pelo Windows, conferindo o resultado antes de desligar.
- Falhas, instalação concorrente, atualizações que exigem interação/licença ou reinicialização impedem o desligamento automático. Cancelar a autorização administrativa mantém o PC ligado. Quando for necessário reiniciar, a janela informa que o usuário deve salvar o trabalho e concluir pelo Windows Update; não reinicia silenciosamente.

Mantidas as correções da hora e da ilha da 0.31.1 Beta, a agenda experimental e os demais recursos existentes. A integração abandonada com Google Home e Playwright não está incluída.

## Compilação e limites

Compilação Release e instalador Windows x64. Não foram executados novos testes, lint ou type check, conforme a preferência do usuário. A instalação real de atualizações do Windows e o desligamento não foram acionados durante a preparação. O comportamento com os pacotes pendentes e com os controles da Biblioteca ainda precisa ser conferido em uso real.

Configurações preservadas. Instalador com sete idiomas e PawnIO oficial opcional. Incluídos SHA-256 e assinatura RSA separada para o atualizador integrado. Essa assinatura não é Authenticode e não elimina avisos do SmartScreen.
