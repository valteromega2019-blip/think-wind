# Validação 0.30.0 Beta — 05/11/2026

## Integração
A branch feature/barra-inferior-fixa contém perf/revisao-geral-orby e feature/layout-widgets. Nenhum commit de hoje em outras branches ficou fora da ancestralidade da entrega. Não foram incluídos os experimentos abandonados de Explorer/Windows nativo.

## Testes executados
- Suíte geral: 60 aprovados, 1 falha, 0 ignorados.
- Layout/transições da central: 18 aprovados, 0 falhas.
- Revisão de recursos, cancelamento, fontes e inicialização: 10 aprovados, 0 falhas.
- Atualizador: 10 aprovados, 0 falhas; consulta real sem download automático.
- Total: 98 aprovados, 1 falha.
- Falha pendente: BluetoothSnapshot / FindAllAsync excede 20 segundos neste PC, inclusive consultando somente pareados. Adaptador detectado; dois watchers iniciam e são liberados corretamente no teste de ciclo de vida. Não considerar a listagem/pareamento plenamente validada.
- Testes atualizados para respeitar fechamento adiado do popup e limite de fonte solicitado de 140%; nenhuma expectativa foi relaxada para ocultar a falha Bluetooth.

## Build e embalagem
Publish Release Windows x64, runtime incluído, single-file. Inno Setup com sete idiomas, PawnIO opcional. Assinatura RSA separada e SHA-256 para a atualização integrada; não é assinatura Authenticode.

## Tutorial
Tutorial completo e mini tutorial incluem editor de widgets, redimensionamento pelas bordas, seleção, conclusão e barra fixa. Guias dispensáveis e repetíveis nas configurações; progresso independente do tutorial anterior.

## Limites
Não foi realizado novo benchmark de jogos/ETW, pois não havia cenário de jogo preparado. Os testes não comprovam ausência universal de input lag nem ganho de consumo em todo hardware. Conferência física Bluetooth, DPI/múltiplas telas e todas as combinações visuais permanece necessária. Logs locais em artifacts/validation-v0.30.0-beta.