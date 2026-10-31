# Think Wind 0.17 Alfa

## Novidades

- Personalização organizada em abas, com fontes de 80% a 250% e escolha de ícones na barra principal, na expansão ou desativados.
- Primeira expansão da Dynamic Island com mídia e áudio em tamanho natural, sem rolagem nessa área; medição do conteúdo antes da abertura.
- Área de monitoramento de CPU, GPU, memória e armazenamento, com overlay opcional para jogos e configurações próprias.
- Suporte configurável a múltiplos monitores, menus de ferramentas do sistema, clima junto ao relógio e botão Mostrar área de trabalho no Dock.
- Filtro de sessões de mídia de navegadores, ajustes nas miniaturas e opção protegida de encerrar aplicativos em segundo plano.
- Manutenção da ocultação da barra original, inclusive nas transições de tela cheia.

## Instalação e sensores

Distribuição independente Windows x64, com runtime incluído. Atualiza a instalação existente e preserva preferências. O assistente configura a ocultação automática da barra original e a substituição pelo launcher; confira essas opções em Personalizar > Sistema.

Inclui o instalador oficial assinado PawnIO 2.2.0, opcional no assistente. Uma instalação compatível existente é preservada. A instalação do driver solicita a autorização normal do Windows. O driver é compartilhado e não é removido ao desinstalar o Think Wind.

A interface continua com permissões normais. O coletor de sensores utiliza uma tarefa separada com arquivos protegidos em Program Files. A primeira configuração ou atualização do coletor pede UAC. A versão do coletor é conferida para evitar reutilizar um payload antigo após atualizar o aplicativo.

## Compilação e limites

Aplicativo compilado para distribuição e instalador gerado. Não foram executados testes, suíte de regressão nem type check separado nesta publicação, conforme a preferência do usuário. A prévia anterior foi conferida pelo usuário; isso não representa validação completa em todos os dispositivos.

Leituras dependem do hardware, driver e autorização. Temperaturas do processador, coleta ETW/FPS e instalação limpa dos sensores ainda precisam de validação prática. O overlay não injeta código em jogos e pode não aparecer em tela cheia exclusiva. Bluetooth e bandeja continuam sujeitos às APIs, permissões e recursos disponibilizados pelo Windows. O instalador do Think Wind não tem assinatura digital.

Consulte COPYRIGHT.md, SECURITY.md e as licenças incluídas. Compare o SHA-256 do instalador com SHA256SUMS.txt publicado nesta release.