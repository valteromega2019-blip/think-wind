# Iconografia unificada do Orby

Branch `feature/iconografia-propria`, reaproveitada a pedido do usuário. O cadastro
opcional foi revertido; o serviço online não foi publicado, e não houve coleta.

## Uso

Personalizar → Aparência → **Experimental · ícones unificados**.
Desligada por padrão, preservando os ícones originais. A escolha é salva nas
preferências e afeta os ícones carregados imediatamente, sem reiniciar.

- Mesmo fundo arredondado, na cor de destaque escolhida, e símbolo branco.
- Aplicado ao Dock, Fixados, Biblioteca, Recentes, Todos, resultados da pesquisa,
  atalhos favoritos e bandeja de aplicativos em segundo plano.
- As imagens originais permanecem disponíveis para retornar ao modo original.
- Não muda atalhos, identidade dos aplicativos, arquivos, ícones do sistema ou
  imagens de capas/miniaturas. Os controles do launcher mantêm seus ícones atuais.

## Personalização por aplicativo

No botão direito de um aplicativo no Dock ou nas gavetas (Fixados, Biblioteca,
Recentes e Todos), use **Personalizar ícone…**. O seletor aceita PNG/ICO de até
2 MB, com transparência e dimensões de até 1024 × 1024 pixels. A cópia é recortada
ao símbolo, reduzida para até 256 pixels e convertida para branco, preservando o
alfa. O original não é modificado, e o desenho é colocado sobre a cor de destaque.

O arquivo normalizado fica incorporado às preferências, sem referência ao caminho
original; assim, acompanha o backup das configurações. São permitidos até 64
registros, com orçamento total de aproximadamente 1 MB para as imagens codificadas.
A escolha usa AppUserModelID e/ou destino resolvido do atalho. **Restaurar ícone
padrão** remove essa escolha e retorna ao catálogo unificado ou ao ícone original,
conforme a opção global.

A personalização individual é explícita: funciona mesmo com o modo unificado
global desligado, sem ativá-lo para os outros aplicativos. Por padrão não há
ícones personalizados. O arquivo é processado em tarefa de fundo após a seleção;
os controles existentes permanecem protegidos contra fechamento por clique fora
enquanto o seletor está aberto. Esta adição foi revisada por leitura e diff, ainda
sem nova compilação, testes ou type check. Os cinco testes citados abaixo são da
implementação anterior e não validam a importação personalizada.

## Base de símbolos

Importação fixada em Simple Icons 16.38.0: 3.472 símbolos de marcas. Não significa
3.472 aplicativos instalados ou todos os aplicativos existentes. O pacote de origem
teve sua integridade SHA-512 conferida antes da extração. Nenhum script npm foi
executado nem uma dependência de runtime JavaScript foi adicionada.

Aliases reconhecem aplicativos de design, arquitetura, desenvolvimento, mídia,
comunicação e jogos. Os símbolos que não existirem no catálogo recebem monogramas;
configurações, pastas e alguns utilitários usam os glifos nativos do Windows. Os
demais aplicativos usam iniciais. Não se promete um logotipo específico para todo
programa. A correspondência não usa nomes de pastas ancestrais, evitando classificar
todos os jogos instalados no Steam como o próprio Steam.

O acabamento é do Orby; os logotipos de terceiros não são marcas de propriedade do
Orby. Licença, aviso de marcas e metadados de origem acompanham o projeto em
`licenses/SimpleIcons-*`. Os ícones da base não foram redesenhados individualmente.

## Recursos e validação

Vetores locais, sem downloads durante o uso, timers, hooks ou novas consultas ao
sistema. O catálogo só é lido ao usar o modo unificado. Geometrias e resoluções têm
caches limitados, e os controles removem suas assinaturas ao sair da tela.

Validação autorizada em 17/11/2026: compilação Release sem erros nem avisos e cinco
testes focados aprovados. Cobertura: padrão desligado e perfis anteriores,
persistência em arquivo temporário, compatibilidade dos 3.472 vetores com WPF,
renderização de símbolos e monogramas em quatro escalas DPI, mudança de cor e
retorno à imagem original. Uma falha inicial da captura off-screen foi corrigida
processando UpdateLayout antes de capturar a imagem novamente; não houve falha
na geometria do catálogo. Nenhuma preferência real foi alterada pelos testes.
Não foi executada nova regressão completa, lint ou type check separado. Ainda é
necessária conferência visual em uso real. Nenhum instalador ou release foi criado.

## Complemento de símbolos — 17/11/2026

Creative Cloud, ChatGPT e VS Code passam a usar vetores da versão 11.15.0 do
Simple Icons, com licença e metadados preservados em licenses. Edge utiliza o
símbolo de navegador do Windows. Hex2Think usa uma adaptação vetorial monocromática
do seu ícone original; Tretalk usa uma adaptação do símbolo extraído do pacote
instalado, preservando a referência SVG original. Esses recursos são locais,
sem consultas de rede durante a renderização. As marcas continuam pertencendo
aos respectivos titulares. Ícones personalizados manualmente têm precedência.

Estas alterações adicionais foram revisadas por leitura de código e diff.
Não foram compiladas nem submetidas a testes ou type check neste pedido.

## Símbolos sem fundo

Em Aparência, “Ícones sem fundo” remove o quadrado dos símbolos unificados e
personalizados. O modo com fundo continua sendo o padrão. Sem fundo, o símbolo
usa branco quando fechado e a cor de destaque quando há uma janela aberta,
incluindo janelas minimizadas. A bandeja de segundo plano identifica seus itens
como ativos. Gavetas e favoritos reutilizam as identidades das janelas já lidas
pelo Dock; não são criados timers nem consultas adicionais ao sistema.

A escolha persiste nas preferências e muda em tempo real. O tamanho do controle,
a área de clique e o zoom existente são preservados. A opção não recolore os
ícones nativos quando a iconografia unificada está desligada e não há símbolo
personalizado. Revisão por leitura de código e diff, sem compilação, testes ou
type check neste pedido.

## Tipografia dos símbolos Adobe

Os símbolos Ps, Ai, Id, Ae, Pr, Lr e Au utilizam agora os contornos vetoriais
originais do Simple Icons 11.15.0, em vez dos monogramas desenhados com Segoe UI.
O contorno externo do quadrado é cancelado pela regra de preenchimento par/ímpar,
preservando o desenho das letras e seus espaços internos. Assim o fundo continua
sendo controlado pelo Orby, inclusive no modo sem fundo. Acrobat usa seu símbolo
vetorial próprio, em vez de Ac. Os SVGs de referência e seus metadados de origem
foram preservados. Creative Cloud mantém o símbolo vetorial já adicionado.

Revisão por leitura dos recursos e do código; sem compilação, testes ou type check
neste pedido. A conferência visual em execução permanece pendente.

### Correção do estado de destaque

No modo sem fundo, somente o aplicativo em primeiro plano recebe a cor de
destaque. Aplicativos atrás de outras janelas ou minimizados ficam brancos.
A identificação reutiliza a janela ativa já observada pelo Dock, sem novas
consultas. Esta regra substitui a anterior, baseada em estar em execução.
Sem compilação, testes ou type check neste pedido.

## Microsoft e ArtCraft

Ampliada a correspondência por nome, executável e identidade de aplicativo do
Windows. Store, Word, Excel, PowerPoint, Outlook, OneNote, OneDrive, Teams, Access,
SharePoint, Visio, Bing, Translator, Azure, SQL Server, Skype e Xbox usam vetores
locais Simple Icons 11.15.0, com referências SVG e metadados de licença.
Utilitários (Paint, Fotos, Captura, Câmera, Relógio, To Do, gravador, mídia,
segurança, entre outros) recebem símbolos funcionais do Windows. Aplicativos
Microsoft não mapeados individualmente usam o símbolo Microsoft, evitando
iniciais genéricas; isso não representa cobertura de logotipos exclusivos para
todos os produtos presentes e futuros.

ArtCraft Launcher, WordCraft, GridCraft e DeckCraft usam adaptações monocromáticas
dos ícones instalados. VectorCraft usa uma pena vetorial para diferenciá-lo: o
recurso de ícone encontrado no executável era o mesmo desenho de apresentação do
DeckCraft. As imagens de referência e os hashes dos executáveis de origem foram
preservados, sem redistribuir ou executar esses binários.

Preservados os ícones importados manualmente, o modo sem fundo e o destaque
somente para o aplicativo em primeiro plano. Sem novas consultas periódicas,
compilação, testes ou type check neste pedido. Conferência visual pendente.
SolidCraft também recebe um símbolo de cubo adaptado do ícone local instalado.

## Resposta do destaque e revisão das opções

A cor do primeiro plano deixou de depender da enumeração de janelas (intervalo de
2 s). O Dock reaproveita seu ciclo de 100 ms para detectar mudança de HWND e
atualizar os símbolos da lista em cache. Após ações de ativação/minimização, faz
uma sincronização imediata. Se uma nova janela ainda não existe no cache, solicita
uma atualização da lista uma vez por mudança de primeiro plano. Sem novos timers,
hooks ou enumerações contínuas; esse caminho fica inativo com o estilo sem fundo
desligado e o timer permanece suspenso no Modo Jogo conforme a regra existente.

Aparência oferece um seletor com três estilos: Originais, símbolo branco com
fundo e flutuante sem fundo, além de orientações sobre importação e restauração.
O experimento continua opcional. O mini tutorial das novidades ganhou destaque
próprio e arquivo de progresso separado; notas prontas em NOVIDADES-ICONOGRAFIA.md.

Validação autorizada em 17/11/2026: Release compilado sem avisos/erros; 8 testes
focados aprovados (3.472 vetores da base e 37 complementos) e 61 testes de regressão
aprovados, sem falhas ou ignorados. A falha inicial de renderização sem Source
revelou que Image reduzia o controle a tamanho zero: ArrangeOverride agora mantém
o espaço reservado para símbolos vetoriais e fallback. A correção e repetição
foram autorizadas pelo usuário. A suíte focada foi repetida após os ajustes finais.
Nenhuma preferência real foi alterada pelos testes. Não houve type check separado,
instalador ou publicação. Não foi medida latência física de clique a pixel; 100 ms
é a cadência do ciclo existente, não uma garantia sob bloqueio do Dispatcher.

### Destaque por contexto — múltiplos monitores

Regra atual: no Dock e nas demais superfícies sem fundo, aplicativos com pelo
menos uma janela visível e não minimizada recebem a cor de destaque em qualquer
monitor. Janelas restauradas em segundo plano também contam; não é feita análise
pixel a pixel da oclusão por outras janelas. Na Biblioteca, Fixados, Recentes,
Todos e resultados do menu, qualquer aplicativo aberto na lista de janelas conta,
inclusive minimizado. Programas sem janela, apenas como serviço, não entram nessa
classificação. A cor continua sendo a de destaque configurada pelo usuário.

A enumeração existente é global entre monitores. Na troca de foco, reutilizamos
as identidades em cache e verificações leves de visibilidade; a cor não depende de
qual monitor contém o primeiro plano. Não foram executados novos testes, type
check ou compilação neste pedido; as 69 aprovações acima são da revisão anterior.
