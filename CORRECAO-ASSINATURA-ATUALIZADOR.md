# Correção da autenticação do atualizador Beta

Causa confirmada por leitura: UpdateInstaller.ps1 exigia que o launcher atual tivesse Authenticode Valid e o instalador tivesse o mesmo certificado. Todos os binários Orby publicados são NotSigned. O SHA-256 passava, mas essa política tornava impossível a instalação integrada, independentemente da integridade do download.

Implementado caminho de autenticação por assinatura própria: chave pública RSA-3072 embutida no launcher; assinatura separada de domínio/tag/SHA-256 nos pacotes oficiais, conferida pelo cliente e novamente pelo auxiliar antes de prontidão/saída da interface. O caminho Authenticode permanece para launcher assinado e exige mesmo certificado. Não são aceitos estados inválidos, revogados ou adulterados como se fossem NotSigned. SHA-256 isolado não autoriza a execução.

A chave privada foi criada com proteção DPAPI CurrentUser, fora do repositório, em OrbyPublisher, com ACL restrita à conta e SYSTEM. Não foi impressa nem adicionada ao Git. tools/Sign-Update.ps1 permite assinar futuras publicações, recusando chave pública divergente. Artefatos `.sig` devem acompanhar a release. O tamanho e digest da assinatura também são conferidos contra a API. A assinatura vincula a tag exata, evitando reaproveitar uma prova sob outra tag.

Limites: isso não fornece certificado Authenticode, reputação SmartScreen ou remoção de avisos de editor desconhecido. Uma versão instalada antiga não pode aprender esta validação sem instalação manual inicial. Não houve recompilação, nova release ou execução do instalador nesta tarefa. Não foram executados testes/type check, conforme preferência permanente. Validação ponta a ponta e fluxos de recusa continuam pendentes de solicitação de testes.

Arquivos: GithubUpdates.cs, UpdateController.cs, UpdateInstaller.ps1, SettingsWindow.Updates.cs, ThinkWind.csproj, Assets/UpdatePublicKey.xml, tools/Sign-Update.ps1, SECURITY.md e installer/README.md.
