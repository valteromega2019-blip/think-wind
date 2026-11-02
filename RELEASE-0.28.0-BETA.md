# Orby Launcher 0.28.0 Beta

## Novidades

- Alarmes integrados ao relógio: nome, horário, repetição semanal, sons do sistema e notificações com Adiar/Dispensar. Salvos entre aberturas; exigem o Orby aberto e não despertam o computador. Novos avisos aguardam o retorno do Modo Jogo/tela cheia.
- Widget de monitoramento com fontes maiores e até três blocos escolhidos pelo usuário. A personalização aparece abaixo do interruptor do widget, somente enquanto habilitado.
- Seletor de até quatro unidades locais acima dos blocos de armazenamento, sincronizado entre resumo e widget. A seleção é salva; rede e mídia óptica não são sondadas.
- Histórico selecionável no site, com notas das releases anteriores e uma área separada de planejamento.
- Instalador com escolha inicial de idioma: português do Brasil, português de Portugal, inglês, espanhol, francês, alemão e italiano. O idioma escolhido aplica-se ao assistente; a interface do launcher continua em português.

## Refinamentos e correções

- Cronômetro, temporizador e alarmes compartilham as dimensões da janela, com ícones, transições e interruptores compactos sem ON/OFF; ícones existentes permanecem no círculo escurecido, seguindo as cores do tema.
- Corrigido fechamento de Personalizar causado pelo construtor do novo interruptor no template WPF.
- Margens do relógio equilibradas e janela do calendário ajustada à largura do conteúdo.
- Ícones dos painéis internos permanecem destacados enquanto o painel está visível; o zoom continua restrito ao hover.
- Esc recolhe o painel expandido após abertura por clique e fecha as janelas de personalização com foco. Sem interceptação global de Esc de outros aplicativos.
- Indicador de notificação do Dock em espaço medido próprio, sem deslocamento negativo. Altura total da barra e posições dos ícones preservadas.
- Reforçada a recuperação da ocultação da barra do Windows após troca de área de trabalho/UAC. Auxiliar autorizado assume a tecla Windows isolada em janelas administrativas, mantendo a interface em permissões normais. A revisão da autorização pode pedir consentimento para atualizar a cópia protegida.

## Validação e limites

Compilação Release sem erros/avisos. Suíte geral: **58 aprovados, zero falhas, zero ignorados**. Atualizador: **10 aprovados, zero falhas**, incluindo consulta real à API sem download. Quatro verificações novas cobrem instanciação/renderização do interruptor, preferências do monitoramento, persistência dos alarmes e seletor de discos.

Não são testes comparativos de latência/FPS em jogos nem certificação de compatibilidade universal. Retorno de UAC, janelas administrativas, monitores/DPI físicos variados, suspensão prolongada e comportamento visual completo ainda dependem de conferência em uso real. A tela protegida do Windows não é modificada. O seletor de idiomas será exibido na instalação interativa; atualizações silenciosas não interrompem para perguntar idioma.

Instalador x64 com runtime incluído e PawnIO oficial opcional. Preferências e aplicativos fixados existentes são preservados. Os alarmes usam arquivo próprio no perfil do usuário.

Pacote acompanhado por SHA-256 e assinatura RSA `.sig` para autenticação no atualizador do Orby. Essa assinatura não é certificado Authenticode e não remove avisos de editor desconhecido/SmartScreen do Windows.
