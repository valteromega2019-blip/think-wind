# Orby Launcher 0.22 Beta

## Novidades e correções

- Aparência organizada em Tema e cores, Superfícies e transparência, Tipografia e Animações.
- Fonte pesquisável, cor HEX/RGB, contraste automático, prévia e salvamento. Padrão Segoe UI Variable, sem mudar fontes do Windows.
- Acrylic com a mesma política de opacidade nos temas claro e escuro; menus e seletores com acabamento e rolagem padronizados.
- Recorte personalizado do Mica alinhado ao raio das barras, sem um segundo arredondamento fixo do DWM.
- Ampliação magnética dos ícones superiores, com destaque de cor ao passar o mouse, mantendo o fundo e os botões fixos.
- Indicador de atenção acima dos ícones do Dock, na cor de destaque, quando o app emite o sinal nativo correspondente.
- Prévias ajustadas à proporção das janelas, com margem inferior e lateral consistente.
- Botões de minimizar e fechar integrados ao topo dos cartões de mídia e chamadas.
- Controle de câmera nas chamadas que expõem uma ação reconhecida, com estado ligado/desligado quando disponível.
- Sessões de música compatíveis com os controles de mídia do Windows, vinculadas ao aplicativo da prévia.
- Reunião confirmada tem prioridade sobre música, com alternância Reunião/Música.
- Meet identificado por origem e controles reais, sem assumir chamada pelo nome da aba. Uma chamada já reconhecida pode manter uma referência da aba: ao clicar no comando, a aba pode ser ativada e a chamada revalidada, sem extensão.
- Tutorial inicia na área de trabalho, minimizando as janelas sem fechar os aplicativos.

## Limites

As integrações dependem das APIs de mídia e da acessibilidade expostas pelos aplicativos. Navegadores podem ocultar documentos ao minimizar ou trocar de aba. Nesses casos a prévia mostra **Verificar chamada**, sem afirmar o estado atual, e precisa de uma chamada identificada anteriormente. A aba é selecionada somente após clique explícito e o comando é validado novamente. Não há extensão do navegador.

O sinal de atenção do Dock não representa todas as notificações nem uma contagem de mensagens. Abertura de aba, toast e badges próprios nem sempre geram o sinal do Windows.

O contraste é calculado pelas tonalidades configuradas; fundos reais muito transparentes precisam de conferência visual. Alto contraste e ausência de suporte nativo usam fallback sólido. Mica é um material do sistema, com comportamento diferente do desfoque ao vivo do Acrylic.

## Preparação da entrega

Compilação e empacotamento solicitados pelo usuário. Não foram executados novos testes, lint ou type check separados para esta publicação, conforme preferência permanente. Os ajustes finais ainda precisam de validação em chamadas reais e nos diferentes DPIs/monitores. Não se declara validação universal dos sensores, latência em jogo ou integrações de terceiros.

Entrega para Windows 11 x64, com runtime incluído e PawnIO oficial opcional para sensores. A interface roda com permissões normais; permissões administrativas dos auxiliares continuam separadas. Configurações existentes do Orby são preservadas. O instalador não possui assinatura digital.

Arquivos da release:

- `OrbyLauncher-Setup-0.22.0-beta.exe`
- `OrbyLauncher-Setup-0.22.0-beta.exe.sha256`
