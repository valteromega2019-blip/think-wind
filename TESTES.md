# Relatórios de validação

## Validação mais recente — v0.06

Bateria explicitamente solicitada em 2026-10-29: **34 checks aprovados, zero falhos e zero ignorados**, seguida de build Release sem erros/avisos. Corrigidos o tamanho dos ícones, a estabilidade da grade Fixados/Recentes e uma atualização de Wi-Fi fora do dispatcher. Escopo, limites e executável em [VALIDACAO-0.06.md](VALIDACAO-0.06.md). Esta autorização não se estende automaticamente a alterações futuras.

A atualização v0.21 foi compilada e validada com 29 checks aprovados, conforme pedido explícito do usuário. Relatório atual em [VALIDACAO-0.21.md](VALIDACAO-0.21.md). A execução anterior da etapa 2.0-dev foi autorizada posteriormente pelo pedido de abrir o aplicativo; nenhuma autorização é estendida automaticamente a mudanças futuras.

## Histórico da primeira versão experimental — 0.2.0

**Relatório histórico da 0.2.0. Não valida as alterações 2.0.0-dev, que não foram compiladas nem testadas nesta etapa.**

Data: 29/10/2026. Escopo autorizado explicitamente pelo usuário: realizar os testes e entregar uma primeira versão para avaliação.

## Resultados efetivamente obtidos

- SDK .NET 10.0.403 e Inno Setup 6.7.3 instalados pelas distribuições oficiais via WinGet.
- Build Release: aprovado com zero erros e zero avisos após correções.
- Checks automatizados: **18 aprovados, zero falhos, zero ignorados** na execução final.
- Publish `win-x64`, self-contained e single-file: concluído com sucesso. Inclui as bibliotecas nativas para extração automática do runtime.
- Instalador Inno Setup: compilado com sucesso, após o publish final.
- Executável publicado iniciado no Windows: dock e ilha observados em execução, com relógio, lista real de aplicativos e ícones, níveis e nomes de áudio.
- Interação visual: expansão da ilha e abertura do calendário observadas. Corrigidos os símbolos de mídia que não renderizavam e os nomes internos expostos no seletor.

## Checks automatizados

Projeto: `checks/ThinkWind.Checks`. Runner de console sem dependências externas, com código de saída diferente de zero em caso de falha. Configurações usam uma pasta temporária exclusiva, sem editar as preferências reais do usuário.

1. Arquivo ausente carrega padrões.
2. Preferências e atalhos persistem ao reabrir.
3. Escala/opacidade são limitadas aos intervalos válidos.
4. JSON inválido gera aviso e é preservado.
5. Atalhos nulos/vazios são removidos.
6. Lista nula de atalhos não impede inicialização.
7. Substituição do arquivo não deixa `.tmp` no caminho de sucesso.
8. Falha de escrita em diretório inválido é reportada.
9. Enumeração Win32 retorna janelas com identificadores e títulos.
10. RAM do sistema retorna percentual válido.
11–14. Saída: enumeração, leitura, regravação de volume/mudo atuais e erro recuperável para endpoint inválido.
15–18. Microfone: os mesmos quatro checks.

Na máquina local foram enumeradas cinco saídas e três entradas ativas. A leitura percorreu todos esses dispositivos. A escrita usou o primeiro endpoint ativo de cada tipo e preservou seus valores existentes, sem testar uma mudança audível nem gravar áudio.

Comando usado, que só deve ser repetido quando solicitado:

```powershell
dotnet run --project checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release --no-launch-profile
```

## Limites desta entrega

- O usuário interrompeu Computer Use com Esc. Não foram feitas novas interações na tela após essa interrupção.
- A correção final de contraste do calendário foi compilada, mas sua aparência não foi reinspecionada depois da interrupção.
- Não foram concluídos testes interativos de salvar preferências, adicionar/reordenar/remover atalhos, ocultação automática, menu Iniciar, controles de mídia, troca de seleção de áudio, fechamento pela bandeja ou instância única.
- Não foram executados testes completos de instalação/desinstalação, DPI misto, troca de monitores, desconexão física de dispositivos, falha do Explorer ou consumo prolongado de recursos.
- O instalador e o executável não têm assinatura digital. Não há atualização automática nesta etapa. A barra nativa permanece visível.

O argumento `--inspect` expõe as janelas na barra nativa para ferramentas de inspeção. A execução normal mantém as janelas fora dela. `--settings` abre diretamente a personalização ao iniciar uma nova instância.

## Arquivos para testar

- Instalador: `artifacts/installer/ThinkWind-Setup-0.2.0.exe`.
- Execução sem instalação: `artifacts/publish/ThinkWind.exe`.

Abra somente uma instância. Para personalizar ou sair, use o ícone Think Wind na bandeja do Windows. A primeira inicialização do executável independente pode levar mais tempo devido à extração do runtime.
