# Inicialização e permissões

- A interface usa prioridade AboveNormal apenas durante sua construção inicial, por no máximo cinco segundos. Ao terminar a abertura, volta imediatamente a Normal. Não utiliza High ou RealTime e não eleva a prioridade se um jogo for identificado na abertura.
- Essa prioridade influencia o agendamento da CPU durante a abertura; a ordem dos programas iniciados pelo Windows continua sob controle do sistema. A preferência de iniciar com o Windows é preservada.
- Após preparar a interface, uma única execução autorizada configura os auxiliares de sensores e de ações de janelas que estiverem habilitados e desatualizados. A confirmação inicial do UAC permanece necessária. Não são solicitadas permissões de funções desativadas.
- As tarefas usam executáveis em Program Files com ACL protegida. Em aberturas seguintes, tarefas já autorizadas são utilizadas sem novo UAC, até uma atualização do protocolo exigir nova autorização.
- A interface permanece com permissões normais, necessárias para receber arquivos arrastados do Explorer. Auxiliares elevados mantêm os papéis limitados existentes, com coleta e controle de janelas sob demanda. Não há aquecimento contínuo de sensores apenas para acelerar o início.
- A preparação não é exibida durante um jogo identificado na abertura. As opções manuais de autorização e revogação continuam em Personalizar → Sistema.

Alteração revisada por leitura de código e diff. Sem compilação, testes, type check ou medição de tempo nesta etapa. A melhora do tempo de abertura ainda precisa de medição prática.
