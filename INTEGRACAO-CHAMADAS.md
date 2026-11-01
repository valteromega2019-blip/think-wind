# Prévias de chamadas

Implementação na branch `feature/tutorial-interativo`, conforme escolha do usuário.

Ao passar o mouse por um aplicativo elegível, a miniatura normal é apresentada primeiro. O launcher procura uma chamada em segundo plano e troca a prévia por um cartão de controles somente quando encontra, simultaneamente, botões acessíveis de microfone e saída da chamada.

## Aplicativos candidatos

- Discord, Discord PTB e Canary.
- Tretalk desktop (`Tretalk.exe`): microfone ligado/desligado/silenciado, som ligado/desligado e Sair da voz, pelos controles reais de acessibilidade.
- Google Meet em Chrome, Edge, Firefox ou Brave, com endereço `meet.google.com` verificável pela acessibilidade. O título anterior da janela não impede reconhecer uma mudança para a aba do Meet.
- Microsoft Teams e Zoom, quando seus controles usam os nomes acessíveis reconhecidos.

Os rótulos reconhecidos são em português e inglês. Isso representa suporte condicional, não uma declaração de compatibilidade validada com todas as versões. A detecção no Meet depende da aba ativa e da exposição da barra de endereços. Janelas minimizadas, versões diferentes, idiomas não reconhecidos, interfaces sem acessibilidade e restrições de integridade podem impedir a detecção. Nesses casos, permanece a prévia comum. Não há tentativa de contornar permissões nem ativação de depuração remota do navegador.

## Controles

- Microfone da chamada: aciona o botão real disponível no aplicativo.
- Áudio da chamada: exibido no Discord somente quando Deafen/Undeafen ou equivalente é encontrado.
- Sair: aciona o controle nativo, preservando eventuais confirmações do próprio aplicativo. Não encerra processos ou fecha o navegador como substituto.
- Abrir chamada: solicita a ativação da janela pelo mecanismo existente.
- Silenciar saída padrão: controla o endpoint padrão do Windows, identificado no tooltip.
- Silenciar mic + áudio padrão: guarda os estados anteriores dos dois endpoints controlados e permite restaurá-los explicitamente. Não significa silenciar todos os dispositivos conectados ou mudar a seleção de áudio interna do aplicativo. Fechar a prévia não remove o mute aplicado.

As ações da chamada exibem os nomes acessíveis originais e não apresentam estados otimistas de sucesso. Depois de uma ação, os controles são redescobertos. O processo proprietário da janela é conferido e o botão alvo deve ter um único nome exato, estar habilitado e visível.

No Discord, estados Toggle/Pressed/Checked de acessibilidade têm prioridade sobre rótulos estáticos como Mute. Mudo usa ícone riscado e vermelho. A âncora da prévia permanece no ícone original enquanto aberta, mesmo que a mudança de foco reconstrua o Dock. A permanência usa a posição física do cursor; uma reconstrução dos botões não é tratada como saída do mouse. Leituras temporariamente vazias não substituem o cartão sob o cursor, e botões inalterados não são reconstruídos a cada consulta. Mensagens de estado usam uma linha com tooltip para não deslocar o painel ao clicar.

## Recursos e permissões

UI Automation (`AutomationElement`, `TreeWalker`, `InvokePattern`, `TogglePattern`) executa fora do Dispatcher, sem hooks ou injeção de atalhos. A descoberta tem limite de nós e orçamento cooperativo de tempo. Chamadas individuais a provedores UIA podem ultrapassar esse orçamento; o semáforo global impede consultas concorrentes. Cancelamento evita reaproveitar resultados obsoletos.

A consulta é iniciada por hover e renovada a cada dois segundos apenas enquanto o cartão está aberto. Ao fechar a prévia, esconder o Dock ou entrar no Modo Jogo, os temporizadores param e a consulta recebe cancelamento. As integrações não criam serviço, subprocesso ou monitoramento permanente.

Configuração: Personalizar → Aplicativos → Controles de chamadas nas miniaturas. Depende também da opção de miniaturas estar ativa.

Na prévia lateral do Meet, os comandos usam o mesmo espaço de uma miniatura (320 × 160 DIP). As opções adicionais de áudio do Windows ficam no menu de três pontos, que mantém a interação ativa enquanto aberto. A integração depende apenas da acessibilidade nativa; a extensão de navegador e seu conector foram removidos.

## Referências e limites

- [Discord OAuth2](https://github.com/discord/discord-api-docs/blob/main/developers/topics/oauth2.mdx): os escopos RPC de voz exigem aprovação. Não foi implementado login, coleta de tokens ou uso de endpoints privados.
- [Microsoft InvokePattern](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.invokepattern.invoke): a execução depende da implementação de acessibilidade do aplicativo.
- [Meet Media API](https://developers.google.com/workspace/meet/media-api/guides/overview): esta implementação não utiliza acesso ao conteúdo da reunião.

Revisado por leitura de código e diff. Não foram executados testes, type check, compilação ou instalação. O funcionamento real no Discord, Meet, Teams e Zoom ainda requer validação solicitada pelo usuário; não foi iniciada ou encerrada nenhuma chamada durante o desenvolvimento.

### Identificação do Tretalk

Os rótulos foram identificados por leitura estática do pacote instalado `@tretalkdesktop/resources/app.asar`, no renderer `out/renderer/assets/index-DsBjVQj4.js`. O pacote usa `aria-label` com o estado atual, incluindo microfone bloqueado/silenciado, e o microfone pressionado significa ligado (não mudo). Por isso, o adaptador usa esses rótulos em vez de aplicar a convenção de toggle do Discord.

A análise REA foi parcial, interrompida pelo prazo por arquivo; os rótulos específicos foram confirmados por leitura estática do arquivo no ASAR, sem executá-lo. SHA-256 do pacote observado: `70951f2fa2c79a375db1187664b165939299d4762c06d4659521b1494356fc3c`. A integração do Tretalk ainda não foi validada em uma chamada. Não altera o aplicativo instalado, não lê tokens nem usa APIs privadas ou atalhos globais.
