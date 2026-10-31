# Validação 0.18 Alfa

Pedido explícito: compilar, executar os testes e gerar instalador. Data: 30/10/2026.

## Testes executados

Comando: dotnet run --project checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release

Resultado final: **47 aprovados, 0 falhos, 0 ignorados**. Log em artifacts/validation-v0.18/checks-final.log.

Cobertura: persistência e normalização de preferências; janelas nativas; estados da ilha; composição/clipping claro e escuro; áudio real com escrita dos valores atuais; layout e capacidade do Dock; miniaturas DWM/WM_CLOSE em janelas de teste; Bluetooth somente consulta e ciclo de watchers; identidade/ícones de atalhos. Novas verificações de posição do overlay, ilha secundária opcional e células do calendário com fontes de 11,2, 14 e 35 DIP em larguras de 220 e 320 DIP.

A primeira rodada revelou uma expectativa desatualizada do teste de Modo Jogo: a proteção fullscreen é independente dessa opção e das exceções. O teste foi ajustado para cobrir ambas as configurações, sem mudar a implementação para satisfazê-lo. Na criação do teste de calendário, a comparação de RenderSize/DesiredSize foi corrigida para descontar as margens de layout.

## Limites

Não houve medição comparativa de latência, leitura de FPS em jogo ou validação de pareamento Bluetooth. O teste de calendário verifica dimensões, não substitui inspeção visual. O foco de teclado e o Acrylic do overlay em jogos precisam de confirmação interativa. Não foram desligados aplicativos reais nem alteradas conexões Bluetooth.

## Distribuição

Publish Release win-x64 self-contained e compilação Inno Setup para a versão 0.18.0-alpha. Runtime e instalador oficial PawnIO opcional incluídos. Artefato e SHA-256 em artifacts/installer. Nenhuma instalação ou publicação remota executada nesta etapa.