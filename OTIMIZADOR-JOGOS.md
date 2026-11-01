# Otimizador de Jogos

Na aba Otimizador, a interface oferece apenas Equilibrado, Desempenho, Economia, Otimizar e Voltar ao normal. Detalhes técnicos e seleção manual de processos foram removidos do painel. Equilibrado é a seleção inicial quando não existe preferência compatível.

Otimizar salva o modo e habilita o perfil para jogos reconhecidos pelo serviço existente. PowerGetActiveScheme/PowerSetActiveScheme aplicam o plano correspondente ao entrar no jogo e restauram o anterior ao sair. O registro optimizer-recovery.json permite recuperação após interrupção. Alterações externas no plano são respeitadas. Voltar ao normal desativa o perfil e solicita restauração.

O launcher preserva suas configurações, não fecha aplicativos automaticamente, não limpa working sets, não aumenta prioridades e não injeta código em jogos. Planos indisponíveis geram mensagem; Desempenho pode aumentar calor e consumo. Não há promessa de ganho de FPS.

Otimizar também solicita sugestões locais ao Ollama fora de jogos. As recomendações são texto e não executam comandos. Consulte IA-LOCAL-OTIMIZADOR.md.

Esta simplificação recebeu revisão por leitura de código. Compilação, testes e type check não foram executados nesta alteração.
