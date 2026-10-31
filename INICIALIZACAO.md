# Inicialização e permissões

- A interface usa prioridade AboveNormal apenas durante sua construção inicial, por no máximo cinco segundos. Ao terminar a abertura, volta imediatamente a Normal. Não utiliza High ou RealTime e não eleva a prioridade se um jogo for identificado na abertura.
- Essa prioridade influencia o agendamento da CPU durante a abertura; a ordem dos programas iniciados pelo Windows continua sob controle do sistema. A preferência de iniciar com o Windows é preservada.
- Após preparar a interface, uma única execução autorizada configura os auxiliares de sensores e de ações de janelas que estiverem habilitados e desatualizados. A confirmação inicial do UAC permanece necessária. Não são solicitadas permissões de funções desativadas.
- As tarefas usam executáveis em Program Files com ACL protegida. Em aberturas seguintes, tarefas já autorizadas são utilizadas sem novo UAC, até uma atualização do protocolo exigir nova autorização.
- A interface permanece com permissões normais, necessárias para receber arquivos arrastados do Explorer. Auxiliares elevados mantêm os papéis limitados existentes. A coleta começa na abertura e mantém a última leitura em cache, mesmo com o painel fechado, usando o intervalo existente de dois segundos. Em Modo Jogo, esse consumidor é suspenso e apenas o overlay ou outro consumidor visível mantém a coleta. O controle elevado de janelas continua sob demanda.
- Depois da autorização inicial, o coletor é reconectado imediatamente, sem esperar o intervalo de recuperação de uma tentativa anterior. As permissões continuam sendo concedidas pelo Windows; iniciar a coleta não substitui essa autorização.
- A preparação não é exibida durante um jogo identificado na abertura. As opções manuais de autorização e revogação continuam em Personalizar → Sistema.

Alteração revisada por leitura de código e diff. Sem compilação, testes, type check ou medição de tempo nesta etapa. A melhora do tempo de abertura ainda precisa de medição prática.
