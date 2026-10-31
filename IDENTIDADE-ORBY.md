# Identidade independente do Orby Launcher — alterações em desenvolvimento

Preparada a próxima versão 0.20.1-beta como instalação independente, conforme solicitado pelo usuário:

- Executável e assembly OrbyLauncher.exe / OrbyLauncher.dll; namespaces e identidade do manifesto OrbyLauncher.
- Pasta de instalação %LOCALAPPDATA%\Programs\OrbyLauncher e dados em %LOCALAPPDATA%\OrbyLauncher.
- Novo AppId do instalador: B7408A21-58E2-4E3A-A842-84EF85965350.
- AppUserModelID Think.OrbyLauncher no processo e no atalho, com ícone próprio explícito no atalho.
- Inicialização automática, mutexes, pipes, tarefas e pastas protegidas dos auxiliares usam a nova identidade OrbyLauncher.
- Nenhum arquivo, dado ou instalação anterior foi removido nesta etapa. A nova identidade não importa as configurações antigas automaticamente e não substitui a instalação anterior.

O catálogo e os ícones detalhados do próprio Orby priorizam seu PNG incorporado, congelado e compartilhado, em vez da imagem retornada pelo cache do Shell. Isso cobre executáveis, atalhos resolvidos e o AppUserModelID do novo aplicativo.

Configurações do Windows: leitura de ícone grande usa a identidade registrada do pacote, em vez do recurso do SystemSettings.exe. Caso a fonte não disponibilize imagem, há uma engrenagem do conjunto nativo Segoe Fluent Icons / MDL2. Imagens completamente vazias não são promovidas a retângulos opacos e não substituem o ícone de reserva.

Revisão por leitura de código e diff. Sem compilação, testes, type check, instalação ou publicação nesta etapa. O instalador 0.20.0-beta já publicado permanece o anterior; estas alterações ainda precisam ser empacotadas em uma nova entrega.
