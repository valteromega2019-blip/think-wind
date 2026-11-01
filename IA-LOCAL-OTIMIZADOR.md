# Sugestões com modelo local

Branch feature/otimizador-jogos. Integração real com a API local do Ollama em `http://127.0.0.1:11434`, sem fallback para IA externa e sem download automático. Requer instalar/configurar Ollama e um modelo de inferência local; nenhum modelo foi instalado nesta solicitação. O Orby não embute os pesos de um modelo nesta etapa.

No Otimizador, Localizar modelos instalados consulta /api/tags. A lista exclui nomes com cloud e entradas com remote_host. Analisar com IA local faz uma única requisição /api/generate somente ao clicar, com stream false, contexto/saída limitados, duas threads solicitadas e keep_alive 0 para solicitar descarregamento após a resposta. O backend pode ter seus próprios limites/comportamentos; o Orby não encerra o serviço Ollama do usuário. Proxy e redirects HTTP são desativados. O serviço local precisa ser confiável e configurado pelo usuário para inferência offline; um serviço de terceiros em loopback ainda é responsável pelo próprio processamento.

Envia apenas uma amostra recente do hardware, nomes de CPU/GPU, temperaturas, uso CPU/GPU/RAM e opções do otimizador. Não envia títulos de janela, conteúdo de mensagens, arquivos, nomes de usuário ou lista de processos. Não grava histórico de recomendações. Cancelamento disponível; sair da aba cancela a solicitação. Entrada em jogo reconhecido cancela a requisição em andamento e impede nova análise. Cancelar o cliente não garante interrupção instantânea da computação do servidor. Timeout de 90 segundos na geração, quatro segundos na descoberta e limite de resposta HTTP. Não há consulta automática ou serviço novo no boot.

As recomendações são texto sem execução de comandos, URLs, ferramentas ou fechamento de apps. Prompt delimita ações conservadoras e exige separar hipóteses de dados observados. O modelo ainda pode errar: o texto é identificado como sugestão e não é convertido em ações. Ajustes de energia e fechamento de janelas continuam nos controles existentes com autorização do usuário. Não se apresenta um motor de regras como IA.

Arquivos: Services/LocalOptimizerAi.cs, Views/LocalOptimizerAiPanel.cs, Services/GameOptimizer.cs e Views/GameOptimizerPanel.cs.

Compilação, testes e type check não executados, conforme preferência permanente. Não foi feita consulta a um modelo real nesta etapa nem alterado plano de energia. Funcionamento ponta a ponta depende de um modelo instalado e precisa ser validado após compilação solicitada.

Referências oficiais: https://docs.ollama.com/api/generate e https://docs.ollama.com/api/tags.
