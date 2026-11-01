# Orby Launcher 0.25.0 Beta

- Otimizador de Jogos sem IA: Economia, Equilibrado e Desempenho, com restauração do plano anterior ao sair do jogo.
- Análise local sob demanda de processos, energia e sensores recentes. Aplicativos fica em primeiro; Resumo e Processos separam recomendações de consumo informativo.
- Seletores e ícones do launcher, paginação e resultados com altura estável.
- Fechamento somente de itens selecionados e confirmados. Aplicativos com janela recebem fechamento normal. Alguns programas conhecidos de segundo plano podem ser encerrados diretamente após aviso explícito. Serviços e processos críticos são excluídos; dependências de terceiros exigem revisão do usuário.
- Verificação da saída do processo e atualização da lista após a ação. Nenhuma limpeza global de working sets/standby.
- Tutorial com duas etapas do otimizador após o monitoramento: abrir a área e realizar análise, sem fechar aplicativos.
- Busca de conexão com o coletor sem prazo total; watchdog da transmissão preservado, com reconexão automática.
- Reforço do bloqueio da barra original durante Alt+Tab e mudanças de foco/estado. Continua dependente da opção de substituir a barra.
- Refinamentos das transições e medidas da interface.

As preferências existentes são preservadas. O aplicativo não usa nem inicia Ollama; uma instalação independente dele no computador não é removida.

Distribuição x64 com runtime incluído e PawnIO oficial opcional. Instalador e executável Orby continuam sem assinatura digital; o atualizador integrado exige assinatura confiável e pode recusar a instalação automática desta distribuição. Use o instalador manual.

Compilação Release x64 e geração do instalador concluídas com sucesso. O SHA-256 publicado do instalador corresponde ao digest informado pelo GitHub. Não foram solicitados testes, lint ou type check adicionais nesta publicação; não foram executados. Fluxos interativos, compatibilidade entre jogos e dispositivos e ausência de quadros da barra original ainda dependem de validação em uso real.

