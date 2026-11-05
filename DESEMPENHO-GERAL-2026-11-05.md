# Desempenho geral — 05/11/2026

A revisão ainda não está validada como mais leve. Repouso e interface oculta apresentaram CPU média semelhante, mas os painéis abertos consumiram mais CPU e memória nesta comparação curta. Há indicação de possível regressão; os dados não isolam a causa. Não publicar como otimização comprovada antes do perfilamento dos painéis.

| Estado | CPU publicada → revisão | RAM residente publicada → revisão | Memória privada comprometida publicada → revisão |
|---|---:|---:|---:|
| Repouso | 0,317% → 0,299% | 285,7 → 289,9 MiB | 179,0 → 174,7 MiB |
| Personalização | 0,260% → 1,072% | 311,1 → 321,5 MiB | 213,5 → 223,7 MiB |
| Central expandida | 0,442% → 1,017% | 330,6 → 350,2 MiB | 265,3 → 280,3 MiB |
| Monitoramento | 0,470% → 1,528% | 340,3 → 368,5 MiB | 274,9 → 297,1 MiB |
| Interfaces ocultas | 0,665% → 0,659% | 351,8 → 374,7 MiB | 273,7 → 295,8 MiB |

CPU é do processo principal, normalizada pelos 16 processadores lógicos. RAM residente é WorkingSet, incluindo páginas compartilhadas; memória privada comprometida não equivale à RAM privada residente. Médias das amostras de duas execuções por versão.

## Método

- Base publicada 0.29.1 Beta (`b14d793`), revisão `1e4b584` na branch `perf/revisao-geral-orby`.
- Quatro execuções Release na ordem base, revisão, revisão, base. Instrumentação idêntica em cópias arquivadas dos fontes, sem incorporá-la ao produto.
- Aquecimento de 10 segundos; cinco fases de 15 segundos; acomodação de 2,5 segundos excluída das médias. Entre 28 e 30 amostras por estado/versão, cerca de uma por segundo.
- Painéis reais abertos por chamadas internas. Ocultar janelas WPF não equivale ao Modo Jogo. A tabela exclui o custo das transições.
- Perfis clonados com as mesmas preferências visuais e dois monitores. Desativada nas cópias a configuração automática de privilégios, migração de inicialização, tutorial e recuperação do otimizador, evitando alterações reais do sistema.
- Windows 11 Pro build 26300; Ryzen 7 9800X3D, 16 processadores lógicos, 32 GB RAM; RTX 4060 Ti e Radeon integrada; principal 1920×1080 e secundário 1080×1920.

## Abertura e GPU

Processo iniciado até o primeiro evento ContentRendered do Dock:

- Publicada: 3.499 e 1.518 ms; média 2.509 ms.
- Revisão: 3.560 e 1.553 ms; média 2.556 ms.

A diferença é pequena perante a variação entre execuções. Não há evidência de abertura mais rápida. Isso não mede logon, início a frio nem ordem entre aplicativos no Windows.

Contadores GPU do processo principal: 38 consultas por versão, cerca de uma a cada cinco segundos; máximo reportado 2% na base e 3% na revisão. A resolução é inteira e não inclui todo o trabalho de composição do DWM; não permite afirmar ganho ou custo zero de GPU.

## Preservação e limites

- Quatro execuções concluídas sem erro registrado. Processo principal e guardião amostrados; tabela apresenta somente o principal.
- SHA-256 das configurações reais idêntico antes/depois. Instância original reaberta e conferida: `artifacts/preview-native-visual/OrbyLauncher.exe`, PID 29160. Nenhum instalador ou publicação.
- A instância anteriormente aberta era uma prévia 0.29.0. Não foi usada como base; foi restaurada para preservar a sessão.
- Coletores elevados vinculam-se à revisão do binário; seu vínculo às cópias instrumentadas não foi validado. Os números não representam o custo completo de temperaturas/FPS e integração administrativa.
- Sessões curtas, cache aquecido e atividade externa não isolada. Não comprovam ausência de vazamentos, input lag, perda de FPS, suavidade ou estabilidade prolongada.
- Pendente: perfilamento por thread/alocações dos painéis, controlar trabalhos assíncronos de aquecimento em sessões longas, logon real e coletores administrativos da mesma versão.

## Evidências locais

Diretório `artifacts/performance-geral-20261105/`: `final-01-base/result.json`, `final-02-revisao/result.json`, `final-03-revisao/result.json`, `final-04-base/result.json`, `summary.json`, `final-gpu-samples.json`, `preservation.json`.

Rodada preliminar descartada por incluir acomodação nas fases. Somente resultados `final-*` foram considerados. Perfis pessoais permanecem locais, fora do Git.
