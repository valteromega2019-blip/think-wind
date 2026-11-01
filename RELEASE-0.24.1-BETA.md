# Orby Launcher 0.24.1 Beta

## Correção do atualizador

- Trata InvalidDataException na validação do instalador e na consulta de releases, evitando que a falha se propague pelo botão de atualização e encerre o launcher sem uma mensagem adequada.
- O launcher aguarda a confirmação de prontidão do auxiliar antes de solicitar seu encerramento. O auxiliar valida o arquivo, confirma sua preparação e aguarda a saída da interface antes de executar o instalador.
- Falhas de validação/instalação ficam registradas em `%LOCALAPPDATA%\OrbyLauncher\Updates\installation-result.json`.
- Ausência do script auxiliar, cancelamento e falha de preparação mantêm a interface aberta. Uma falha posterior tenta reabrir o launcher mesmo se a gravação do log falhar.

## Instalação desta versão

**Use o instalador manual.** Launcher e instaladores publicados continuam sem assinatura digital confiável. A instalação pelo atualizador integrado segue bloqueada por essa política; esta correção não remove a validação de autenticidade nem promete que a atualização automática funciona com os artefatos atuais. É necessária uma versão-base assinada e instaladores com o mesmo certificado confiável para habilitar esse ciclo.

O arquivo SHA-256 publicado permite verificar a integridade do download e não substitui a autenticação do publicador.

Mantém as funções da 0.24 Beta: personalização por categorias e busca, editor do overlay integrado, mixer por aplicativo, controle de brilho WMI/DDC e sinais adicionais para indicadores do Dock. Os limites de hardware, sessão de áudio e badges continuam válidos.

## Validação desta entrega

Compilação e empacotamento solicitados pelo usuário. Nenhum novo teste, lint ou type check separado foi executado. Não foi realizada uma atualização assinada ponta a ponta; o protocolo de prontidão e a instalação precisam dessa validação antes de serem considerados confirmados em uso real.

Detalhes em CORRECAO-ENCERRAMENTO-ATUALIZADOR.md e ATUALIZADOR-GITHUB.md.
