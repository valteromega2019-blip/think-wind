# Expansão lateral do fundo do Dock

Branch de teste experiment/atalhos-animacao-janelas. O DockMagnifier publica a largura extra efetivamente aplicada aos ícones após o limite de reserva; DockWindow usa esse mesmo valor para reduzir simetricamente as margens transparentes laterais do material.

A altura e a posição central permanecem fixas. A janela mantém a reserva máxima já calculada pelo layout e pela largura do monitor; não há novo timer, mola independente ou redimensionamento por posição do mouse. Reset/desativação do zoom retorna a expansão a zero. Modo barra fixa continua ocupando a largura da tela.

A animação experimental de janelas foi removida; o arraste de atalhos continua na mesma branch. Alteração revisada somente por leitura e diff. Não compilada nem validada visualmente. Testes/type check não executados conforme preferência. Nenhum instalador ou publicação.
Organização direta: arrastar um aplicativo do Dock reordena sem entrar no editor, com marcador e DockOrder persistido. Payload interno de reordenação fica em memória, distinto de CF_HDROP externo; soltar não abre/minimiza o aplicativo. Prévias do navegador: somente o cartão ativo (conteúdo real da janela) aciona o peek grande; cartões de abas em segundo plano mantêm apenas a imagem em miniatura. Estas últimas mudanças foram revisadas por leitura, sem testes/type check/build nesta etapa.
