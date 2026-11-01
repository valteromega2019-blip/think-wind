# Orby Launcher 0.21 Beta

## Novidades

- Tutorial interativo de primeira abertura, com três módulos, destaques, progresso, repetição e continuação pelas configurações.
- Instruções posicionadas ao lado dos painéis e à frente das outras janelas, sem tirar o foco da pesquisa.
- Pesquisa e barra inferior recolhidas ao avançar no tutorial.
- Prévias de chamadas com microfone, áudio e saída da chamada quando os controles de acessibilidade estão disponíveis.
- Integração com Tretalk desktop e indicações de mudo em vermelho.
- Meet em cartão lateral compacto, preservando a prévia do navegador.
- Até quatro abas em leque vertical, com cartões traseiros inclinados em profundidade e acabamento Acrylic reforçado.
- Memória das últimas abas identificadas para janelas minimizadas, sem restaurá-las apenas pelo hover.
- Botões de minimizar e fechar a janela do navegador restaurados nas prévias, sem propagar o clique para a ação de abrir a aba.
- Player com capa até os limites do cartão e transparência das prévias sincronizada ao tema.

A extensão que exigia instalação no navegador foi removida. Sem extensão, imagens independentes de abas inativas não estão disponíveis; a lista depende da acessibilidade e pode exigir abrir o navegador uma vez para ser identificada.

## Validação

Na preparação da versão, 50 verificações foram aprovadas, com zero falhas e zero ignoradas; build, estilo e lint foram concluídos. A correção posterior dos botões de minimizar e fechar foi recompilada e empacotada para publicação sem repetir testes ou lint, conforme o escopo solicitado. Os testes não substituem a conferência de chamadas reais e múltiplos monitores. Detalhes em [VALIDACAO-0.21-BETA.md](VALIDACAO-0.21-BETA.md).

## Arquivos para a release

- `OrbyLauncher-Setup-0.21.0-beta.exe`
- `OrbyLauncher-Setup-0.21.0-beta.exe.sha256`

SHA-256 do instalador: `5c34cfe3e77c0e9a89ede92ed9ff507b2b45b31dfbd1ffcf569079191df5d17e`.

Entrega Beta para Windows 11 x64. O instalador ainda não possui assinatura digital. As integrações de chamadas dependem dos controles expostos pelo aplicativo e das permissões do Windows.
