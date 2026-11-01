# IA local do Otimizador

A interface usa o modelo predefinido qwen2.5:1.5b, sem campos técnicos. Ao clicar em Otimizar, inicia o Ollama instalado se necessário, consulta /api/tags e, se o modelo estiver ausente, solicita confirmação antes do download de aproximadamente 1 GB via /api/pull. Progresso aparece no próprio painel. Recusar mantém o perfil de energia disponível, sem análise com IA.

A inferência usa /api/generate em http://127.0.0.1:11434, sem proxy, redirects ou fallback externo. Usa uma amostra recente dos sensores, nomes de CPU/GPU e opções do perfil. Não envia arquivos, mensagens ou títulos de janelas. A resposta é apenas uma sugestão, sem execução de comandos ou fechamento de aplicativos.

Contexto e saída são limitados, duas threads são solicitadas e keep_alive é zero. Sair do painel ou entrar em jogo cancela a requisição; o cancelamento do cliente pode não interromper imediatamente o servidor. O serviço Ollama do usuário não é encerrado pelo launcher. Nenhuma análise automática é iniciada no boot.

Nesta tarefa, o Ollama 0.40.13 foi instalado via winget. Os pesos do modelo não foram baixados. O Orby não embute o runtime nem o modelo no instalador.

Arquivos principais: Services/LocalOptimizerAi.cs, Services/GameOptimizer.cs e Views/GameOptimizerPanel.cs. O antigo LocalOptimizerAiPanel permanece fora do painel simplificado.

Compilação, testes e type check não executados nesta alteração. Inferência e troca de plano ainda precisam de validação em execução.
