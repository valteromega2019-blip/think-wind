# Encerramento durante tentativa de atualização

## Investigação

A versão instalada encontrada neste computador é 0.23.0-beta. Não havia installation-result.json ou setup.log na pasta Updates durante a inspeção, apenas o estado de notificação. Não foi reproduzida uma nova instalação para investigar.

Foi identificado um caminho de falha no código: AuthenticateAsync e a verificação de integridade lançam System.IO.InvalidDataException. Essa exceção deriva de SystemException, não de IOException, e não estava incluída no filtro do UpdateController. Sua propagação pelo handler assíncrono do botão pode encerrar o aplicativo antes de mostrar o erro e sem iniciar o instalador. A ausência de assinatura nos artefatos publicados fornece um gatilho desse caminho. Isso é evidência de código, não confirmação por rastreamento de uma execução real do problema relatado.

## Correções

- InvalidDataException tratada tanto na consulta de releases quanto no download/validação. A interface permanece aberta com o erro em vez de propagar a falha de validação.
- Falhas de instalação/validação registradas em `%LOCALAPPDATA%\OrbyLauncher\Updates\installation-result.json`, sem depender de o auxiliar chegar a executar o setup.
- Verificação explícita da existência do script auxiliar.
- Novo protocolo de prontidão: o auxiliar abre e bloqueia o instalador contra escrita/exclusão, valida hash/assinaturas e publica um arquivo de confirmação atomicamente. O launcher só solicita seu encerramento depois dessa confirmação, dentro de um limite de espera. Se o auxiliar falhar, não confirmar, a operação for cancelada ou um jogo/tela cheia entrar em primeiro plano, o launcher não fecha e interrompe o auxiliar que ainda estava aguardando.
- Depois da confirmação, o auxiliar aguarda a saída do launcher e só então executa o instalador. Uma falha ao escrever o log de erro não impede a tentativa de reabrir o launcher após uma falha posterior ao encerramento.

## Limite preservado

Não foi removida a validação de autenticidade. Launcher e instaladores 0.23/0.24 publicados não possuem assinatura digital confiável; portanto, a atualização integrada continua recusada nesse cenário, agora com erro tratado. Para essas versões, a instalação é manual. Uma base assinada e instaladores assinados com o mesmo certificado confiável continuam necessários para habilitar a instalação integrada com essa política.

A correção está somente no código da branch `fix/brilho-notebook`, não nas releases já publicadas nem no executável atualmente aberto. Não houve compilação, teste, type check, instalação ou publicação nesta solicitação. Revisão por leitura do código e diff; o protocolo de prontidão e o ciclo de instalação assinada ainda precisam de validação executada quando solicitada.

Arquivos: Services/UpdateController.cs, Services/GithubUpdates.cs e UpdateInstaller.ps1.
