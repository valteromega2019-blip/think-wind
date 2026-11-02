# Orby Launcher 0.26.0 Beta

Revisão de estabilidade e consumo, preservando o visual e as funcionalidades.

- Limita eventos de mídia e publicações de sensores pendentes na interface.
- Libera estado/capas ao desativar mídia e corrige corrida no descarte da conexão do coletor.
- Reduz consultas da janela ativa no coletor de FPS para até dez por segundo, preservando a contagem de frames por ETW.
- Remove a prioridade AboveNormal temporária da inicialização e seu timer: o launcher inicia em prioridade Normal.
- Inclui correções da prévia de mídia: controles maiores e player somente durante reprodução ativa.
- Preserva otimizador, tutorial, preferências, aplicativos fixados e assinatura dos pacotes de atualização.

## Validação e limites

A revisão anterior ao empacotamento concluiu compilação Release sem erros/avisos, 54 verificações gerais e 10 do atualizador aprovadas, além de duas verificações de assinatura sem executar instalador. Consulta NuGet não apontou vulnerabilidades conhecidas. Defender concluiu varredura da pasta compilada sem nova detecção correspondente. Isso não certifica ausência de vulnerabilidades ou malware nem antecipa a classificação deste novo instalador.

RAM residente observada: 312,3 MB antes e 283,0 MB depois em sessões de desktop diferentes. CPU: 0,019% e 0,065%. Amostras curtas sem controle completo de carga, sem demonstrar causalidade, redução de CPU ou ganho de FPS. GPU, input lag e frametime ainda precisam de comparação real em jogos. Consulte REVISAO-0.26-BETA.md no código.

O pacote inclui assinatura RSA oficial (.exe.sig) e SHA-256 para o atualizador do Orby. Não é certificado Authenticode e não remove avisos de editor desconhecido do Windows. Versões anteriores à 0.25.1 devem receber a correção do atualizador por instalação manual uma vez. Não desative proteções do Windows.

Instalador x64 com runtime incluído e PawnIO oficial opcional. Nesta solicitação de empacotamento/publicação não foram executados novos testes ou type check.
