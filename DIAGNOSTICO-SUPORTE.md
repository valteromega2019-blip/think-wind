# Diagnóstico e suporte

Disponível em Personalizar → Aplicativo → Diagnóstico e suporte.

1. Use Atualizar registros para consultar até 40 eventos recentes (12 resumos visíveis).
2. Descreva o problema e clique em Preparar relatório.
3. Revise e, se necessário, edite a prévia. Ao confirmar, a sanitização é aplicada novamente; se houver mudança, será preciso revisar e confirmar de novo.
4. Copie ou salve um TXT, ou use Abrir relato no GitHub. O botão abre um rascunho no navegador: somente sua confirmação no GitHub publica a issue. É necessária uma conta GitHub.

Relatórios grandes usam um resumo no link; use Copiar relatório ou Salvar TXT para incluir o restante pelo GitHub. A área não envia arquivos ou solicitações HTTP em segundo plano.

O histórico é local, limitado a três arquivos de 512 KiB em %LOCALAPPDATA%/OrbyLauncher/Diagnostics. Falhas idênticas dentro de um minuto são reduzidas. Caminhos, perfil, máquina, e-mails e padrões comuns de credenciais são omitidos. Isso não substitui sua revisão: as issues são públicas.

Captura exceções WPF/AppDomain e tarefas não observadas, além de erros instrumentados de inicialização, atualizador, cliente de sensores e descoberta Bluetooth. Não engole exceções fatais nem garante captura de falhas nativas, término forçado ou queda de energia. Não coleta screenshots, inventário de processos, arquivos pessoais, dumps de memória ou configurações completas. Helpers elevados não gravam nesses arquivos; a interface registra falhas de comunicação com eles.

A descrição e a prévia são mantidas na memória enquanto Personalizar está aberta, inclusive em reconstruções de tema. Não são salvas automaticamente. O mini tutorial inclui acesso a esta área; tutoriais já concluídos não são reiniciados automaticamente.

Implementação ainda não compilada nem validada em execução. Testes/type check não executados por preferência do usuário. Nenhum instalador ou envio de relato foi realizado.