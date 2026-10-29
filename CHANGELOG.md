# Histórico do Think Wind

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
