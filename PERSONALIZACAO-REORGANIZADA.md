# Personalização reorganizada

Implementação na branch `fix/brilho-notebook`, conforme escolha do usuário. O trabalho anterior de brilho foi preservado, assim como teclado, widgets e atualizador da base integrada.

## Organização

- **Visual:** Aparência, Transparência, Tipografia e Animações. As opções anteriormente reunidas na página longa de aparência agora têm acesso direto.
- **Seu desktop:** barra inferior, central de controle, ícones, widgets, clima, teclado e editor de layout.
- **Desempenho:** editor completo do overlay, sensores, jogos e monitores.
- **Aplicativo:** sistema, atualizações, tutorial e informações de segurança/créditos.

Navegação lateral agrupada, com categoria selecionada e caminho da página. Em largura menor que 640 DIP úteis para a escala da fonte, a navegação muda para um seletor compacto. A janela abre no canto superior esquerdo da área útil, limitada a 740 × 660 DIP e ao espaço disponível do monitor. Não aumenta em resposta a mudanças de personalização.

## Busca

Campo único no topo, Ctrl+F para focar e Esc para limpar. Busca local nos rótulos e nomes acessíveis dos controles de todas as páginas, ignorando diferenças de acentos/maiúsculas. Resultados abrem a seção e levam ao controle correspondente. Valores de caixas de texto e listas completas de fontes não são indexados. Nenhuma consulta de rede, serviço ou temporizador foi adicionado para a busca.

Páginas permanecem construídas ao navegar: controles, callbacks, salvamento automático, prévia ao vivo e posição de rolagem por categoria são preservados. As barras deslizantes e superfícies continuam usando os recursos visuais do launcher.

## Overlay

O formulário existente foi extraído para `OverlaySettingsContent`, compartilhado entre a página Overlay e a janela avulsa já acessível pela central de controle. Inclui aparência/prévia, ativação, atalho, presets de canto, coordenadas X/Y, escala, presets e seleção das métricas, informações de compatibilidade. A página Sensores mantém autorização e gerenciamento do coletor; não foi alterada a coleta ou a política de permissões.

Não há dois códigos diferentes para o mesmo editor. Cada janela descarta a inscrição de atualização do formulário ao fechar. A janela avulsa continua disponível para preservar os acessos existentes.

## Arquivos

- SettingsWindow.cs, SettingsWindow.Tabs.cs e SettingsWindow.Appearance.cs: estrutura, navegação e divisão das páginas.
- SettingsWindow.Search.cs: busca e navegação até opções.
- SettingsWindow.Overlay.cs: página integrada do overlay.
- OverlaySettingsContent.cs e OverlaySettingsWindow.cs: editor compartilhado e janela avulsa.

Revisão somente por leitura de código e diff. Compilação, testes e type check não executados nesta solicitação, conforme preferência permanente. Não foi instalada nem publicada uma nova versão. O acabamento visual, fontes grandes e transições precisam de conferência após uma compilação solicitada pelo usuário.
