# Correção do recorte dos indicadores do Dock

A alteração anterior da borda de foco não corrigia a geometria interna dos aplicativos. CreateButton colocava o indicador em uma Grid com altura fixa e depois o deslocava 7 DIP para fora, sem reservar esse espaço na medida. Indicadores de até 26 DIP também podiam ser mais largos que ícones reduzidos pelo ajuste automático.

Agora a célula mantém a mesma altura total (ícone + 14 DIP), distribuída em linha do ícone, intervalo de 6 DIP, linha do indicador de 4 DIP e margem inferior de 4 DIP. Não usa TranslateTransform para posicionar indicadores. A largura dos indicadores de execução/arrasto fica limitada ao ícone atual. Fundo Acrylic, altura total da célula, ampliação e clique permanecem nos mecanismos existentes. A mudança não reconstrói a barra nem altera preferências.

Revisão por leitura/diff; não executados compilação, testes ou type check nesta solicitação. A correção precisa de conferência visual nos tamanhos configurados antes de considerar o relato encerrado.
