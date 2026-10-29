# Think Wind — Dynamic Island v0.04

Implementação sobre a v0.03, na branch `developer`. Versão técnica `0.4.0-dev`. A composição Acrylic, as janelas transparentes e os controles nativos existentes foram preservados.

## Distribuição dos módulos

O painel completo mostra os sete módulos juntos, sem navegação por páginas da ilha. Os três primeiros módulos visíveis na ordem personalizada ficam em destaque; os quatro restantes ficam abaixo, com títulos, espaçamentos e cartões menores. Ocultar e reorganizar módulos continua disponível no editor. Na ordem padrão, os principais são Conexões, Mídia e Áudio; os secundários são Monitoramento, Calendário, Aplicativos em segundo plano e Notificações.

A grade usa três colunas principais a partir de 520 DIP de área interna; abaixo disso, duas ou uma. A última linha incompleta ocupa a largura disponível. Os secundários usam quatro colunas a partir de 700 DIP e duas ou uma em áreas menores. A altura acompanha o conteúdo, limitada a 64% da área útil e no máximo 520 DIP. Em telas com pouco espaço, o ajuste proporcional preserva todo o conteúdo sem criar barras de rolagem, podendo reduzir o tamanho aparente dos controles. A largura expandida é configurável entre 400 e 900 DIP, limitada ao monitor.

O calendário apresenta um resumo compacto e abre o calendário WPF existente em uma mini-ilha independente. A bandeja mantém paginação interna de ícones quando necessário; isso não pagina os sete módulos. Permanecem os limites de acessibilidade do Explorer documentados na v0.03.

## Três estágios

1. **Compacto:** relógio, rede, volume, engrenagem discreta e botão de expansão completa.
2. **Hover:** mídia e volume em blocos lado a lado, atalhos opcionais e informação de rede. O mesmo cartão de mídia e o mesmo controle de saída são transferidos entre os estágios, preservando o dispositivo selecionado e a sessão de reprodução.
3. **Completo:** três módulos em destaque e quatro menores abaixo, respeitando as preferências de visibilidade e ordem.

A mídia consulta as sessões do Windows no hover e no painel completo, suspendendo assinaturas e timer no compacto. Sem mídia ativa, mostra uma indicação curta, sem comandos simulados. Os controles de áudio compactos mantêm volume e mudo reais e oferecem seleção do dispositivo pelo botão de reticências; a escolha continua selecionando o dispositivo a controlar, sem trocar o padrão do Windows.

A engrenagem de personalização fica sempre no cabeçalho da ilha, inclusive no compacto. O atalho duplicado da barra inferior e o acesso de personalização abaixo do cabeçalho foram removidos. O acesso pelo ícone do Think Wind na bandeja original do Windows foi mantido.

## Recolhimento automático

Em Personalização → Dynamic Island:

- ativar/desativar o recolhimento automático;
- escolher espera de **0 a 10 segundos**, com ajuste por teclado de 0,1 segundo;
- salvamento automático, sem reiniciar e sem reconstruir os cartões ao alterar o tempo.

O padrão permanece em 0,45 segundo para configurações existentes. A contagem considera a ilha e as mini-ilhas de volume, rede e calendário. Retornar com o cursor cancela a espera. Captura de mouse, arraste de layout, comandos de mídia pendentes, menus/seletores de áudio e interações por teclado suspendem o recolhimento. A contagem recomeça após terminar a interação, se o cursor continuar fora. A edição e a janela de personalização também suspendem o fechamento automático.

O valor zero usa o próximo ciclo de entrada da interface. O pequeno corredor entre a ilha e uma mini-ilha aberta é considerado durante a passagem do cursor, sem bloquear cliques de outras janelas. A desativação mantém os estágios abertos até fechamento explícito; Esc e os botões de fechar continuam funcionando. Mini-ilhas também preservam o fechamento nativo por perda de foco.

## Animações

O redimensionamento da ilha usa uma curva com aceleração e desaceleração. Retomar uma transição parte da geometria atual; pedir o mesmo destino não reinicia o movimento. A duração fica definida no início de cada transição para evitar saltos ao mudar a velocidade nas configurações.

Os cartões têm translações discretas entre posições antigas e novas, incluindo reordenação. As atualizações de tamanho do conteúdo são agrupadas no dispatcher, adiadas durante a interação com os controles e não reiniciam animações quando o destino do bloco não mudou. A consulta de rede do hover ocorre fora da thread visual, com uma operação pendente por vez. A configuração de animações do Windows e o valor zero de duração são respeitados. Não há animação da opacidade da janela Acrylic.

## Estado da entrega

Revisão por leitura de código e diff. **Testes, type check, build, publish e validação visual em execução não foram realizados nesta atualização**, conforme a preferência permanente do usuário. Nenhum instalador foi gerado. O executável existente permanece na v0.03 e não contém estas mudanças.

O resultado da compilação da v0.03 é histórico e não valida a v0.04. A nova grade, os tempos de recolhimento, o trânsito entre mini-ilhas e as transições em diferentes escalas de DPI ainda precisam de validação em execução quando solicitada.
