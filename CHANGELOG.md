# Histórico do Think Wind

## 0.015 Alfa — revisão 1 (0.15.1-alpha)

- Leitura de System.AppUserModel.ID por janela via SHGetPropertyStoreForWindow; o identificador do processo permanece como alternativa. A identidade por janela é usada pelo Shell para agrupamento e não era consultada antes.
- Atalhos .lnk e AppsFolder resolvidos pelo Shell no worker STA já existente, com cache por versão do atalho/destino. Não depende da abertura prévia da biblioteca para conhecer a identidade de um fixado.
- Associação usa identidade do atalho/janela e executável; atualização também considera mudanças de identidade do fixado. Sem comparação aproximada por título ou imagem.
- 44 testes aprovados, zero falhas/ignorados. Compilação/verificação de tipos Release sem erros ou avisos e instalador gerado. Conferência visual final no Edge ainda pendente; consulte VALIDACAO-0.15.1.md.

## 0.015 Alfa — 0.15.0-alpha

- Publicação das alterações detalhadas abaixo: Dock, ilha, tema, fontes, instalação e detecção de jogos.
- Publish Release win-x64 self-contained com avisos tratados como erros e instalador Inno Setup compilados. Sem novos testes ou type check separado nesta entrega. As notas Em desenvolvimento abaixo registram as etapas de implementação anteriores à publicação.


## Em desenvolvimento — tamanho das fontes

- Controle Tamanho das fontes em Personalizar → Aparência, de 80% a 130%, com pré-visualização imediata e salvamento automático.
- Escala aplicada aos textos das janelas do launcher, incluindo elementos criados depois; mantém tamanhos de ícones em fontes de símbolos. Usa tamanhos originais para evitar ampliação acumulada, sem polling ou redimensionar a janela de personalização.
- Sem compilação, testes ou type check nesta etapa; alteração ainda não está na versão aberta.

## Em desenvolvimento — tema do Windows e launcher

- Botão claro/escuro da ilha e seletor da personalização aplicam a preferência aos aplicativos e ao sistema Windows (AppsUseLightTheme/SystemUsesLightTheme em HKCU), com notificação WM_SETTINGCHANGE fora do dispatcher e sem reiniciar Explorer.
- Automático continua acompanhando o Windows sem alterar sua preferência. Escrita apenas em ações explícitas do usuário, nunca ao iniciar/atualizar aparência; falhas de acesso são informadas e alterações parciais têm tentativa de restauração.
- Sem compilação, testes, type check ou execução nesta etapa. A versão aberta ainda não contém essa integração.

## Em desenvolvimento — alinhamento e fixados do Dock

- Ícones, Iniciar e pesquisa centralizados verticalmente no fundo Acrylic; indicadores deslocados para baixo sem puxar o centro visual dos ícones para cima.
- Associação de janelas a fixados compara caminhos normalizados e AppUserModelID, evita aliases repetidos e reavalia alterações de identidade/catálogo. Consulta do executável usa acesso limitado em lugar de leitura de MainModule.
- Sem compilação, testes ou type check nesta etapa; mudanças ainda não estão no aplicativo aberto.

## Em desenvolvimento — barras ao alternar janelas comuns

- Removida a regra que tratava qualquer aplicativo em primeiro plano como jogo. O perfil inspecionado mantinha GameModeProtectAllForeground=true; esse campo legado não participa mais da decisão, inclusive na sondagem inicial.
- Removida a opção Proteção ampla da personalização. Mantidos reconhecimento por instalação, tela cheia, lista complementar e exceções.
- Revisão por leitura; sem compilação, testes ou type check nesta etapa. A versão instalada ainda usa a regra anterior.

## Em desenvolvimento — convivência e estado da ilha

- Think Monitor (processos ThinkExplode/ThinkMonitor) não ativa Modo Jogo, inclusive em perfis existentes com proteção ampla habilitada. Essa opção estava ativa no perfil inspecionado e podia ocultar/restaurar o launcher ao alternar o foco. Isso não demonstra a causa dos crashes internos do Think Monitor registrados pelo Windows.
- Estado maximizado nativo passa a prevalecer sobre divergências temporárias da área útil. A busca de janelas em segundo plano também considera tela cheia sem bordas; uma janela menor à frente não deve retornar a ilha ao centro enquanto houver maximizada/Snap/tela cheia no mesmo monitor.
- Reserva da barra superior evita reenviar ABM_SETPOS para um retângulo negociado idêntico, reduzindo notificações redundantes a outros aplicativos.
- Revisão por código e registros existentes; sem compilação, testes, type check ou novo instalador nesta etapa. Alterações ainda não estão na versão instalada.

## Em desenvolvimento — configuração na instalação

- Instalador executa `ThinkWind.exe --configure-installation` antes de oferecer a abertura do launcher, inclusive na instalação silenciosa.
- Ativa a preferência Usar somente a barra do Think Wind e a ocultação automática nativa via SHAppBarMessage (ABM_GETSTATE/ABM_SETSTATE), preservando demais preferências e sem reiniciar o Explorer.
- Comando executa sem overlays, hooks ou serviços do launcher. Instância aberta ou falha de leitura/configuração é reportada; configurações ilegíveis não são sobrescritas. Se a gravação falhar, desfaz a alteração de ocultação automática realizada pelo comando.
- A ocultação automática é uma preferência do Windows e pode ser desativada nas configurações da barra. Esta etapa altera somente o código; sem compilação, testes, type check, novo instalador ou publicação.

## 0.11 Alfa — 0.11.0-alpha

- Corrigida falha de inicialização com aplicativo maximizado: criar o HWND da ilha antes de atribuir a janela de status como dependente.

- Modo Jogo reconhece executáveis nas instalações Steam (incluindo bibliotecas adicionais), Epic e GOG, além da detecção existente de tela cheia. A lista manual passa a ser complementar e novos perfis não contêm um jogo específico.
- Opção de proteção ampla cobre qualquer aplicativo em primeiro plano fora do Shell e das exceções, para jogos não reconhecidos; também oculta o launcher em aplicativos comuns quando ativada.
- Metadados locais somente para leitura, com cache de um minuto, sem varredura recursiva, novos hooks ou temporizadores. Caminhos de instalação são heurística: software instalado nas mesmas bibliotecas pode ser reconhecido, e jogos portáteis/de outras lojas podem precisar da proteção ampla ou lista complementar.
- Distribuição Release win-x64 independente e instalador gerados para publicação. Sem novos testes nem type check separado nesta entrega, conforme preferência do usuário.

## 0.10 Alfa — 0.10.0-alpha

- Tecla Windows isolada abre o launcher por hook em thread dedicada, desativado durante jogos. Atalho Ctrl+Alt+Espaço opcional.
- Modo Jogo por primeiro plano, tela cheia e lista de jogos/exceções; esconde interfaces e suspende tarefas não essenciais.
- Enumeração de janelas assíncrona e atualizações coalescidas.
- Miniaturas DWM sob demanda, hover configurável e `WM_CLOSE` individual.
- Painel Bluetooth nativo com descoberta Classic/LE, estado, rádio e pareamento; fallback explícito para perfis sem conexão genérica.
- Ícones `.lnk` recuperados do destino/recurso, sem overlay, com invalidação de cache.
- Distribuição e instalador 0.10.0-alpha publicados. Bateria atual: 42 aprovados, 1 falha por timeout na consulta Bluetooth. Comparação histórica curta no Wardogs: usuário sem atraso percebido no Modo Jogo; sem medição física de latência.

## 0.08 Alfa (0.8.0-alpha) — 2026-10-29

- Reserva superior medida, camada de ícones sem recorte e botão Iniciar dimensionado para ícones grandes.
- Superfícies internas translúcidas, com opacidade global configurável em tempo real; texto e alto contraste preservados.
- 37 checks aprovados, publicação Release com avisos tratados como erros e instalador solicitado. Release privada e site público atualizados.

## 0.07 Alfa (0.7.0-alpha) — 2026-10-29

- Microfone na primeira expansão, reutilizando o controle de entrada do painel completo.
- Tecla Windows isolada alterna o Iniciar próprio enquanto o app estiver em execução; combinações encaminhadas ao sistema, sem histórico de teclado.
- Compilação de tipos C#/XAML com avisos tratados como erros: zero erros/avisos. Publicação e instalador solicitados. Sem suíte de testes ou validação interativa do novo hook nesta etapa.

## v0.06 (0.6.0-dev) — 2026-10-29

- Encaixe da ilha expandida: cartões preenchem suas células; calendário/bandeja têm altura alinhada e coluna de blocos menores termina na mesma base, com intervalos uniformes. Sem novo build ou testes.

- Após o build: camada transparente independente para o Dock, fundo Acrylic compacto, grades uniformes de aplicativos sem cartões e recuperação adicional de bandeja por MSAA. Extração de ícones maiores em worker com cache. Revisão somente de código; sem nova compilação, execução, testes ou type check.

- Compilação Release posteriormente autorizada: zero erros e zero avisos. Entrega para teste em `artifacts/preview-v0.06/ThinkWind.exe`, preservando a cópia anterior em execução. Sem testes, type check separado, execução, publish ou instalador.

- Ampliação estilo Dock para fixados/abertos: campo contínuo de distância, mola compartilhada, deslocamento sem sobreposição, reserva fixa contra cortes e personalização instantânea de intensidade, velocidade e vizinhos. Sem compilação ou testes nesta etapa.

- Refinamento posterior: removido o encaminhamento da área de segundo plano à bandeja do Windows; imagens reais com cache/renderização do host e ações de acessibilidade quando disponíveis.
- Opção persistente para ocultar as barras originais em todos os monitores, com auxiliar de restauração ao encerrar/desativar ou detectar saída do launcher. Não encerra Explorer nem bloqueia a tecla Windows. Limites da enumeração de ícones ocultos documentados.

- Bandeja 6 × 4 com paginação, nomes compactos, favoritos persistentes e prioridade por uso local/recência.
- Calendário mensal aberto no painel completo e configuração do Windows no cabeçalho compacto; personalização do launcher somente no painel completo.
- Mini-ilha WLAN com redes, conexão/desconexão, senha segura e rádio conforme suporte/permissões do Windows.
- Saída padrão de áudio por PolicyConfig isolado, confirmação/recuperação e notificações Core Audio para volume, mute e dispositivos.
- Recolhimento protegido durante interação, ativação por aproximação e transições sincronizadas aos quadros do WPF, com cache de recortes/geometria.
- Cores de destaque suaves e adaptadas ao tema, preservando a composição Acrylic existente.
- Símbolo vetorial do Windows no botão Iniciar da barra inferior, preservando o menu próprio.
- Revisão por leitura de código e diff. Testes, type check, compilação, execução e publish não realizados. Sem instalador; executável anterior preservado.

Detalhes e limites em `REFINAMENTOS-0.06.md`.

## v0.05 (0.5.0-dev) — 2026-10-29

- Menu Iniciar próprio acima da barra, com fixados reordenáveis, recentes, todos os aplicativos, informações do usuário e opções reais de energia com confirmação.
- Biblioteca por AppsFolder e atalhos do usuário/máquina, ícones do Shell, categorias/ordem alfabética, adição em um clique e alternativa manual.
- Pesquisa compacta/lupa com espaço reservado na barra e painel próprio; integração Windows Search/ADO, aplicativos, recentes e configurações nativas.
- Bandeja em mini-ilha superior independente, botões transparentes e acesso à bandeja original preservado.
- Recolhimento padrão de 0,8 s configurável; estado recolhido independente na borda física inferior, com distância final de zero pixels.
- Drag & Drop OLE: revelar na borda, manter aberto durante a operação, ativar janela por hover e negociar entrega com receptores nativos, sem simular recebimento nem mover arquivos de origem.
- Compilação Release posteriormente solicitada pelo usuário: zero erros e zero avisos; aplicativo atualizado iniciado. Testes, type check separado e publish não executados. Nenhum instalador gerado.

Detalhes e limitações em `REFINAMENTOS-0.05.md`.

## v0.04 (0.4.0-dev) — 2026-10-29

- Sete módulos na mesma grade: três principais e quatro blocos secundários responsivos, respeitando ordem e visibilidade; removida a paginação dos módulos.
- Mídia e volume disponíveis no hover, reutilizando os mesmos controles e mantendo seleção de dispositivo e reprodução.
- Engrenagem no cabeçalho da ilha; removidos os acessos duplicados de personalização no hover e na barra inferior.
- Recolhimento automático persistente de 0 a 10 segundos e opção de desativar; proteção para sliders, menus, teclado, edição e mini-ilhas associadas.
- Calendário em mini-ilha, áudio e bandeja compactos; seletores reais e controles nativos preservados.
- Redimensionamento com aceleração/desaceleração, duração estável durante a transição e movimento discreto dos cartões sem reiniciar destinos iguais.
- Revisão por leitura de código e diff. Testes, type check, build e publish não executados; nenhum instalador gerado. Executável anterior preservado.

Detalhes em `REFINAMENTOS-0.04.md`.

## v0.03 (0.3.0-dev) — 2026-10-29

- Dimensionamento proporcional e paginação da barra inferior, sem ScrollViewer ou deformação dos ícones.
- Ilha com três estados preservados, seções paginadas e limites de altura/largura; mini-ilhas sem rolagem.
- Cartão de mídia por sessões nativas do Windows, com metadados, capa, transporte e linha do tempo quando disponibilizados pelo player.
- Área de processos substituída por controles acessíveis da bandeja, ícones visíveis e acesso a ações/menus nativos quando expostos pelo Explorer.
- Blocos arredondados, sliders preenchidos na cor de destaque e brilho via DDC/CI em monitores compatíveis.
- Atualização seletiva de preferências e janela de configurações independente, sem expansão automática da ilha ao personalizar.
- Compilação Release posteriormente solicitada pelo usuário: zero erros e zero avisos. Corrigidas referências de UI Automation e removido fallback LegacyIAccessiblePattern incompatível. Testes, type check separado e publish não executados; nenhum instalador gerado.

Detalhes e limites em `REFINAMENTOS-0.03.md`.

## 0.21.0-dev — 2026-10-29

- Conteúdo WPF com transparência por pixel, template sem fundo de tema e máscara alfa; material separado em HWND nativo sem bitmap GDI.
- Acrylic por HostBackdropBrush/Windows Composition, independente do foco da janela, com recorte e posição sincronizados.
- Estágios compacto (relógio/rede/volume), hover com intervalos de entrada/saída e painel completo explícito.
- Mini-ilhas independentes de volume e rede, com fechamento por Esc/perda de foco e proteção contra conflitos de hover/clique.
- Snapshot real da rede, consulta WLAN quando autorizada pelo Windows, processos reais da sessão e acesso à central nativa de notificações.
- Build sem erros/avisos e 29 checks aprovados no escopo autorizado. Transparência confirmada visualmente pelo usuário. Nenhum instalador gerado.

Detalhes em `VALIDACAO-0.21.md`.

## 2.0.0-dev — 2026-10-29

- Identidade visual baseada no Think Monitor: grafite, ciano e recursos dinâmicos para temas claro, escuro e automático.
- Ícones sem fundos; recuperação pelo Shell/janelas; resolução de atalhos; agrupamento de janelas e indicadores de minimização.
- Ilha integrada ao topo, com acessos configuráveis, estados proporcionais, cartões translúcidos e calendário com tema.
- Material, tonalidade, escala, espaçamento, raio e posicionamento independentes por componente; contorno DWM desativado e recorte nativo.
- Editor visual com arraste de ícones, controles e módulos; linhas de inserção/alinhamento, listas acessíveis e restauração individual/completa.
- Pré-visualização instantânea, salvamento automático com debounce e migração das preferências antigas.

Revisão por leitura de código e diff. Sem testes, type check, build, publish ou novos instaladores, conforme preferências do usuário. Consulte `REFINAMENTOS-2.0.md` para limitações e verificação pendente.

## 0.2.0 — 2026-10-29

- Integração com Core Audio para enumerar saídas e entradas ativas, ler e alterar volume e mudo por dispositivo.
- Controles de áudio na Dynamic Island com opção de acompanhar o padrão multimídia do Windows e mensagens de indisponibilidade.
- Recuperação periódica após conexão/desconexão de dispositivos; consultas suspensas com a ilha recolhida, oculta ou o módulo desativado.
- Preferência persistente para exibir o módulo de áudio.
- Rolagem do conteúdo expandido e limite de altura pela área útil do monitor.
- Atualização da versão do aplicativo e do script do instalador.

Primeira distribuição experimental local, gerada após autorização explícita para testes: 18 checks aprovados, compilação Release sem avisos/erros, publish independente e instalador compilado. Corrigidas ambiguidades de tipo, espaçamento inválido, identificação legível no seletor de áudio, contraste local do calendário e tratamento de HRESULT de endpoints inválidos. Detalhes e limites em `TESTES.md`. Ainda sem release publicado no GitHub.

## 0.1.0 — 2026-10-29

- Base independente em WPF: dock, Dynamic Island, atalhos, calendário, preferências, bandeja e script de instalador.
