# Barra superior fixa e escala para TVs

## Objetivo
Adicionar em Aparência uma opção de barra superior fixa de ponta a ponta e escala geral independente do Windows. Branch: feature/barra-superior-fixa-escala.

## Abordagem recomendada
Reutilizar IslandStatusWindow e StatusBarReservation para a barra fixa, em vez de criar outra superfície. Preservar a abertura dos painéis abaixo da barra, Acrylic, espelhamento e seleção de monitores. Ativar o modo fixa reserva a faixa superior; desativar restaura a preferência anterior de reserva. Tela cheia e Modo Jogo preservam as regras atuais de ocultação.

Escala geral entre 60% e 140%, padrão 100%, com restauração para 100%. Aplicar à interface do launcher (barras, Iniciar, painéis e Personalizar), mantendo as escalas específicas como ajustes relativos. Não alterar DPI, fontes ou preferências globais do Windows. Overlay mantém sua escala própria. Limitar dimensões pela área útil e atualizar âncoras, área de clique e recorte nativo junto ao conteúdo. Salvar automaticamente e aplicar sem reiniciar.

## Alternativas
Escalar apenas ícones seria mais simples, mas não resolve painéis grandes em TVs. Alterar a escala do Windows afeta outros programas e fica fora do escopo. A escala do Orby deve entrar nos cálculos de dimensão e composição, sem simplesmente comprimir o conteúdo dentro de janelas antigas.

## Restrições
Não adicionar polling, hooks ou serviços. Não reconstruir a arquitetura. Preservar todas as preferências existentes e defaults. Nenhum teste, type check, build, instalador ou publicação neste escopo sem pedido explícito. Revisão por leitura de código e diff.

## Aprovação
Aprovado pelo usuário em 23/11/2026. Incluído aviso central com slider ao detectar mudança do DPI do Windows, prévia ao vivo e Aplicar/Manter atual. Aviso adiado no Modo Jogo.