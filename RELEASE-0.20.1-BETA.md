# Orby Launcher 0.20 Beta — revisão 1

Desenvolvido pela **Think**.

- Instalação independente com executável OrbyLauncher.exe, assembly, pastas de dados, inicialização e auxiliares identificados como OrbyLauncher.
- Novo AppId do instalador e AppUserModelID Think.OrbyLauncher no processo e no atalho, com ícone explícito.
- Catálogo e barra usam o ícone Orby incluído no aplicativo, evitando a imagem antiga retornada pelo Shell.
- Ícone das Configurações do Windows obtido da identidade registrada do pacote. Engrenagem nativa de reserva quando a leitura falhar; bitmaps vazios não substituem o ícone existente.
- Preferências do novo aplicativo ficam em %LOCALAPPDATA%\OrbyLauncher. O instalador não apaga nem importa automaticamente os dados das versões anteriores em %LOCALAPPDATA%\ThinkWind.

O instalador é independente das versões anteriores. Para migrar, preserve suas configurações antes de remover a instalação antiga e importe-as para o novo diretório. Não mantenha as duas versões do launcher ativas simultaneamente.

Compilação e empacotamento realizados. Testes automatizados, type check separado e conferência visual completa não executados nesta entrega, conforme preferência do usuário. O instalador não possui assinatura Authenticode do projeto.

Runtime Windows x64 e PawnIO oficial opcional incluídos. Créditos e informações de segurança em COPYRIGHT.md e SECURITY.md.

SHA-256: `47a4d8e86ffab698a18e82885126e4843f3a24a9db99f4caef0c47833d61b499`.
Tamanho: 66306933 bytes.
