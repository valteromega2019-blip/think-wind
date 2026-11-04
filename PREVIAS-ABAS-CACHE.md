# Imagens das abas em segundo plano

Branch: `feature/player-persistente`.

- A observação existente do navegador captura a página selecionada após uma mudança de foco ou título. Não há um novo temporizador de captura.
- A captura verifica a janela em primeiro plano, o processo, a identidade e seleção da aba e pontos de cobertura da página antes e depois da leitura da tela.
- Somente imagens reduzidas de até 384 × 216 pixels ficam no cache em memória, limitado a 24 entradas. Nada é salvo em disco ou enviado à rede.
- A identidade inclui janela, processo, identificador de automação e título, ignorando o sufixo de consumo de memória do navegador.
- Ao passar o mouse, uma aba com imagem armazenada mostra essa imagem estática. A aba selecionada usa a prévia nativa da janela. O clique continua abrindo a aba real.
- Abas nunca visitadas nesta execução, páginas sem área acessível e capturas recusadas não recebem uma imagem inventada. O cache desaparece ao encerrar o launcher. Mudanças de página com título idêntico podem não provocar uma nova captura.

Revisão de código e diff apenas. Compilação, testes e type check não executados nesta alteração, conforme a preferência do usuário. A captura e a apresentação ainda precisam de conferência em execução.
