# Tutorial interativo

Branch: `feature/tutorial-interativo`.

Três módulos e oito etapas reconhecem ações reais da interface:

1. Barra de aplicativos: aproximar o mouse, abrir Iniciar e digitar uma pesquisa.
2. Central de controle: aproximar o mouse no topo e abrir o painel completo.
3. Monitoramento e personalização: abrir monitoramento, personalizar e selecionar Aparência.

O contorno pulsante identifica o controle real. A confirmação permanece por 1,1 segundo antes do avanço. Há Voltar, Pular etapa e Sair. A animação respeita a preferência de movimento do Windows. Controles ocultos podem ser pulados, sem modificar as preferências do usuário.

O guia aparece automaticamente apenas para perfis novos. A configuração executada pelo instalador registra a pendência antes de salvar as preferências iniciais. Perfis existentes não são tratados como primeira instalação após uma atualização. Em Personalizar, a última aba é Tutorial, com Repetir tutorial e Continuar tutorial interrompido.

O progresso fica em `tutorial.json`, ao lado de `settings.json`, com gravação temporária e substituição. As etapas não modificam volume, rede, temas, overlay ou permissões. O guia apenas explica esses recursos e observa as ações solicitadas.

Não há hooks globais, captura ou movimentação automática do cursor. Os destaques não recebem cliques. A localização do alvo é consultada somente durante o guia, para acompanhar reconstruções dos controles e mudanças de monitor. No Modo Jogo, a janela, os destaques e os dois temporizadores do tutorial são suspensos. Encerrar ou pular o guia libera as proteções temporárias de recolhimento.

Revisão por leitura de código e diff. Compilação, testes, type check, instalador e publicação não executados nesta alteração. A aparência, o posicionamento em DPI misto e o fluxo interativo ainda precisam de validação em execução quando solicitada.
