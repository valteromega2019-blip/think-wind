# Painel do relógio

A hora abre uma mini-ilha com relógio e data completos, abas Cronômetro e Temporizador e atalho ms-clock: para alarmes do Windows. O ícone de data permanece exclusivo para calendário. No modo barra os painéis ancoram no controle clicado.

Cronômetro usa Stopwatch, sem timer visual quando oculto. Temporizador de 1 a 1440 minutos usa tempo monotônico, pausa/continuação e cancelamento; enquanto ativo mantém um tick por segundo mesmo oculto para emitir aviso sonoro ao concluir, sem ativar janelas. Não é um alarme persistente: encerra com o launcher, não acorda o computador e não garante aviso no instante exato durante suspensão. O painel só atualiza texto quando visível; timers são descartados no fechamento definitivo. Estilos herdados da mini-ilha e recursos Think.

Arquivos: ClockPanel.cs, MiniIslandWindow.cs, IslandWindow.cs e IslandWindow.Adaptive.cs. Branch feature/data-na-barra. Revisão por leitura; compilação, testes e type check não executados nesta solicitação.

## Refinamento e durações rápidas

Branch feature/relogio-presets, criada com as alterações visuais pendentes preservadas. Blocos usam Think.GlassCard, cantos de 20 DIP e tipografia/ícones Segoe Fluent dos componentes existentes. Abas e presets selecionados usam Think.AccentSurface/Accent; números de cronômetro/temporizador seguem o destaque. Botões de iniciar/pausar/continuar, zerar, cancelar e alarmes recebem ícones.

Presets 5, 10, 15 e 30 minutos preenchem a duração e aguardam Iniciar; não substituem uma contagem ativa/pausada sem cancelamento. A entrada manual continua disponível. Nenhum timer adicional foi criado. Revisão por leitura/diff, sem compilação, testes ou type check nesta solicitação.
