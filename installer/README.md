# Empacotamento 0.28.0 Beta

1. Baixar https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe para `artifacts/dependencies/PawnIO_setup.exe`.
2. Conferir Authenticode válido do fornecedor namazso e SHA-256 `1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032`.
3. Publicar: `dotnet publish src/ThinkWind/ThinkWind.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:RunAnalyzers=false -o artifacts/publish-v0.28.0-beta`.
4. Compilar `installer/ThinkWind.iss` com Inno Setup 6.7 ou superior.

Nenhum desses comandos executa a suíte de testes. Não adicionar chamadas de testes, lint ou type check sem solicitação explícita do usuário. A publicação normal C#/XAML é a compilação necessária para gerar o executável.

O assistente permanece por usuário; somente o instalador oficial do driver solicita UAC. Sensores já configurados são atualizados pelo próprio fluxo de autorização do coletor, sem apontar tarefas elevadas para pastas graváveis pelo usuário.

## Idiomas do instalador

`OrbyLanguages.iss` inclui sete traduções oficiais do Inno Setup: português do Brasil, português de Portugal, inglês, espanhol, francês, alemão e italiano. `ShowLanguageDialog=yes` apresenta a seleção antes do assistente na execução interativa, com a preferência do Windows como sugestão. Mensagens próprias sobre sensores e configuração também são traduzidas. A página inicial informa que a interface do Orby continua em português. Em atualização silenciosa, não é aberto um seletor interativo. Os arquivos de linguagem com caracteres acentuados devem permanecer em UTF-8 com BOM.

A partir da 0.20.1 Beta, o pacote usa OrbyLauncher.exe, pasta OrbyLauncher e novo AppId. É uma instalação independente, sem importar ou remover os dados da instalação anterior automaticamente.

## Assinatura dos pacotes de atualização Beta

Após gerar o instalador, execute `powershell -NoProfile -File tools/Sign-Update.ps1 -Installer artifacts/installer/OrbyLauncher-Setup-VERSAO.exe -Tag vVERSAO`. Substitua VERSAO pela mesma versão/tag do pacote. Publique o `.exe.sig` junto com o `.exe` e o SHA-256 na mesma release. A API do GitHub fornece também o digest e o tamanho da assinatura.

O script usa a chave privada DPAPI de `%LOCALAPPDATA%\OrbyPublisher\update-signing-key.dpapi`, vinculada à conta Windows de publicação. Ela nunca deve ser enviada ao GitHub, incluída no instalador, logs ou arquivos do projeto. Não gere uma nova chave silenciosamente se perder esse arquivo: isso quebra a confiança dos clientes existentes. A chave pública RSA-3072 está em Assets/UpdatePublicKey.xml e é embutida no executável. A rotina de assinatura compara as duas chaves antes de assinar.

A assinatura RSA/SHA-256 PKCS#1 vincula o domínio OrbyLauncher.Update.SHA256.v1, a tag e o hash exato do instalador. Não é Authenticode e não remove avisos SmartScreen/editor desconhecido. O cliente confere origem, hash da API e assinatura; o auxiliar repete hash e assinatura com o instalador bloqueado para escrita antes de anunciar prontidão. Assinaturas Authenticode inválidas continuam recusadas; o caminho alternativo só aceita ambos os binários no estado NotSigned.

A versão antiga que exige somente Authenticode precisa receber o novo launcher manualmente uma vez. Adicionar um `.sig` a uma release não modifica o código já instalado. Versões futuras com o novo cliente poderão usar o fluxo integrado com pacotes assinados. Não foi gerado novo instalador nesta correção sem pedido de compilação.
