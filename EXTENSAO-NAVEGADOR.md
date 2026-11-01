# Extensão de prévias de abas

Implementação opcional em `extensions/orby-tabs`, com Manifest V3, sem dependências npm. Chrome e Edge têm registro nativo específico; Brave, Chromium, Vivaldi e Opera recebem registros de compatibilidade e dependem do suporte do navegador às APIs Chromium. Firefox mantém a integração por acessibilidade, sem esta extensão.

## Conectar após compilar

1. Abra `chrome://extensions` (Chrome) ou `edge://extensions` (Edge).
2. Ative o modo de desenvolvedor, escolha Carregar sem compactação e selecione a pasta `BrowserExtension` da compilação. No código-fonte, ela está em `extensions/orby-tabs`.
3. Abra a extensão e copie o ID mostrado. Em Orby → Personalizar → Aplicativos, cole o ID e clique em Conectar ID da extensão.
4. Na extensão, configure os domínios excluídos e clique em Ativar prévias. O navegador pede autorização de acesso aos sites. Se não aceitar, a captura permanece desativada.
5. Visite as abas desejadas e aguarde a captura. Ao passar pelo navegador no Dock, até quatro abas aparecem no leque. Passe sobre um cartão para revelar sua última captura; clique para selecionar a aba.

Carregar a extensão e aceitar as permissões são ações do usuário no navegador. Esta implementação não força instalação, não altera políticas corporativas e não habilita depuração remota. Não foi instalada nesta tarefa.

## Como funciona

A extensão captura a aba visível por `chrome.tabs.captureVisibleTab` após ativação, carregamento ou mudança de foco. Nunca troca abas para capturá-las. As imagens são reduzidas a 320 × 180, JPEG, no máximo uma tentativa a cada 1,1 segundo; eventos rápidos são agrupados. Eventos de troca e navegação invalidam uma captura em andamento para evitar associar pixels a outra aba.

As capturas recentes são transmitidas por Native Messaging para `OrbyBrowserHost.exe`, um pequeno processo console que recebe uma mensagem e termina. Não há porta HTTP, servidor permanente, conexão externa, token do navegador ou scripts injetados nas páginas. O processo valida a origem aprovada, versão, sessão UUID, tamanho do pacote, número de abas e formato das imagens.

O projeto do launcher inclui a compilação do conector e copia os arquivos da extensão para a saída. Na publicação, o conector compartilha o runtime do launcher, inclusive para instalações self-contained. O conector é registrado em HKCU com origem exata, sem administrador. Conecte cada ID se usar mais de um navegador ou perfil. A configuração aceita até oito IDs.

O Orby combina as imagens com as abas acessíveis do navegador. A associação exige uma janela identificável e título de aba único; em caso de ambiguidade, mostra título e seleção sem inventar uma imagem. Uma captura em cache não é uma transmissão ao vivo; seu horário fica no tooltip. Sem captura válida, continua a apresentação nativa.

## Privacidade e recursos

- A captura começa desativada e depende da autorização na extensão.
- Janelas anônimas, páginas internas e domínios excluídos não são capturados.
- A autorização permite capturar páginas comuns visitadas, que podem conter informações pessoais. Cadastre exclusões antes de ativar.
- As imagens e títulos ficam somente neste computador, em `%LOCALAPPDATA%\OrbyLauncher\BrowserBridge\Snapshots`. URLs não são transmitidas ao conector. A extensão mantém URLs somente em armazenamento de sessão para invalidar imagens após navegação.
- Cache nativo limitado a 32 snapshots de até quatro abas, com tamanho de pacote inferior a 800 KB. Capturas com mais de 15 minutos são ignoradas; a limpeza ocorre na próxima leitura ou comunicação, sem temporizador permanente para isso.
- Desativar e apagar na extensão solicita limpeza da sessão. Se o conector estiver indisponível, use Apagar capturas locais no Orby. Desconectar remove os registros específicos desta integração e as capturas.
- Remover manualmente a extensão no navegador não executa limpeza no aplicativo; desconecte pelo Orby para removê-la imediatamente.
- Não muda a seleção de abas automaticamente, não lê senhas por API e não transmite dados à internet. Uma captura pode conter texto ou conteúdo sensível visível na página; não há filtragem automática desse conteúdo.

## Estado de validação

Somente revisão por leitura e diff. Não foram executados testes, type check, lint, compilação, instalador ou registro no PC. A publicação em lojas de extensões não foi realizada. Compatibilidade efetiva, cópia do host na compilação/publicação e fluxo de autorização ainda precisam ser conferidos quando o usuário solicitar.

Referências: [Chrome Native Messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging), [Chrome tabs](https://developer.chrome.com/docs/extensions/reference/api/tabs), [Edge Native Messaging](https://learn.microsoft.com/en-us/microsoft-edge/extensions-chromium/developer-guide/native-messaging).
