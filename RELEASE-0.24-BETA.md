# Orby Launcher 0.24 Beta

## Novidades

- Personalização reorganizada em categorias laterais, com busca por opções (Ctrl+F), navegação compacta e páginas separadas para cores, transparência, tipografia e animações.
- Editor completo do overlay integrado à personalização, compartilhando os controles com a janela avulsa existente.
- Nova aba Aplicativos ao abrir o painel de som: volume e mudo independentes por aplicativo, consultando sessões de todas as saídas de áudio ativas.
- Detecção de brilho de painéis internos de notebooks pelo provedor WMI do Windows, além do DDC/CI existente para monitores externos. Slider de brilho no mesmo visual do volume.
- Indicador do Dock complementado com contadores de mensageiros e badges de notificações expostos pela barra nativa.
- Mantidos teclado personalizado/redimensionável, efeitos magnéticos, calculadora, clima e atualizador da versão anterior.

## Limitações

Brilho depende do suporte do driver e da identificação inequívoca da tela; não ajusta outro monitor por adivinhação. O mixer agrupa sessões pelo executável quando possível, não por aba do navegador. Sessões de áudio muito breves entre consultas podem não aparecer. O estado ativo do Windows pode incluir streams temporariamente silenciosos.

O indicador não lê mensagens privadas. Aplicativos que não exponham contador/badge nem solicitem atenção ao Windows ainda podem ficar sem a bolinha. Não foi reproduzido nesta entrega o recebimento real de uma mensagem do WhatsApp.

Launcher e instalador continuam sem assinatura digital. A instalação integrada permanece bloqueada até existir uma versão-base assinada e instaladores com certificado confiável do mesmo publicador. O download e a instalação manual desta release continuam disponíveis. SHA-256 verifica integridade, não autentica o publicador.

## Distribuição

Windows x64 autocontido, instalador com ícone Orby e PawnIO oficial opcional. Não há desinstalação prévia nem remoção do perfil. Atualizações integradas preservam as escolhas de inicialização/barra pelo caminho específico do instalador.

Compilação e empacotamento solicitados pelo usuário. Nenhum novo teste, lint ou type check separado foi executado nesta publicação. A compatibilidade do hardware do notebook, a interação real do mixer e os badges ainda precisam de conferência em uso real; compilação bem-sucedida não valida esses comportamentos.

Detalhes: BRILHO-NOTEBOOK.md, PERSONALIZACAO-REORGANIZADA.md, MIXER-APLICATIVOS.md e INDICADOR-BADGES.md.
