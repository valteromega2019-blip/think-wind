# Revisão gradual de desempenho e consistência — 05/11/2026

Base: Orby 0.29.1 Beta (`b14d793`). Branch: `perf/revisao-geral-orby`.

## Alterações implementadas

### Inicialização

- `App.xaml.cs` / `App.Monitors.cs`: removida a primeira família Dock/ilha que era construída apenas para ser descartada por `ConfigureMonitors`. A criação inicial e as reconstruções usam o mesmo caminho.
- Recuperação do otimizador, aquecimento dos sensores, Wi-Fi e preparação do início automático passam pelo Dispatcher em `ContextIdle`, depois do trabalho de layout/render de maior prioridade. A coleta habilitada continua começando na abertura do app, sem exigir abrir o monitor.
- O otimizador não aplica um novo perfil antes de recuperar eventual sessão anterior. Jogos já abertos continuam sendo considerados antes de mostrar as janelas.
- `StartupRegistration.cs`: tarefa de logon por usuário, sem atraso configurado, token interativo limitado, prioridade normal (4 no Scheduler), sem limite de execução e permitida em bateria. Não há serviço novo, senha, elevação da interface ou mudança global de atraso do Windows.
- Se o Agendador recusar a criação, permanece a alternativa HKCU Run. Desativação remove Run mesmo quando a remoção da tarefa falha; essa falha é relatada. O instalador tem limpeza da tarefa na desinstalação.
- Migração única de perfis existentes é restrita ao executável registrado como instalado; uma prévia com perfil existente não substitui o início da instalação. `--startup` evita aviso de cópia duplicada durante logon.
- O guard espera a barra nativa existir para guardar seu estado original. Ausência temporária do Explorer não é interpretada como preferência original igual a zero.

O Windows não oferece garantia de que um programa de usuário seja o primeiro depois dos componentes essenciais. Referências: [Run/RunOnce](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys), [gatilho de logon](https://learn.microsoft.com/en-us/windows/win32/taskschd/logontrigger-delay), [prioridade do Scheduler](https://learn.microsoft.com/en-us/windows/win32/taskschd/taskschedulerschema-priority-settingstype-element). Nenhum registro/tarefa do computador foi alterado nesta revisão de código.

### Segundo plano, múltiplos monitores e jogos

- Um `ApplicationCatalog`, worker STA e conjunto de observadores pertencem ao App, compartilhados pelos Docks. Fechar/recriar um Dock remove sua assinatura e não destrói o catálogo de outras telas.
- Suspensão para jogo cancela varreduras do catálogo, inclusive tarefas enfileiradas e publicação de resultados antigos. Cancelamento é cooperativo entre entradas: não interrompe uma chamada COM individual já bloqueada.
- Eventos simultâneos dos observadores são reunidos em uma entrada de Dispatcher antes do debounce existente.
- `StaWorker` cancela trabalhos ainda na fila ao descartar, converte `OperationCanceledException` em tarefa cancelada e libera a coleção ao sair da thread. Não força encerramento de threads.
- Alterações que não mudam consumidores não acordam os sensores novamente. Preferências sem mudança nas opções do Modo Jogo não reconfiguram sua leitura.
- Mantidos hooks/atalhos existentes, proteção de jogos, sensores autorizados, continuidade do overlay e restauração da barra nativa.

### Mídia, abas, ícones e animação

- `MediaService` reaproveita metadados/classificação das sessões até os eventos invalidarem o cache. Um evento durante `await` invalida também a resposta em andamento. A seleção e a apresentação reutilizam a mesma consulta; capas adicionais não são guardadas por sessão.
- `MediaCard.Volume` trata `AudioUnavailableException`, inclusive nos handlers assíncronos, mantendo volume indisponível em vez de deixar a exceção escapar.
- `TrayIconCatalog` decodifica snapshots no tamanho útil do ícone de 28 DIP, considerando o maior DPI conectado, sem ampliar fontes menores nem deformar a proporção. Expiração e invalidação por DPI permanecem.
- `BrowserTabs` e `BrowserTabImages` compartilham busca limitada à árvore de controles, sem descer nos documentos das páginas: até 4.096 nós/24 níveis. Mantidos cache de abas minimizadas, seleção explícita e dupla conferência de identidade antes/depois de capturar imagem.
- `DockMagnifier` reutiliza a lista de ícones de trabalho e substitui LINQ por laços no frame; campo de distância, mola, duração e transformações permanecem iguais.

### Acrylic e layout

- `PopupBackdrop`: chamadas duplicadas de abertura reutilizam o host do mesmo `HwndSource`, sem destruir/recriar a composição recém-aberta.
- Fallback opaco usa valor corrente preservando a origem da propriedade. Fechar ou recuperar o material invalida esse override para voltar ao fundo definido pelo tema/binding, inclusive após alto contraste.
- `ThemeService`: `TintOpacity` entra na chave do cache, pois o recurso `Think.Popup` depende desse valor quando a opacidade da ilha não está definida.
- `MiniIslandWindow`: conteúdo estável/âncora estável não provoca nova medição a cada segundo. Eventos de layout e abertura continuam dimensionando; busca de clima conserva a atualização periódica por ter resultados assíncronos sem evento próprio de layout.
- Não foram redesenhados controles nem alteradas as cores, fontes, transparência ou preferências do usuário.

## Comparação estrutural por leitura

Estas são contagens dedutíveis do código, não medições de consumo:

| Trabalho | Antes | Depois |
|---|---|---|
| Família temporária de Dock/ilha descartada no início | 1 | 0 |
| Catálogos/workers de aplicativos ativos em N monitores | N | 1 |
| Array temporário de ícones por frame de magnificação | 1 | 0 após aquecimento da lista |
| Metadados do player a cada atualização de posição | Consulta repetida | Cache até evento invalidante |
| Busca de abas | Descendentes da janela inteira | Árvore limitada, sem documentos |

Não foram medidos CPU, GPU, RAM, FPS, input lag ou tempo de logon. Nenhuma porcentagem de melhoria é afirmada.

## Revisão e limites

- Inspeção do código e diff; auditorias independentes de materiais e recursos e revisão independente do conjunto.
- Dois problemas da primeira versão das mudanças foram detectados por leitura e corrigidos: propagação de cancelamento no Dock e limpeza do fallback Run. A revisão também preservou a atualização de tamanho do clima.
- Testes, type check, lint, build, execução, benchmark e inspeção visual **não executados**, conforme a preferência permanente do usuário. Nenhum novo instalador, instalação, merge ou publicação.
- A migração do início automático, desinstalação, abertura em jogo, múltiplos monitores, troca de temas/alto contraste/DPI e APIs de players/navegadores ainda precisam de validação em execução quando solicitada.
- Provedores UIA podem bloquear numa chamada individual; a busca agora é limitada, mas não introduz timeout/encerramento forçado. Árvores extraordinariamente grandes podem retornar abas parciais.
- Players que alteram metadados sem os eventos do Windows podem manter dados até o próximo evento invalidante; não foi acrescentado polling para compensar um provedor irregular.
- A revisão não certifica ausência de todos os bugs do aplicativo. Recursos existentes e dependências necessárias foram preservados; não houve remoção especulativa de código funcional.
