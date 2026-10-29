# Think Wind — v0.06 (0.6.0-dev)

## Estado da entrega

### Ampliação estilo Dock

- Aplicativos fixados e abertos recebem ampliação contínua por distância horizontal, com curva cossenoidal e mola amortecida. Iniciar, pesquisa, biblioteca e navegação permanecem estáveis em tamanho, acompanhando o deslocamento do conjunto.
- Personalização → Barra inferior: ativar/desativar, intensidade de 40% a 60% (padrão 50%), velocidade de 0,5× a 2× e zero a quatro vizinhos por lado (padrão dois).
- Um relógio `CompositionTarget.Rendering` governa toda a linha e para quando a mola estabiliza. Entrada/movimento/saída do cursor retomam o cálculo; não há timers permanentes por ícone nem animações individuais disparadas a cada hover.
- Escala ancorada à base dos botões e deslocamento acumulado conforme a largura ampliada, preservando intervalos e áreas reais de clique. Os indicadores acompanham seus botões.
- A janela reserva largura e altura adicionais no layout expandido, antecipadamente. Essa reserva pode alterar a altura normal da superfície Acrylic e a quantidade de aplicativos por página; não há redimensionamento nativo por movimento do mouse. Em espaço restrito a intensidade efetiva é limitada à reserva disponível, para evitar cortes.
- Recolhido continua sendo a faixa de 6 DIP na borda inferior. Ampliação é suspensa na edição visual, durante Drag & Drop de arquivos ou quando o Windows desativa animações. Atualizações periódicas da lista são adiadas enquanto o cursor está sobre os ícones, evitando reconstruções no meio da onda.
- Preferências salvas e aplicadas em tempo real. Velocidade ajusta somente a mola; intensidade/vizinhos/ativação recalculam o espaço reservado sem alterar a janela de configurações. Restaurar barra também restaura essas opções.

Implementação revisada somente por leitura de código/diff, sem compilação, testes, type check ou validação visual nesta etapa.

Implementação revisada por leitura de código e diff. **Testes, type check, compilação, execução e publish não foram realizados nesta atualização**, conforme a preferência do usuário. Nenhum instalador foi gerado. O executável existente continua sendo o da v0.05; este documento não atesta validação visual ou funcionamento em máquina real da v0.06.

## Bandeja e acessos

### Refinamento posterior — bandeja própria e barra exclusiva

- A área de segundo plano permanece na grade/mini-ilha do Think Wind. Removidos Win+B, foco na barra original e encaminhamento para a bandeja do Windows dos menus e mensagens deste componente. Falhas de integração são informadas no próprio painel.
- Atualização lê também os hosts de overflow existentes sem abri-los. Recuperação de imagem por renderização do host, uma vez por leitura, e cache de ícones reais. Quando o host não renderiza, captura da tela só é permitida com o ícone visível e sem outra janela cobrindo os pontos verificados. Não há ícones genéricos para simular aplicativos indisponíveis.
- Invocação pela acessibilidade; expansão do menu nativo somente quando o provedor expõe esse padrão. Não é garantido reproduzir menus de todos os aplicativos sem revelar a interface original.
- Nova opção em **Personalização → Sistema → Usar somente a barra do Think Wind (ocultar a barra do Windows)**, desativada por padrão e salva automaticamente. Oculta barras principal/secundárias sem encerrar Explorer nem alterar permanentemente a configuração da barra. A tecla Windows e a área de trabalho nativa são preservadas; não é um bloqueio do teclado/menu Iniciar do sistema.
- Antes de ocultar, tenta recuperar os ícones expostos em segundo plano. Um processo auxiliar do mesmo executável monitora o processo principal e restaura as janelas que ocultou ao sair/desativar ou detectar o encerramento do launcher. Nenhum script, tarefa agendada, serviço ou instalador é criado. Se o auxiliar também for encerrado à força, sua restauração não pode ser garantida.
- O espaço reservado pelo Windows para sua barra não é alterado nesta etapa. O auxiliar verifica as barras a cada 250 ms; o Explorer ainda pode exibi-las brevemente antes da próxima ocultação.

**Limite importante:** ícones escondidos que o Explorer não materializou não podem ser enumerados por UI Automation apenas por ocultar a barra. A grade mostra os ícones reais que o sistema disponibiliza; esta mudança não implementa um substituto completo do host de notificações do Explorer. Fonte: [Microsoft — enumeração de ícones ocultos](https://devblogs.microsoft.com/oldnewthing/20250929-00/?p=111637). As referências a Win+B nos itens históricos abaixo descrevem o comportamento anterior, substituído neste refinamento.

Revisão somente por leitura de código/diff. Não foram executados testes, type check, build, aplicativo, helper ou publish. O executável existente continua na v0.05; o recurso ainda requer compilação e validação autorizadas.

- Grade fixa de 6 colunas × 4 linhas, até 24 aplicativos por página, com ícone e nome reduzido. Paginação aparece somente quando necessária.
- Botão direito chama o menu nativo quando o Explorer o disponibiliza. Ctrl + botão direito oferece fixar/desafixar; Ctrl + F também alterna o favorito no item focado. Favoritos ficam antes dos demais.
- Ordenação considera frequência de ações realizadas pelo launcher e recência de uso/primeira observação. Não representa estatísticas globais de uso do Windows. Preferências e histórico limitado são salvos localmente.
- Atualização a cada cinco segundos enquanto a bandeja está visível, preservando controles existentes quando a ordem não muda e evitando reconstrução durante menus.
- A recuperação dos ícones continua pelo provedor de acessibilidade do Explorer e captura de ícones expostos. Não há garantia de enumerar ícones ocultos ou de manter a identidade quando o aplicativo altera seus metadados. Win+B e o menu da própria grade preservam acesso à bandeja original.
- Configurações do Windows ficam no cabeçalho, inclusive no compacto; configurações do Think Wind aparecem somente no painel completo, com símbolo de personalização distinto.
- O botão Iniciar da barra inferior usa o símbolo vetorial de quatro painéis do Windows, sem fundo individual, com cor de destaque e escala independente da fonte de ícones. Continua abrindo o Menu Iniciar próprio do launcher.

## Ilha, calendário e movimento

- Preservados os estados compacto, hover com mídia/volume e painel completo. Recolhimento configurável de 0 a 10 segundos ou desativado, mantendo o compacto na borda física superior, sem margem.
- Aproximação em até 8 DIP da área compacta aciona o hover, com pequeno atraso contra ativações involuntárias. Não há janela invisível adicional que capture cliques.
- Sliders, captura do mouse, menus, entrada de senha, operações Wi-Fi e painéis associados protegem a interação contra recolhimento. Retornar com o cursor cancela a espera.
- Calendário mensal já visível no painel completo, dia atual destacado e navegação nativa entre meses. Abrir o painel retorna ao mês atual; relógio e data acompanham o Windows.
- Três módulos iniciais destacados; blocos detalhados de calendário e bandeja têm espaço próprio. Limite do painel completo: 620 DIP ou 64% da altura do monitor, o menor. Conteúdo se ajusta proporcionalmente em telas menores, sem rolagem. Novo padrão de largura: 780 DIP; larguras anteriormente salvas são preservadas.
- Movimento da janela sincronizado com `CompositionTarget.Rendering`, somente durante transições. Geometria e recorte nativos são reaproveitados quando tamanho e raio não mudam; destinos iguais não reiniciam animação.
- Consultas de rede fora da thread visual. Mini-ilhas deixam de animar a opacidade da superfície Acrylic e usam pequeno deslocamento do conteúdo. O mecanismo de transparência por pixel e a janela de composição nativa foram preservados.
- Destaque com superfície suavemente tingida, tinta ajustada para tema claro/escuro e tratamento de alto contraste. Recursos dinâmicos aplicam as mudanças sem reinicializar a janela de configurações.

## Áudio do Windows

Seleção de saída passa a solicitar a troca do dispositivo padrão de sistema/multimídia, conferindo os dois papéis após a alteração. Em falha parcial, tenta restaurar os padrões anteriores e apresenta novamente o estado real. O papel de comunicações e roteamentos particulares de cada aplicativo não são modificados.

Volume e mute usam Core Audio; callbacks de dispositivos, padrão e volume atualizam os controles. As notificações são agrupadas e transferidas ao dispatcher visual; assinaturas são removidas quando os controles saem de uso. Leitura periódica permanece como recuperação. Microfone continua sendo seleção do endpoint a controlar, sem gravação nem mudança do padrão de captura.

**Limite de compatibilidade:** o Windows não oferece um setter global documentado em MMDevice. A troca usa a interface desktop interna `IPolicyConfig`, isolada em `Native/DefaultAudioPolicy.cs`, sujeita a mudanças do Windows. Não se presume sucesso: há leitura de confirmação, tentativa de reversão e mensagem de falha. Referência da definição: [IPolicyConfig](https://raw.githubusercontent.com/tartakynov/audioswitch/master/IPolicyConfig.h). Notificações documentadas: [IMMNotificationClient](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immnotificationclient).

## Wi-Fi nativo

Mini-ilha de rede com adaptadores, redes disponíveis, sinal, conexão atual, paginação compacta e controle de rádio quando suportado. Consultas são serializadas em worker, somente com o painel ativo; atualização a cada cinco segundos e solicitação de scan no máximo a cada trinta segundos, além da atualização manual.

- Conectar/desconectar/trocar rede pelas APIs WLAN; conexão é aguardada por notificações ACM e conferida no estado retornado antes de ser mostrada como concluída.
- Perfis já existentes do Windows são reutilizados sem ler suas senhas. Redes novas abertas, WPA2-Personal AES e WPA3-Personal AES aceitam perfil temporário; redes empresariais e demais mecanismos exigem perfil previamente configurado no Windows.
- Senha em `PasswordBox`/`SecureString`, sem persistência em preferências, arquivo ou log. XML temporário montado em buffers mutáveis e memória nativa limpos após a chamada. O Windows recebe a credencial para autenticação; o launcher não salva um perfil permanente.
- Controle de rádio verifica o estado efetivamente retornado; bloqueio físico/modo avião/política não é contornado.
- Permissões de localização e políticas recentes do Windows podem impedir scan/listagem/conexão. A interface mostra o erro; a autorização deve ser concedida pelo usuário no Windows. Cadastro manual de SSID oculto e novos perfis corporativos não fazem parte desta etapa.

Referências: [WlanGetAvailableNetworkList](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlangetavailablenetworklist), [WlanConnect](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanconnect), [permissões de localização para Wi-Fi](https://learn.microsoft.com/en-us/windows/win32/nativewifi/wi-fi-access-location-changes).

Integrações de áudio, rádio, credenciais, menus da bandeja, DPI, múltiplos monitores e fluidez visual ainda precisam de compilação e validação de execução autorizadas pelo usuário. Não há medição de desempenho desta etapa.
