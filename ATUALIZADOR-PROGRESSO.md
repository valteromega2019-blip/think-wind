# Progresso visível do atualizador

Branch: feature/progresso-atualizador.

O auxiliar iniciava o instalador com /VERYSILENT e WindowStyle Hidden. Depois do encerramento do launcher, nenhuma janela informava que a atualização continuava.

O auxiliar agora usa /SILENT e WindowStyle Normal: o instalador pula as perguntas do assistente, mas mantém a janela de progresso e os estados reais da instalação visíveis. O processo auxiliar continua aguardando sua conclusão antes de reabrir o Orby. A confirmação de prontidão, as verificações de integridade/publicador, o registro de falhas, /NORESTART e a preservação das configurações permanecem no fluxo existente.

Atualizações com /ORBYUPDATE=1 recebem título e explicação próprios e uma mensagem de finalização durante a preparação dos componentes de sensores. As mensagens seguem os sete idiomas do instalador. Instalações manuais mantêm o fluxo normal.

Não foi implementada porcentagem simulada: a barra é a do instalador e representa sua etapa de instalação, não o download ou o tempo total. A autorização administrativa do Windows, quando necessária, continua visível.

Limitação de distribuição: a seleção do modo visível fica no auxiliar da versão instalada. Quem ainda usa um auxiliar antigo pode ter uma última atualização invisível ao instalar a primeira versão com esta correção; as seguintes usarão o progresso visível.

Revisão por leitura de código e diff. Não foram executados testes, type check, compilação ou instalação nesta alteração, conforme preferência do usuário.