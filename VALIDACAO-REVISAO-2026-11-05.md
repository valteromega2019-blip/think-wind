# Validação da revisão gradual — 05/11/2026

Pedido explícito do usuário: “faças os testes”. Escopo desta rodada: revisão na branch `perf/revisao-geral-orby`, sem publicação, instalação ou reinicialização do Windows.

## Resultado final

| Etapa | Resultado |
|---|---|
| Compilação Release com avisos tratados como erros | 0 erros, 0 avisos |
| Suíte geral | 61 aprovados, 0 falhos, 0 ignorados |
| Atualizador | 10 aprovados, 0 falhas |
| Regressões específicas da revisão | 8 aprovados, 0 falhas |
| Total da rodada final | **79 aprovados, 0 falhas, 0 ignorados** |

Comandos efetivamente executados:

```powershell
dotnet build checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release -warnaserror -p:RunAnalyzers=false -p:NuGetAudit=false -o artifacts/checks-revisao-20261105
dotnet artifacts/checks-revisao-20261105/ThinkWind.Checks.dll
dotnet artifacts/checks-revisao-20261105/ThinkWind.Checks.dll --updates
dotnet artifacts/checks-revisao-20261105/ThinkWind.Checks.dll --revision
```

Logs locais finais em `artifacts/checks-revisao-20261105/general.log`, `updates.log` e `revision.log`. O executável compilado está no mesmo diretório; o launcher completo não foi iniciado por esta rodada.

## Cobertura

- Composição nativa do Dock/ilha em claro e escuro: HWNDs, recorte, alfa e sincronização durante expansão.
- Gaveta de aplicativos, layout dos módulos, fontes entre 100% e 250%, contraste em 125 fundos e recursos de tema.
- Prévias DWM, fechamento individual de janelas de teste, descarte de miniaturas, hover e leque de abas.
- Persistência isolada de configurações, alarmes, monitoramento e seletor de discos.
- Rede/Wi-Fi, enumeração/leitura de áudio e regravação dos mesmos valores de volume/mudo. Cinco saídas e três entradas ativas; nenhuma mudança intencional de nível audível ou dispositivo padrão.
- Bluetooth: um adaptador encontrado, zero endpoints retornados por cada seletor e zero watchers ativos na observação do teste. Os checks passaram nos limites existentes; isso não comprova descoberta/pareamento de dispositivos reais.
- Atualizador: SemVer, origem restrita, falhas de rede, integridade/adulteração de conteúdo de teste, autorização, persistência de aviso, adiamento durante jogos e consulta real à API do GitHub. Download usa handler simulado; nenhum instalador foi executado.

## Oito regressões novas

1. Pedido cancelado antes de entrar no worker não executa a ação.
2. `OperationCanceledException` da ação resulta em tarefa cancelada, não falha.
3. Descarte idempotente do worker deixa a ação em andamento terminar e cancela ações na fila/novos pedidos.
4. Catálogo suspenso cancela identidades e ícones sem publicar alterações.
5. Mudança isolada de `TintOpacity` atualiza o popup nos dois temas.
6. Fallback do popup restaura brush local, recurso dinâmico e binding; os dois últimos continuam atualizando depois da restauração.
7. Definição XML do logon: sem atraso, token interativo limitado, prioridade normal, caminho/argumentos e escape de caracteres preservados.
8. Preferência desativada de início automático e versão de migração persistem no perfil temporário.

A geração da definição foi extraída de `SetEnabled` para `StartupRegistration.Definition`, sem mudança na política ou conteúdo do XML, permitindo verificar a configuração sem registrar/executar uma tarefa nem escrever nas chaves reais de início automático.

Um aviso de nulabilidade no novo código de teste foi corrigido antes da compilação/rodada finais. Nenhum teste falhou durante as execuções desta rodada.

## Limites

- Não foi executada a migração real de tarefas de logon, desinstalação ou reinicialização para medir velocidade de abertura.
- Não foram medidos RAM/CPU/GPU/FPS ou latência física de teclado/mouse em jogos. Os checks do Modo Jogo validam regras, não desempenho real.
- Não houve validação interativa completa do launcher, sessão prolongada, troca física de monitores/DPI, pareamento Bluetooth, dispositivos de áudio desconectando nem todos os players/navegadores reais.
- Não foram executados lint/analisadores adicionais. O compilador validou C#/XAML e referências durante o build Release.
- As verificações usam Application WPF isolada e dados temporários; não iniciam o `App` do launcher com seus serviços/permissões reais.
- Autorização para testes vale somente para esta rodada, sem alterar a preferência permanente.
