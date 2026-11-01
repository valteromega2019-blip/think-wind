# Otimizador de Jogos — primeira etapa

Branch feature/otimizador-jogos, criada da base atual sem alterar as branches anteriores. Em Desempenho, a nova aba Otimizador fica à esquerda de Componentes.

Implementado: painel com ativação opcional, escolha entre manter energia atual, Equilibrado ou Alto Desempenho, aplicação ao jogo reconhecido pelo serviço existente e restauração ao sair do jogo/encerrar o launcher. Usa PowerGetActiveScheme/PowerSetActiveScheme, sem criar planos. Estado anterior registrado antes da alteração em optimizer-recovery.json; próxima inicialização tenta recuperar uma sessão interrompida. Se o usuário mudar o plano por fora, não o sobrescreve ao restaurar. Desativado por padrão; não modifica o computador apenas por abrir o painel. Não muda configurações de barras, overlay ou sensores.

Fechamento de aplicativos: lista de janelas acionada manualmente, confirmação por janela e CloseMainWindow, sem TerminateProcess. Exclui algumas janelas de sistema e ferramentas de comunicação comuns, mas não classifica automaticamente qualquer programa como dispensável. Não fecha aplicativos ao detectar jogo, não reabre apps nem promete restaurar documentos. O usuário decide cada fechamento. Não limpa standby/working sets, não aumenta prioridade e não injeta código no jogo.

Ainda pendentes: provedor/API de IA, sugestões baseadas em análise, lista persistente de aplicativos autorizados e política de restauração/reabertura de apps. O painel informa que o perfil é local; nenhuma IA é simulada e nenhum dado é enviado para fora. Esta etapa não promete ganho de FPS nem redução garantida de latência. Alto desempenho pode aumentar consumo/calor e pode não existir em equipamentos com Modern Standby.

Não foram executados compilação, testes ou type check. Revisão por leitura e diff. Não alterado o plano de energia deste computador para validar. Recuperação após interrupção e mudança de plano durante jogo precisam de validação em execução quando solicitada.

Arquivos: Services/GameOptimizer.cs, Views/GameOptimizerPanel.cs, Views/HardwareMonitorPanel.cs, Services/SettingsStore.cs e App.xaml.cs.
