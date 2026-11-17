# Orby Launcher 0.31.3 Beta

Correção de “Atualizar e desligar” do Windows sobre a versão publicada 0.31.2 Beta.

- Atualizações já instaladas que aguardam reinicialização são encaminhadas ao mecanismo nativo de atualizar e desligar, em vez de serem recusadas como uma lista vazia de downloads.
- Uma instalação bem-sucedida que exige conclusão pelo Windows deixa de ser tratada como falha. A solicitação mantém a opção nativa de instalar atualizações antes de desligar.
- O detector e o instalador usam o mesmo filtro de atualizações de software. Falhas de consulta ficam registradas no diagnóstico.
- Falhas ao iniciar o auxiliar de atualização são informadas ao usuário.
- Preservados configurações, personalizações e recursos anteriores. Nenhuma integração experimental com Explorer/Windhawk ou Google Home foi incluída.

## Validação

Compilação Release sem erros nem avisos. Na revisão desta correção, passaram 61 testes gerais e 10 testes do atualizador de versões do Orby: 71 aprovados, nenhuma falha. Os testes do atualizador do Orby não validam a instalação de atualizações do Windows. Não foi executada instalação real de atualizações, suspensão, reinicialização ou desligamento do PC; a conclusão desse fluxo ainda depende de validação prática e do estado do Windows Update.

Instalador Windows x64 com sete idiomas e PawnIO oficial opcional. O SHA-256 e a assinatura RSA separada acompanham o instalador para o atualizador integrado. Essa assinatura não é Authenticode e não elimina avisos de editor desconhecido do Windows.
