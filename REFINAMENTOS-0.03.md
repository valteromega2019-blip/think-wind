# Think Wind — v0.03

Alterações no código em `developer`, sobre a composição Acrylic existente. Versão técnica do projeto: `0.3.0-dev`. Sem dependências do Think Monitor.

## Layout e interação

- Barra inferior sem ScrollViewer. Ícones, padding e espaçamento diminuem proporcionalmente ao espaço útil em DIP. Abaixo do limite de legibilidade, os aplicativos são distribuídos em páginas com setas, sem barras de rolagem. Ícones preservam a proporção; a barra permanece centralizada e acompanha o conteúdo.
- A ordenação é global entre páginas. No modo de edição, soltar um ícone na seta permite movê-lo para a página vizinha; os menus de mover e as configurações continuam disponíveis.
- Ilha com os mesmos estados compacto, hover e painel completo. O painel completo mostra uma seção por página, com altura natural limitada a 64% da área útil e no máximo 520 DIP; largura expandida de 400 a 640 DIP, limitada também pelo monitor.
- Cabeçalho, atalhos e cartões se ajustam à área disponível. Em áreas úteis muito pequenas, os cartões são reduzidos proporcionalmente. Não há ScrollViewer na ilha, mini-ilhas ou barra; a rolagem continua disponível no formulário longo de configurações e nos seletores de dispositivos.
- As setas do painel também aceitam o arraste de módulos para reordenação. A lista de módulos nas configurações mantém a ordenação completa.
- Abrir a personalização não força a ilha a entrar no painel completo. Alterações de tema, cor, material, opacidade e raio atualizam a aparência sem reconstruir os cartões nem reposicionar as janelas. Mudanças de tamanho/ordem afetam somente o componente correspondente.
- Configurações com `SizeToContent.Manual`, tamanho independente dos overlays e salvamento automático preservado.

## Mídia e controles

- Cartão de mídia integrado a `GlobalSystemMediaTransportControlsSessionManager`: música, artista, aplicativo de origem, capa, estado de reprodução e comandos anterior/reproduzir/pausar/próxima.
- Posição e duração aparecem somente quando o player fornece uma linha do tempo válida; o arraste é habilitado somente quando ele aceita mudança de posição. Sem duração inventada ou estado de reprodução simulado.
- Informações acompanham os eventos da sessão. O progresso visível é atualizado a cada 500 ms; assinaturas e timer são suspensos com o painel fechado. Sem mídia ativa, a seção desaparece da navegação, permanecendo disponível no editor.
- Blocos arredondados para conexões e áudio; sliders com preenchimento na cor de destaque, thumb e suporte ao teclado. Wi-Fi, Bluetooth e modo avião abrem os controles nativos correspondentes.
- Brilho real via APIs DDC/CI, apenas quando o monitor selecionado anuncia suporte. Consulta e escrita ocorrem fora da thread visual, com uma operação por vez e leitura de confirmação. Monitores sem suporte oferecem acesso às configurações de Tela; não há escurecimento artificial da tela. Painéis internos de notebooks podem exigir outra API, ainda não implementada.

## Aplicativos em segundo plano

A listagem de processos e o atalho de tarefas foram retirados da ilha. O módulo utiliza UI Automation para os ícones expostos pela área de notificação do Explorer. Os ícones visíveis são obtidos por captura da região original do controle, quando disponível; não são extraídos da memória do Explorer e podem conservar o fundo da bandeja nativa.

O clique usa InvokePattern ou a ação padrão de acessibilidade. O botão direito solicita foco no controle original e o menu nativo por Shift+F10. Disponibilidade depende do provedor de acessibilidade e da versão do Explorer. Ícones ocultos só podem ser consultados quando o overflow nativo estiver aberto. Não há enumeração completa de todos os ícones ocultos nem garantia de menu para todos os aplicativos. O botão “Bandeja do Windows” mantém o acesso nativo por Win+B.

As consultas usam uma chamada pendente por vez, em thread de trabalho, para não bloquear a interface nem acumular chamadas se o Explorer estiver ocupado. A exibição usa páginas de até oito ícones.

## Estado da entrega

Revisão somente por leitura do código e dos diffs. **Testes, type check, build, publish e verificação visual em execução não foram realizados nesta atualização**, conforme a preferência permanente do usuário. Os resultados históricos da v0.21 não validam estas alterações. Nenhum instalador foi gerado; o executável local continua sendo o da compilação anterior.

Integrações novas de mídia, bandeja, brilho e comportamento em DPI/resoluções diferentes ainda precisam de validação em execução quando solicitada.

Referências de implementação: [sessões de mídia do Windows](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager), [UI Automation e threads](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-threading-issues), [capacidade de brilho DDC/CI](https://learn.microsoft.com/en-us/windows/win32/api/highlevelmonitorconfigurationapi/nf-highlevelmonitorconfigurationapi-getmonitorcapabilities).
