# Expansão lateral do fundo do Dock

Branch de teste experiment/atalhos-animacao-janelas. O DockMagnifier publica a largura extra efetivamente aplicada aos ícones após o limite de reserva; DockWindow usa esse mesmo valor para reduzir simetricamente as margens transparentes laterais do material.

A altura e a posição central permanecem fixas. A janela mantém a reserva máxima já calculada pelo layout e pela largura do monitor; não há novo timer, mola independente ou redimensionamento por posição do mouse. Reset/desativação do zoom retorna a expansão a zero. Modo barra fixa continua ocupando a largura da tela.

A animação experimental de janelas foi removida; o arraste de atalhos continua na mesma branch. Alteração revisada somente por leitura e diff. Não compilada nem validada visualmente. Testes/type check não executados conforme preferência. Nenhum instalador ou publicação.