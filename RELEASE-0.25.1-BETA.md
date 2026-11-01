# Orby Launcher 0.25.1 Beta

Correção do bloqueio na instalação de atualizações Beta.

- Pacotes oficiais agora incluem uma assinatura RSA-3072/SHA-256 separada (.exe.sig), vinculada à tag e ao hash do instalador.
- A chave pública acompanha o launcher. A chave privada não acompanha o instalador nem o repositório.
- O cliente confere origem, digest da API do GitHub e assinatura. O auxiliar repete a validação antes de permitir o encerramento do launcher.
- O caminho Authenticode continua disponível para distribuições com certificado válido. Assinaturas inválidas não são aceitas como ausência de assinatura.
- Downloads e instalação continuam dependendo de autorização explícita. Configurações e aplicativos fixados são preservados.
- Inclui o otimizador, tutorial e demais melhorias da 0.25.0 Beta.

## Instalação inicial desta correção

Se a versão atual mostra "Atualização integrada bloqueada", baixe e execute este instalador manualmente uma vez. A lógica antiga não consegue atualizar a si mesma por este novo mecanismo. O novo cliente poderá autenticar os próximos pacotes oficiais que incluam a assinatura correspondente.

A assinatura separada não é um certificado Authenticode e não remove avisos de editor desconhecido ou SmartScreen do Windows. Não desative as proteções do sistema.

Compilação e empacotamento desta entrega são feitos em Release x64 com runtime incluído e PawnIO oficial opcional. Testes, lint e type check adicionais não foram solicitados e não são executados. O fluxo completo de atualização entre instalações ainda precisa de validação interativa.
