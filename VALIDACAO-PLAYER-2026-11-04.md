# Revisão e validação das prévias — 04/11/2026

Branch: `feature/player-persistente`. Sem alteração de versão, instalador ou publicação.

## Alterações

- O cache das abas agora transfere os pixels reduzidos diretamente para um `BitmapSource` imutável. Foi removido o ciclo de compressão PNG, armazenamento em stream e decodificação de cada captura. Mantidos o limite de 24 imagens, a captura somente de abas visitadas e as verificações de identidade e cobertura.
- Ao fechar uma prévia, são liberados os controles das imagens, menus, títulos retidos e relógios de animação, além dos recursos DWM e Acrylic que já eram descartados. Os grupos do navegador também liberam suas referências aos cartões.
- Uma brush congelada é compartilhada entre cartões com a mesma tonalidade, em vez de alocar uma para cada superfície. A atualização de destaque usa o índice corrente sem outra busca na lista.
- A identificação do cartão foi extraída sem mudar o comportamento, permitindo exercitar o hit test real do WPF com cartões sobrepostos.

## Validação executada

- Build Release do launcher e do projeto de checks: zero avisos e zero erros na execução final.
- Suíte geral: **61 aprovados, zero falhas, zero ignorados**. Inclui configurações, áudio, composição, layouts, DPI/fontes, temas, Bluetooth sem pareamento, identidade, alarmes, seletores e recursos nativos.
- Suíte do atualizador: **10 aprovados, zero falhas**. Inclui autorização de download, integridade, adulteração, origem, persistência, adiamento da notificação e consulta real à API oficial. Nenhum instalador foi executado.
- Consulta NuGet de dependências diretas e transitivas: nenhuma vulnerabilidade conhecida reportada pelas fontes consultadas. Isso não é uma certificação de ausência de malware nem substitui uma análise de antivírus do instalador.
- Publish Release win-x64 self-contained concluído. Executável otimizado aberto em `artifacts/preview-revisao-player/OrbyLauncher.exe`.

A primeira execução identificou um teste antigo que contava quatro filhos, sem considerar o novo botão de contagem. O teste foi atualizado para verificar cartões, botão, alinhamento e tonalidade reais. Foram acrescentadas regressões para até seis cartões, contagem apenas de ocultos, abas sem imagem recolhidas, descarte, seleção por Z-order e prévia no desktop preservando foco, posição e estado minimizado. O teste de hit test usa uma janela própria, sem controlar aplicativos do usuário.

Logs locais:
- `artifacts/checks-player-final.log`
- `artifacts/checks-updates-player.log`
- `artifacts/performance-preview-before.json`
- `artifacts/performance-preview-after.json`

## Amostras de recursos

Amostras isoladas de aproximadamente oito segundos do processo principal, antes e depois. A instância anterior estava aquecida; a posterior havia sido reiniciada. Interações e serviços de fundo não foram controlados. Não se deve atribuir a diferença inteira às mudanças ou tratá-la como benchmark reproduzível.

| Medida | Antes | Depois |
|---|---:|---:|
| CPU, equivalente a um núcleo | 2,521% | 1,162% |
| Working set | 304,63 MB | 281,84 MB |
| Memória privada | 178,49 MB | 162,20 MB |
| Handles | 1.194 | 1.216 |
| Threads | 25 | 36 |

Os contadores incluem o efeito da inicialização e não demonstram uma redução sustentada de recursos. Os benefícios verificáveis por código são a remoção do ciclo PNG, menos brushes e descarte antecipado de controles e animações.

## Pendências e limites

- A comparação histórica `--performance-comparison` depende do Wardogs aberto e de um executável diagnóstico antigo em `artifacts/diagnostic-v0.10`. Não foi executada nesta sessão: o jogo não estava aberto. Não há resultado novo de FPS, latência de entrada ou GPU.
- Conteúdo protegido, tela cheia exclusiva, comportamento de abas de navegadores reais, DPI e disposição em vários monitores ainda exigem conferência com os aplicativos e hardware correspondentes. Testes de layout e DWM não substituem essa conferência.
- Não há comando de lint no `docs/package.json`; o site não foi alterado nesta revisão.
