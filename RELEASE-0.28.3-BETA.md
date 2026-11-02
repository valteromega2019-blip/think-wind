# Orby Launcher 0.28.3 Beta

- Atualização integrada passa a executar o instalador com janela visível de progresso e indicação das etapas reais, em vez de deixá-lo completamente oculto após fechar o launcher.
- Modo de atualização com título e explicação próprios: configurações são preservadas e o Orby é reaberto ao finalizar. Mensagem de finalização durante a preparação dos componentes de sensores, traduzida nos sete idiomas do instalador.
- Mantidos a autorização explícita para baixar/instalar, a validação de integridade e publicador, os registros de falha e a opção de não reiniciar o Windows automaticamente.
- Preservadas as melhorias de sessão do overlay, suspensão geral de leituras e editor com prévia real na tela da 0.28.2.

## Limite da primeira atualização

O modo de execução do instalador é escolhido pelo auxiliar da versão já instalada. A atualização de uma versão antiga para esta ainda pode ocorrer sem janela de progresso; depois de instalar a 0.28.3, as próximas atualizações usarão o novo fluxo visível. A instalação manual permite receber a correção diretamente.

## Validação e distribuição

Compilação Release e empacotamento concluídos. Não foram executados novos testes ou type check, conforme preferência do usuário. A janela e suas mensagens ainda exigem conferência durante uma atualização real. A barra mostra o progresso fornecido pelo instalador, não uma estimativa inventada do tempo total.

Instalador x64 com runtime incluído e PawnIO oficial opcional. Preferências e aplicativos fixados são preservados. O pacote inclui SHA-256 e assinatura RSA separada para autenticação no atualizador do Orby. Não é certificado Authenticode e não remove avisos de editor desconhecido do Windows.