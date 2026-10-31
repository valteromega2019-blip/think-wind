# Empacotamento 0.18 Alfa

1. Baixar https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe para `artifacts/dependencies/PawnIO_setup.exe`.
2. Conferir Authenticode válido do fornecedor namazso e SHA-256 `1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032`.
3. Publicar: `dotnet publish src/ThinkWind/ThinkWind.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -o artifacts/publish-v0.18.1-alpha`.
4. Compilar `installer/ThinkWind.iss` com Inno Setup 6.7 ou superior.

Nenhum desses comandos executa a suíte de testes. Não adicionar chamadas de testes, lint ou type check sem solicitação explícita do usuário. A publicação normal C#/XAML é a compilação necessária para gerar o executável.

O assistente permanece por usuário; somente o instalador oficial do driver solicita UAC. Sensores já configurados são atualizados pelo próprio fluxo de autorização do coletor, sem apontar tarefas elevadas para pastas graváveis pelo usuário.