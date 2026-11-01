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
