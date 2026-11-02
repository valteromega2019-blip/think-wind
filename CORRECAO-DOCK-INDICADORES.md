# Correção do recorte dos indicadores do Dock

A alteração anterior da borda de foco não corrigia a geometria interna dos aplicativos. CreateButton colocava o indicador em uma Grid com altura fixa e depois o deslocava 7 DIP para fora, sem reservar esse espaço na medida. Indicadores de até 26 DIP também podiam ser mais largos que ícones reduzidos pelo ajuste automático.

Agora a célula mantém a mesma altura total (ícone + 14 DIP), distribuída em linha do ícone, intervalo de 6 DIP, linha do indicador de 4 DIP e margem inferior de 4 DIP. Não usa TranslateTransform para posicionar indicadores. A largura dos indicadores de execução/arrasto fica limitada ao ícone atual. Fundo Acrylic, altura total da célula, ampliação e clique permanecem nos mecanismos existentes. A mudança não reconstrói a barra nem altera preferências.

Revisão por leitura/diff; não executados compilação, testes ou type check nesta solicitação. A correção precisa de conferência visual nos tamanhos configurados antes de considerar o relato encerrado.

## Bolinha de notificação

A bolinha ainda tinha `TranslateTransform(0, -3)`, desenhando fora da área medida e sofrendo recorte durante a ampliação. Agora ocupa uma linha superior própria, de 4 DIP, com círculo de 4 DIP na cor de destaque. Essa linha reutiliza os 4 DIP que antes eram padding superior do botão: a célula passa de ícone + 14 para ícone + 18 DIP, e o padding superior passa de 4 para 0. A altura total, a posição do ícone, a posição do indicador inferior e o tamanho do fundo permanecem iguais. Imagem e fallback ficam na linha 1; indicadores inferiores, na linha 3. A detecção e o estado das notificações não mudam.

Correção revisada por leitura/diff, sem nova compilação, testes ou type check nesta etapa.
