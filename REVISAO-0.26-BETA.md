# Revisão Orby 0.26.0 Beta — 01/11/2026

## Escopo e método

Revisão direcionada de mídia/prévias, telemetria, FPS/ETW, inicialização, hooks, descarte de recursos, dependências e atualização. Mantida a branch feature/otimizador-jogos e o visual existente. Usada skill-git.md para versionamento; não foi necessário aplicar skills de imagem, Sites ou engenharia reversa a esta tarefa de código-fonte. Não é uma auditoria exaustiva nem uma certificação de segurança/latência.

## Correções

- MediaService: eventos de mídia passam a compartilhar no máximo uma publicação pendente no Dispatcher; alterações de metadados permanecem sinalizadas. Evita fila de callbacks em rajadas de eventos.
- Ao desativar a mídia, limpa State/erro e notifica o cartão para liberar a referência à capa. Dispose libera também referências ao gerenciador e inicialização. Prévia pausada só solicita substituição quando visível.
- HardwareTelemetry: limita a uma publicação pendente para evitar acumulação de amostras quando a interface fica ocupada. O histórico continua limitado a 450 amostras.
- Descarte do pipe do coletor passa a ocorrer no finally da própria thread de leitura, após cancelamento, evitando corrida com a adoção de uma nova conexão. Recursos nativos de sensores continuam sendo fechados fora da UI.
- FrameMonitor: consulta o processo em primeiro plano no máximo a cada 100 ms, em vez de duas chamadas Win32 a cada evento Present. Contagem de frames continua sendo baseada em eventos reais. O cache pode levar até 100 ms para acompanhar uma troca de janela. Remove a alocação LINQ de chaves por evento; limpa o histórico ao trocar PID e preserva limite de 4096 timestamps.
- StartupPriority: deixa de elevar prioridade por cinco segundos e remove o timer de restauração. Inicialização usa prioridade Normal, inclusive se um jogo já estiver aberto.

## Segurança

`dotnet list ... package --vulnerable --include-transitive --no-restore`: nenhuma vulnerabilidade conhecida reportada pelo NuGet nas dependências resolvidas. Isso não audita comportamento de dependências e não garante ausência de falhas desconhecidas. Dependências principais preservadas: LibreHardwareMonitorLib 0.9.6 e TraceEvent 3.2.8. Sem instalação de dependência nova.

Revisão estática de pontos de rede/execução: GitHub para atualização, Open-Meteo para clima, PowerShell do auxiliar local, processos do coletor/guard e fechamento aprovado. Não foi encontrada evidência conclusiva de payload malicioso nessa leitura, mas não foi realizada análise completa de todas as bibliotecas binárias. A busca direcionada não encontrou chave privada PEM/XML nem token GitHub nos fontes/scripts examinados. Chave privada do publicador permanece fora do repositório, protegida com DPAPI.

Defender: CustomScan da pasta artifacts/preview-v0.26.0-beta solicitado e eventos 1000/1001 confirmaram início e conclusão. Nenhuma nova detecção correspondente foi encontrada na consulta local. Proteção em tempo real e antivírus permaneceram ativos. Não foi gerado instalador 0.26; a varredura não cobre um instalador inexistente e não resolve retroativamente o alerta Contebrew da 0.25.0.

Assinatura: auxiliar em modo somente validação aceitou o pacote oficial 0.25.1 com tag correta (exit 0) e rejeitou a mesma assinatura sob outra tag (exit 1). Nenhum instalador foi executado. A primeira tentativa direta de script foi recusada pela política PowerShell local; repetição usou o mesmo parâmetro por processo que o auxiliar real utiliza, sem alterar a política global. Assinatura própria não é Authenticode nem dispensa antimalware.

## Compilação e verificações

- Release da aplicação: sucesso, zero erros e zero avisos.
- Build da suíte disponível: sucesso, zero erros e zero avisos.
- ThinkWind.Checks padrão, executado após as alterações finais: 54 aprovados, zero falhas/ignorados.
- ThinkWind.Checks --updates: 10 aprovados, zero falhas. Download de exemplo em ambiente de teste e consulta oficial sem instalador executado.
- Duas verificações de assinatura descritas acima aprovadas.
- Cobertura inclui persistência, composição nos dois temas, layout, DPI/fontes, áudio, modo jogo por classificação, miniaturas/liberação, Bluetooth e atualizador. Isso não reproduz todos os fluxos reais de usuário.
- Executável: artifacts/preview-v0.26.0-beta/OrbyLauncher.exe. Não foram solicitados instalador nem publicação nesta tarefa.

## Recursos observados

Processo principal, CPU normalizada pelo número de processadores lógicos:

| Amostra | CPU média | Working set | Memória privada | Handles |
| --- | --- | --- | --- | --- |
| Antes, sessão anterior, 10 s | 0,019% | 312,3 MB | 263,3 MB | 1309 |
| Depois, suíte já encerrada, 15 s | 0,065% | 283,0 MB | 180,8 MB | 1237 |

Amostras curtas de desktop, sem automação de interação ou controle de carga externa; idades/JIT das sessões diferentes. Não demonstram causalidade ou redução de CPU. Redução observada de RAM não é ganho garantido. GPU, latência de periféricos, FPS e frametime não foram medidos. Nenhuma alegação de zero input lag ou ganho de FPS.

## Pendências

Comparar um mesmo jogo/cena com launcher encerrado, ativo e Modo Jogo, usando ETW/WPR; verificar overlay/FPS e transições entre monitores sob carga. Medir GPU e sessões prolongadas. Conferir o player corrigido com WhatsApp e outros provedores reais. Atualização com encerramento/instalação completa não foi exercitada aqui. Certificado Authenticode e análise do instalador 0.25.0 pela Microsoft continuam pendentes. A detecção antiga não foi classificada como falso positivo.

Os recursos existentes de sensores e overlay continuam conforme preferências; não foi desativado um recurso para apresentar números menores. O hook de tecla Windows existente permanece em thread dedicada para preservar o Iniciar do Orby; não foi criado hook global novo. Não há política de prioridade de tempo real.
