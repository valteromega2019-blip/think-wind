# Validação — 0.18 Alfa, revisão 3

Atualização posterior em 31/10/2026: o usuário informou que o FPS também já foi testado. Essa confirmação manual não representa uma medição independente de precisão ou latência realizada pelo agente.

Data: 31/10/2026. Execução autorizada explicitamente pelo usuário nesta entrega.

## Resultados

- Runner `ThinkWind.Checks`: **47 aprovados, 0 falhos, 0 ignorados**.
- `dotnet publish` Release, win-x64, self-contained, com `-warnaserror`: concluído, sem avisos ou erros. A verificação de tipos C# ocorre nessa compilação; não existe uma etapa TypeScript separada.
- Inno Setup: instalador gerado com código de saída 0.
- PawnIO opcional incluído: SHA-256 do pacote oficial conferido antes da geração, correspondente ao valor esperado pelo instalador.
- Na prévia anterior desta mesma correção, o registro do auxiliar confirmou CPU AMD Ryzen 7 9800X3D com temperatura de aproximadamente 43,4 °C. Isso é evidência de uma leitura real, não validação de todos os sensores ou do instalador final.

## Comandos

```powershell
dotnet run --project checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release --no-launch-profile
dotnet publish src/ThinkWind/ThinkWind.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -warnaserror -o artifacts/publish-v0.18.3-alpha
ISCC /Qp installer/ThinkWind.iss
```

Logs locais: `artifacts/validation-v0.18.3/`.

## Escopo e limites

Os checks abrangem preferências, estados da ilha, composição, áudio (reescrita dos valores atuais), dimensões do Dock e da gaveta, miniaturas DWM, Bluetooth por consulta, atalhos e calendário com fontes maiores. Não houve nova medição de FPS ou latência em jogo, pareamento Bluetooth, instalação/desinstalação do pacote final ou inspeção visual exaustiva em todos os monitores. A confirmação do usuário de funcionamento aparente não substitui essas medições.

## Artefato

- `ThinkWind-Setup-0.18.3-alpha.exe` — 63.812.115 bytes.
- SHA-256: `69d6e5690dd8605f5c9598e9a379777f1350df92338070435baad2df2c94f442`.
- Instalador sem assinatura digital; a autorização inicial do Windows permanece necessária para atualizar os auxiliares protegidos.
