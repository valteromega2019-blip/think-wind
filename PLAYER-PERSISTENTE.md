# Cartão de música persistente

Base: tag publicada `v0.28.9-beta`. Branch: `feature/player-persistente`.

- Players instalados reconhecidos, incluindo Spotify e YouTube Music, podem apresentar seu cartão quando a sessão está pausada ou parada.
- Últimos metadados e capa ficam em cache de memória limitado a 16 origens. A informação reaparece enquanto o Windows ainda disponibiliza uma sessão compatível. Não há gravação de histórico de músicas em disco.
- Play usa o comando da sessão do Windows; não reinicia o aplicativo nem simula atalhos. Se o player encerra sua sessão ou desabilita Play, não é possível garantir retomada.
- Botão Abrir e clique na capa/informações restauram a janela do aplicativo. Os controles de minimizar e fechar são preservados.
- Botões −/+ alteram o volume do aplicativo em passos de 5 pontos percentuais. A correspondência exige uma sessão do mixer associada ao executável; não há alteração silenciosa do volume geral como alternativa. Players web podem compartilhar volume com o navegador.
- A consulta do mixer inclui sessões inativas para permitir ajuste durante pausa, e só ocorre enquanto o cartão está aberto, a cada 2 segundos ou por clique. O painel normal do mixer mantém sua filtragem anterior.
- Sem miniatura nativa na apresentação do cartão de música de players dedicados. Quando nenhuma sessão compatível existe, permanece a apresentação de janelas existente.
- Chamadas e vídeos de navegador mantêm a política anterior; o novo estado em pausa é restrito a identidades conhecidas de música instaladas.

Validação desta etapa: leitura de código e diff. Testes, type check e compilação não executados conforme preferência do usuário. A versão instalada publicada não foi substituída.

## Refinamento da abertura e barras

- A identificação da integração acontece antes de mostrar miniaturas nativas; players dedicados sem sessão disponível permanecem sem miniatura substituta.
- O cartão aguarda os metadados antes de aparecer e descarta aberturas que deixaram de corresponder ao ícone sob o cursor.
- Volume vertical compacto usando o controle do tema existente, com consultas apenas durante a apresentação e gravações agrupadas durante o arraste.
- Progresso da música permanece horizontal e menor, preservando busca e tempo. Os controles de abrir, minimizar e fechar permanecem.
- Esta revisão não foi compilada nem testada; o pedido anterior de compilação se referia à versão anterior.
