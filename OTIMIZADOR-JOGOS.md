# Otimizador de Jogos — perfis internos

O Otimizador usa somente recursos locais do Windows. A integração com IA e Ollama foi removida, incluindo seus painéis e serviço. Não há consultas HTTP, downloads de modelos ou inferência neste recurso. O Ollama instalado no computador não foi desinstalado nem tem seus processos encerrados pelo launcher.

Interface: Equilibrado, Desempenho, Economia, Otimizar e Voltar ao normal. Otimizar habilita o perfil selecionado para jogos reconhecidos pelo serviço existente. Também permite aplicar uma nova escolha durante um jogo. Antes da troca, tenta restaurar o plano anterior; se houver restauração pendente, mantém o erro visível e não substitui o registro de recuperação.

Os modos correspondem aos planos Equilibrado, Alto desempenho e Economia de energia do Windows, usando PowerGetActiveScheme/PowerSetActiveScheme. O plano anterior é registrado antes da alteração em optimizer-recovery.json e restaurado ao sair do jogo ou encerrar o launcher. Uma alteração externa de plano é preservada. Voltar ao normal desabilita o perfil. Planos ausentes ou recusados pelo equipamento são informados, sem simular aplicação.

Economia pode reduzir desempenho; Alto desempenho pode aumentar consumo e calor. Não há garantia de ganho de FPS. Não fecha aplicativos, não esvazia memória, não eleva prioridades e não modifica os recursos configurados de barras, overlay ou sensores. O Modo Jogo existente continua responsável pela redução de trabalho do launcher durante jogos, conforme sua configuração.

Alteração revisada por leitura de código e diff. Compilação, testes e type check não executados nesta solicitação.
