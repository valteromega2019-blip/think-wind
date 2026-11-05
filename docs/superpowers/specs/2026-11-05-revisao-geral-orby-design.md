# Revisão gradual do Orby

Abordagem aprovada pelo usuário em 05/11/2026: preservar arquitetura, funções e visual; mostrar a interface primeiro, adiar trabalho secundário, reduzir consultas duplicadas e corrigir inconsistências visuais comprovadas.

## Escopo e critérios

- Base: commit publicado `b14d793` (0.29.1 Beta), branch `perf/revisao-geral-orby`.
- Eliminar a construção descartada do Dock/ilha no início. Compartilhar o catálogo de aplicativos entre monitores com propriedade e descarte pelo App.
- Exibir as janelas antes de iniciar preparação secundária, preservando coleta contínua habilitada pelo usuário e proteção de jogos.
- Preferir inicialização no logon do usuário sem atraso configurado, token interativo limitado e prioridade normal. Manter fallback compatível e remoção ao desativar/desinstalar. Não disputar CPU com jogos nem prometer precedência absoluta sobre outros programas.
- Corrigir recriação redundante de fundos de popup, fallback opaco persistente e recursos de tema desatualizados.
- Reduzir trabalho repetido de mídia, medições e ciclos desnecessários somente onde houver causa identificada. Corrigir tratamento de indisponibilidade de dispositivos e encerramento de recursos.
- Não adicionar bibliotecas, hooks de entrada, serviços permanentes ou funções de produto.
- Não alterar recursos abandonados do Explorer, atalhos de segurança, permissões do sistema ou preferências de outros apps.

## Abordagens

Escolhida: revisão localizada, com inspeção de causa e commits por conjunto coerente. Reescrita ampla foi descartada por risco de regressão e ausência de benefício demonstrado. Prioridade alta/tempo real e alterações globais no atraso do Windows foram descartadas por concorrência com jogos e outros programas.

## Evidências e limites

A inicialização cria Dock/ilha antes de `ConfigureMonitors`, que os aposenta e recria. Cada Dock possui catálogo/worker/observadores próprios. Popups recebem caminhos duplicados de abertura e o fallback troca um Background que não é restaurado. A chave do cache de tema omite `TintOpacity`, usado no brush de popup.

Não executar testes, type check, lint, compilação, aplicação nem medições provocadas: preferência permanente do usuário. Revisar código e diff; registrar contagens/efeitos dedutíveis do código separadamente de ganhos de RAM/CPU/tempo não medidos. Não publicar, instalar ou integrar main nesta etapa.
