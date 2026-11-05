# Teste: atalhos e animação de janelas

Branch experiment/atalhos-animacao-janelas, criada a partir de 020444b com todas as funcionalidades anteriores preservadas.

Arrastar um aplicativo da Biblioteca: gera um .lnk temporário para o Explorador copiar ao desktop; apps Store usam shell:AppsFolder. Soltar na barra do Orby fixa o aplicativo diretamente, evitando duplicação por destino. O arquivo temporário é removido após o arraste; nenhum executável é movido.

Animação experimental: malha de 64 faixas, topo abre primeiro e base acompanha, inversa ao minimizar pelo Dock. Imagem temporária desenhada pelo próprio HWND via WM_PRINT, nunca salva. Não captura a tela. Apps que não fornecem imagem utilizável ficam sem efeito; não há fallback para print do desktop. Camada sem foco e com passagem de cliques; não altera limites, estilo ou visibilidade da janela real. O efeito de abertura aparece depois que a janela está pronta: não substitui a animação nativa do Windows, e o conteúdo real pode ser visto atrás. Não há garantia de efeito em apps protegidos, de renderização especial ou com demora superior a um segundo no primeiro lançamento.

Somente cliques no Dock acionam o protótipo; não intercepta minimizar pelo próprio aplicativo. Modo Jogo cancela/suspende o efeito, tela cheia é excluída, movimento reduzido respeitado. Snapshot limitado em dimensões e descartado ao terminar; sem polling permanente para o efeito.

Ainda não compilado nem validado visualmente. Testes e type check não executados conforme preferência. Nenhum instalador, instalação ou publicação.
Refinamento: origem no centro do ícone, área visual inclui ícone e janela mesmo desalinhados horizontalmente, curva temporal suave e geometria idêntica percorrida ao contrário na minimização. Captura dirigida tem espera limitada a 80 ms; navegadores com renderização exclusivamente por GPU podem não responder. Ainda não validado em execução.
