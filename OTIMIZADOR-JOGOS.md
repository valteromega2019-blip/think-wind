# Otimizador de Jogos — perfis internos

O Otimizador usa somente recursos locais do Windows. A integração com IA e Ollama foi removida, incluindo seus painéis e serviço. Não há consultas HTTP, downloads de modelos ou inferência neste recurso. O Ollama instalado no computador não foi desinstalado nem tem seus processos encerrados pelo launcher.

Interface: Equilibrado, Desempenho, Economia, Otimizar e Voltar ao normal. Otimizar habilita o perfil selecionado para jogos reconhecidos pelo serviço existente. Também permite aplicar uma nova escolha durante um jogo. Antes da troca, tenta restaurar o plano anterior; se houver restauração pendente, mantém o erro visível e não substitui o registro de recuperação.

Os modos correspondem aos planos Equilibrado, Alto desempenho e Economia de energia do Windows, usando PowerGetActiveScheme/PowerSetActiveScheme. O plano anterior é registrado antes da alteração em optimizer-recovery.json e restaurado ao sair do jogo ou encerrar o launcher. Uma alteração externa de plano é preservada. Voltar ao normal desabilita o perfil. Planos ausentes ou recusados pelo equipamento são informados, sem simular aplicação.

Economia pode reduzir desempenho; Alto desempenho pode aumentar consumo e calor. Não há garantia de ganho de FPS. Não fecha aplicativos, não esvazia memória, não eleva prioridades e não modifica os recursos configurados de barras, overlay ou sensores. O Modo Jogo existente continua responsável pela redução de trabalho do launcher durante jogos, conforme sua configuração.

Alteração revisada por leitura de código e diff. Compilação, testes e type check não executados nesta solicitação.

## Análise local de processos e configurações

O botão Analisar processos e configurações executa uma coleta sob demanda em Task.Run. Compara TotalProcessorTime entre duas leituras separadas por pelo menos 1,2 segundo; normaliza CPU pelos processadores lógicos e mede WorkingSet64. Descarta processos reiniciados durante a amostra e informa leituras indisponíveis. Não mantém polling após terminar nem grava títulos, caminhos ou lista de processos em disco.

Exibe seis processos de maior consumo observado e até cinco nomes para revisão manual, com limite de 2% de CPU total ou 512 MB de working set por processo. Essa seleção é heurística, não uma lista de processos desnecessários. Exclui candidatos de sessão zero, pasta do Windows, primeiro plano capturado, próprio launcher e alguns launchers/comunicadores conhecidos; essas exclusões não garantem identificar todos os componentes necessários. Nenhum processo é encerrado. Memória compartilhada não é tratada como recuperação garantida de RAM.

A análise mostra o plano ativo real do Windows, preferências do otimizador e Modo Jogo do launcher. Aproveita sensores existentes de até 30 segundos para apontar RAM acima de 85%, unidade com menos de 10% livre, CPU acima de 90% e GPU acima de 95%. Mostra temperaturas sem inventar um limite universal. Não diagnostica temperaturas sem dados recentes nem altera configurações. Oferece sugestões manuais, não promete melhoria de FPS.

Sair da área cancela a coleta; a entrada em jogo também solicita cancelamento. Uma leitura de API em andamento pode terminar antes de observar o cancelamento. Resultados são uma amostra curta e podem variar.

Arquivos: Services/OptimizerAnalysis.cs, Services/GameOptimizer.cs e Views/GameOptimizerPanel.cs. Revisão por leitura; não executados testes, type check ou compilação nesta solicitação.

Referência: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.totalprocessortime?view=net-10.0

## Fechamento aprovado

Após a análise, aplicativos elegíveis aparecem em caixas de seleção inicialmente desmarcadas. Fechar selecionados apresenta nomes/PIDs e pede confirmação explícita, com orientação para salvar documentos. Não há autorização persistente nem fechamento automático no início de jogos.

Somente processos com janela principal, na sessão do launcher, fora do primeiro plano e da pasta do Windows, não associados a serviços Win32_Service, não marcados críticos por IsProcessCritical e com leitura de identidade disponível podem entrar na lista de ações. Exclui também Orby/Think, alguns launchers, comunicadores, nomes comuns de segurança/anticheat e caminhos steamapps/WindowsApps. Se a consulta de serviços falhar, não oferece candidatos. A lista de maior consumo é informativa e pode mostrar processos excluídos das ações.

Antes de cada solicitação, reconsulta serviços/proteções e compara PID, início do processo e executável, para rejeitar resultados antigos. Usa CloseMainWindow em tarefa de fundo, sem Kill, TerminateProcess, encerramento de árvore ou elevação. Solicitação aceita não significa processo encerrado: o aplicativo pode mostrar um diálogo de salvamento, ocultar na bandeja ou recusar. O resultado informa isso. Algumas dependências essenciais de terceiros não são identificáveis por estes filtros; a seleção exige revisão do usuário.

Arquivos: Services/OptimizerProcessCloser.cs, Services/OptimizerAnalysis.cs e Views/GameOptimizerPanel.cs. Revisão por leitura/diff; sem compilação, testes ou type check e sem fechar aplicativos reais nesta tarefa.

API de proteção: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-isprocesscritical

## Ajuste de apresentação do Otimizador

O relatório e os aplicativos deixaram de ser empilhados em uma lista extensa. Resumo, Processos e Aplicativos usam a mesma área estável de resultados. Aplicativos têm quatro itens por página, seleção preservada entre páginas e contador no botão de fechamento. Textos longos e fontes maiores ficam em áreas com rolagem interna limitada, sem expandir o painel indefinidamente. Os botões usam OutlineButton e o bloco usa CardBorder/recursos Think do monitor original.

A guarda que preserva o painel durante leituras de sensores agora permite invalidar sua medida quando uma atualização explícita de layout ou personalização ocorre. As leituras periódicas continuam sem reconstruir o painel. Nenhuma alteração nos filtros de proteção ou no mecanismo de fechamento.

Revisão por leitura e diff; sem execução, compilação, testes ou type check nesta solicitação. A apresentação ainda precisa de conferência em execução nas escalas configuradas pelo usuário.

## Seleção e clareza das ações

O Otimizador reutiliza SelectionCircleStyle nos modos (seleção exclusiva) e nos aplicativos (aprovação individual inicialmente desmarcada). Botões e abas recebem glifos Segoe Fluent usados pelo launcher. Cores permanecem nos recursos Think, sem cores fixas novas.

Processos agora informa explicitamente que consumo não equivale a permissão de fechamento, incluindo orientação para a aba Aplicativos. Cada aplicativo elegível mostra Fechamento permitido após aprovação; a área lembra que o usuário deve confirmar que não está usando o programa. O contador de selecionados e a confirmação existente foram preservados. Nenhuma alteração nos filtros ou encerramento efetivo.

Revisão por leitura e diff. Não executados compilação, testes ou type check nesta solicitação; ícones e apresentação ainda precisam de conferência em execução.
