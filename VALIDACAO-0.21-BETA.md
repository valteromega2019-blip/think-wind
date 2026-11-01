# Validação · Orby Launcher 0.21 Beta

Data: 01/11/2026. Escopo explicitamente solicitado: testes, build, lint e criação do instalador para publicação. Esta autorização vale para esta entrega.

## Recompilação final para publicação

Após a bateria abaixo, os botões de minimizar e fechar foram restaurados no leque do navegador. O usuário autorizou recompilar, gerar instalador e publicar essa correção simples. Não foram repetidos testes ou lint nesta recompilação.

- Código da correção: `cfccd4a813114058b8ff1a4eeddcc29cb04950fb`.
- Publish final: `artifacts/publish-v0.21.0-beta-final`, Release win-x64 self-contained, avisos tratados como erros, analisadores desativados; zero erros e zero avisos.
- InformationalVersion: `0.21.0-beta+cfccd4a`.
- Instalador final: `artifacts/installer/OrbyLauncher-Setup-0.21.0-beta.exe`, 66.329.599 bytes.
- SHA-256 final: `5c34cfe3e77c0e9a89ede92ed9ff507b2b45b31dfbd1ffcf569079191df5d17e`.
- Release prevista: `https://github.com/valteromega2019-blip/think-wind/releases/tag/v0.21.0-beta`.

Os resultados de 50 verificações a seguir se referem à preparação anterior à última correção, não a uma nova execução sobre ela.

## Resultado final

- 50 verificações aprovadas; zero falhas; zero ignoradas.
- Build Release dos checks e do aplicativo com avisos tratados como erros: zero erros e zero avisos.
- Lint Roslyn: `dotnet format analyzers --verify-no-changes --severity warn`, app e checks, código de saída zero.
- Estilo: `dotnet format style --verify-no-changes --severity warn`, app e checks, código de saída zero. Não foi aplicada formatação automática.
- `git diff --check`: sem erros.
- Publish `win-x64`, self-contained, Release: concluído com avisos tratados como erros.
- Instalador Inno Setup: concluído, código de saída zero.
- Executável do publish iniciado e processo confirmado em `artifacts/publish-v0.21.0-beta/OrbyLauncher.exe`, junto ao guardião da barra. Não foi instalado sobre a versão do usuário.

## Escopo dos testes

O runner `checks/ThinkWind.Checks` usa configurações e atalhos em uma pasta temporária exclusiva. Verifica persistência, normalização, composição e recorte de HWNDs, alfa, mini-ilhas, áudio, grade de aplicativos, geometria dos widgets, Dock, miniaturas DWM, Bluetooth e ícones.

Nesta execução foram enumeradas cinco saídas e três entradas de áudio. O teste dos setters regravou os valores já existentes, sem alterar intencionalmente volume ou mudo. Bluetooth detectou um adaptador; foram encontrados zero endpoints pareados nas consultas e zero watchers ativos na medição. Esses resultados não validam descoberta efetiva, pareamento ou conexão de dispositivos.

Foram adicionadas três regressões:

1. Leque com quatro cartões, alinhamento, inclinação vertical 3D e tonalidade sem o redutor extra de opacidade.
2. Cartão sintético do Meet dentro do limite de 320 × 160 DIP, com texto e ícone de mudo vermelhos. Não aciona uma chamada real.
3. Distinção entre perfil novo e perfil já salvo, preservando a pendência de tutorial criada pelo instalador.

## Falhas investigadas antes da aprovação

A primeira execução encontrou 46 aprovações e uma falha em um teste antigo de disposição dos módulos. O teste exigia bases iguais para módulos que agora pertencem a linhas diferentes. Foi atualizado para conferir alinhamento dentro de cada linha, limites e ausência de sobreposição em larguras de 360, 520 e 780 DIP. O algoritmo do layout não foi alterado para satisfazer o teste.

A nova verificação do Meet inicialmente exigia tamanho renderizado exatamente igual ao limite. A medição mostrou 300 × 130,55 DIP: o conteúdo já cabia e o Viewbox `DownOnly` corretamente não o ampliava. A asserção passou a verificar tamanho positivo dentro do limite, mantendo as verificações de vermelho e ícone riscado. Depois dessas correções dos testes, a bateria completa terminou com 50 aprovações.

## Comandos principais

```powershell
dotnet build checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release -p:NuGetAudit=false -p:TreatWarningsAsErrors=true -o artifacts/checks-v0.21.0-beta
artifacts/checks-v0.21.0-beta/ThinkWind.Checks.exe
dotnet format analyzers src/ThinkWind/ThinkWind.csproj --no-restore --verify-no-changes --severity warn
dotnet format style src/ThinkWind/ThinkWind.csproj --no-restore --verify-no-changes --severity warn
dotnet format analyzers checks/ThinkWind.Checks/ThinkWind.Checks.csproj --no-restore --verify-no-changes --severity warn
dotnet format style checks/ThinkWind.Checks/ThinkWind.Checks.csproj --no-restore --verify-no-changes --severity warn
```

O publish usou restore isolado em `artifacts/obj-v0.21.0-beta`, `PublishSingleFile=false`, `DebugType=None` e `SourceRevisionId=61b3eea91fadc984fdffd046ebba94cb1484ed0f`. O log completo dos checks está em `artifacts/checks-v0.21.0-beta.log`.

## Artefatos da primeira preparação (substituídos pela recompilação acima)

- Código da entrega: commit `61b3eea91fadc984fdffd046ebba94cb1484ed0f`, branch `feature/tutorial-interativo`.
- Instalador: `artifacts/installer/OrbyLauncher-Setup-0.21.0-beta.exe`.
- Tamanho: 66.330.723 bytes.
- SHA-256: `f4fe644bd982bc4e12b0235ff8c79bcb7895990115e36facb0243901758bce91`.
- Checksum: `artifacts/installer/OrbyLauncher-Setup-0.21.0-beta.exe.sha256`.
- Assinatura do instalador: `NotSigned`.
- O PawnIO incluído manteve hash esperado e assinatura Authenticode válida do fornecedor. O driver não foi instalado novamente.
- A saída nova não inclui a extensão de navegador nem seu host removidos.

## Limitações

Não foram feitos testes reais de encerrar, silenciar ou sair de chamadas no Discord, Tretalk, Meet, Teams ou Zoom. Não foram reproduzidos integralmente o tutorial por mouse, cache de abas minimizadas, DPI misto, instalação/desinstalação, consumo prolongado ou latência em jogos nesta entrega. As verificações de geometria e estado sintético não comprovam compatibilidade universal dos provedores de acessibilidade.

A primeira etapa terminou com os artefatos preparados. A publicação foi autorizada posteriormente pelo usuário; a recompilação correspondente está registrada no início deste relatório.
