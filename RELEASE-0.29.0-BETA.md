# Orby Launcher 0.29.0 Beta

- Player persistente para aplicativos de música compatíveis: mantém a última faixa disponível ao pausar, com reprodução, progresso e volume individual quando o Windows oferece esses controles.
- Prévias do navegador organizadas por janela, com janelas lado a lado, até seis cartões de abas empilhados e contador somente das abas não exibidas. Abas sem imagem salva permanecem recolhidas.
- Cache em memória da última imagem de abas visitadas, sem extensão, gravação em disco ou ativação da aba no hover. O cache é limitado e descartado ao encerrar o app; as imagens não são capturas ao vivo das abas em segundo plano.
- Prévias individuais com Acrylic, sombra e controles de janela, sem bloco externo. Corrigidos recorte, fundo do player, seleção por posição do mouse e destaques em cartões sobrepostos.
- Prévia temporária da janela no desktop ao permanecer sobre sua miniatura: não dá foco nem restaura a janela real; sair remove a reprodução e clicar mantém a ativação normal. O conteúdo depende do que o DWM disponibiliza, especialmente em janelas minimizadas ou protegidas.
- Clique repetido no ícone de um aplicativo em primeiro plano minimiza todas as janelas do grupo.
- Menos trabalho no cache: transferência direta de pixels, brushes compartilhadas e liberação dos controles, menus, animações e recursos nativos ao fechar as prévias.
- A prévia grande passa a excluir a região atrás do Dock para preservar o fundo capturado pelo Acrylic.

## Validação e limitações

Antes do último ajuste de recorte do fundo: 61 checks gerais e 10 do atualizador aprovados, zero falhas; compilação Release sem avisos ou erros. A consulta NuGet de dependências diretas e transitivas não apontou vulnerabilidades conhecidas. O ajuste final do fundo foi compilado posteriormente, sem uma nova execução da suíte. Nesta etapa de empacotamento não foram executados novos testes ou type check separados.

Não há nova medição de FPS ou input lag em jogos. Conteúdo protegido, tela cheia exclusiva e combinações de DPI/monitores exigem conferência em uso real. As amostras curtas de CPU/RAM constam em `VALIDACAO-PLAYER-2026-11-04.md` e não comprovam um ganho sustentado.

Instalador Windows x64, com runtime, configurações preservadas, seleção inicial de idioma e PawnIO oficial opcional. A interface do launcher permanece em português. SHA-256 e assinatura RSA separada acompanham o instalador para o atualizador integrado. Essa assinatura não é certificado Authenticode e não elimina os avisos de editor desconhecido do Windows.
