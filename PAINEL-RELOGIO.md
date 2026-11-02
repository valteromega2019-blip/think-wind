# Painel do relógio

A hora abre uma mini-ilha com relógio e data completos, abas Cronômetro e Temporizador e atalho ms-clock: para alarmes do Windows. O ícone de data permanece exclusivo para calendário. No modo barra os painéis ancoram no controle clicado.

Cronômetro usa Stopwatch, sem timer visual quando oculto. Temporizador de 1 a 1440 minutos usa tempo monotônico, pausa/continuação e cancelamento; enquanto ativo mantém um tick por segundo mesmo oculto para emitir aviso sonoro ao concluir, sem ativar janelas. Não é um alarme persistente: encerra com o launcher, não acorda o computador e não garante aviso no instante exato durante suspensão. O painel só atualiza texto quando visível; timers são descartados no fechamento definitivo. Estilos herdados da mini-ilha e recursos Think.

Arquivos: ClockPanel.cs, MiniIslandWindow.cs, IslandWindow.cs e IslandWindow.Adaptive.cs. Branch feature/data-na-barra. Revisão por leitura; compilação, testes e type check não executados nesta solicitação.

## Refinamento e durações rápidas

Branch feature/relogio-presets, criada com as alterações visuais pendentes preservadas. Blocos usam Think.GlassCard, cantos de 20 DIP e tipografia/ícones Segoe Fluent dos componentes existentes. Abas e presets selecionados usam Think.AccentSurface/Accent; números de cronômetro/temporizador seguem o destaque. Botões de iniciar/pausar/continuar, zerar, cancelar e alarmes recebem ícones.

Presets 5, 10, 15 e 30 minutos preenchem a duração e aguardam Iniciar; não substituem uma contagem ativa/pausada sem cancelamento. A entrada manual continua disponível. Nenhum timer adicional foi criado. Revisão por leitura/diff, sem compilação, testes ou type check nesta solicitação.

## Correção de resposta e dimensão

Cronômetro exibe centésimos e atualiza a cada 100 ms somente enquanto a aba está visível e contando. Iniciar, pausar e zerar atualizam imediatamente e ajustam o intervalo; ao ocultar retorna ao ritmo de um segundo se houver temporizador ativo, ou para o timer quando desnecessário. A contagem permanece baseada em Stopwatch, não em ticks.

As duas abas agora ocupam a mesma célula Grid, com a aba inativa Hidden em vez de Collapsed. Ambas participam da medição e reservam a maior dimensão, evitando encolhimento/expansão ao alternar. O texto de estado tem reserva mínima para mensagens. Não foram adicionados timers ou loops.

Revisão por leitura e diff; não executados compilação, testes ou type check nesta correção.

## Cidade do clima pela barra

O clique no clima abre uma mini-ilha ancorada no próprio botão, tanto na ilha quanto no modo barra. Reutiliza WeatherWidget em modo configuração: busca Open-Meteo, lista de cidades e salvamento de nome/coordenadas nas preferências existentes. Não usa localização automática nem cria novo serviço de rede. Selecionar resultado atualiza a configuração consumida pelo clima da barra/widget. A edição nas configurações continua disponível.

Busca é cancelada ao ocultar e recursos são descartados ao fechar a janela. A lista de cidades aberta conta como interação para impedir recolhimento. Revisão por leitura/diff; sem compilação, testes ou type check nesta solicitação.
