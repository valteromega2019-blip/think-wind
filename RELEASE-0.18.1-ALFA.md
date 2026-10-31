# Think Wind 0.18 Alfa — revisão 1

- Seleção automática da GPU prioriza a placa dedicada, consultando a classificação DXCore do Windows em vez de ordenar apenas pelo fornecedor.
- Novo seletor GPU monitorada no painel, com seleção manual persistente para painel, gráficos, VRAM e overlay. Se a placa escolhida não estiver disponível, utiliza seleção automática.
- O coletor separado envia os sensores de cada GPU e exige atualização da cópia protegida pelo fluxo normal de autorização UAC.

A prévia foi compilada sem erros/avisos e conferida pelo usuário. Esta revisão foi empacotada sem novos testes ou type check separado. Os 47 testes da rodada anterior da 0.18 não validam especificamente a nova seleção de GPU. Identificação de adaptadores ainda depende dos dados fornecidos pelo driver e dos sensores disponíveis.

Instalador Windows x64 com runtime e PawnIO oficial opcional incluídos, preservando preferências. Driver compartilhado existente é mantido. Instalador Think Wind sem assinatura digital. SHA-256 publicado junto do instalador.