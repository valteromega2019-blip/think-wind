# Acesso rápido de brilho e posição do tema

## Proposta
Adicionar Brilho da tela ao catálogo de ícones configuráveis da central, inicialmente nos acessos adicionais da expansão. Seguir as mesmas opções de mostrar na barra principal, mover para a área secundária, ocultar nas duas áreas e reordenar; mesmos efeitos magnéticos, hover/ativo e animação. Ao clicar, abrir mini painel independente com Acrylic e o BrightnessControl existente, respeitando o monitor correspondente e sem expandir todo o painel.

O slider continua usando as leituras/gravações reais WMI de notebook ou DDC/CI quando suportado. Sem simular brilho por escurecimento. Enquanto detectar suporte, usar estado explícito de carregamento e dimensões previstas; sem suporte, não oferecer slider falso e preservar acesso às configurações de Tela do Windows. Durante arraste/interação, impedir recolhimento; Esc/clique fora/troca de painel seguem o fluxo atual. Sem serviço ou monitoramento permanente novo.

Mover o botão claro/escuro da linha secundária para a linha de ferramentas do painel expandido, ao lado de Editar e dos três pontinhos, sem duplicação e mantendo a sincronização com o tema do Windows já implementada. A mudança vale para apresentação em ilha e em barra.

## Alternativas
Reusar o controle real e a infraestrutura de mini painéis evita um segundo controlador de brilho ou abertura direta obrigatória das configurações do Windows. Manter o controle já existente no módulo Conexões preserva o recurso atual.

## Validação
Implementar na branch escolhida pelo usuário. Revisão de código/diff; sem testes, type check, build, instalador, publicação ou mudança real de brilho sem solicitação explícita. Testes com falha continuam bloqueando entregas.