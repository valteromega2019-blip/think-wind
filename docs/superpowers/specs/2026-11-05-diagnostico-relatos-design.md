# Diagnóstico e relatos do Orby — 05/11/2026

## Objetivo e escolhas aprovadas
Adicionar Personalizar → Diagnóstico e suporte na branch feature/barra-inferior-fixa. O usuário escolheu abrir uma issue preenchida no navegador e confirmar a publicação no GitHub. Não adicionar autenticação, token embutido, servidor ou transmissão automática.

## Alternativas
1. Issue preenchida no navegador (escolhida): usa a conta e confirmação do próprio GitHub, sem credenciais no Orby; exige conexão e conta GitHub para publicar.
2. API diretamente no launcher: exige autenticação e manutenção adicional; fora do escopo escolhido.

## Registro local
Serviço pequeno, baseado em eventos, sem polling, hooks ou coleta contínua. Captura exceções não tratadas do WPF, AppDomain e tarefas não observadas, além de falhas recuperáveis explicitamente registradas nos fluxos já existentes de inicialização e atualização. Registrar não deve engolir exceções fatais nem impedir o encerramento normal do processo.

Registros sanitizados em %LOCALAPPDATA%/OrbyLauncher/Diagnostics, com data, versão, área, tipo e pilha limitada. Rotação limitada a três arquivos de até 512 KiB; mensagens/eventos também limitados. Sem dumps de memória, arquivos pessoais, screenshots, inventário de processos, senhas, tokens ou conteúdo integral de settings.json. Reduzir repetição de falhas idênticas. Falha no próprio diagnóstico nunca deve derrubar o app.

## Interface
Página no grupo Aplicativo usando os componentes e recursos de tema atuais: resumo do registro, atualizar lista sob demanda, descrição do problema, prévia editável, preparar relatório, copiar/salvar TXT e abrir relato no GitHub. Ações de salvar/copiar/enviar são explícitas. A descrição pode conter dados pessoais digitados pelo usuário, por isso a tela explica que a issue será pública e solicita revisão.

## Envio
Destino fixo HTTPS: https://github.com/valteromega2019-blip/think-wind/issues/new. Sanitizar novamente e codificar título/corpo antes de abrir. Por limites práticos de URLs, a issue usa resumo limitado; se o relatório completo não couber, oferecer Copiar relatório completo e Salvar TXT, explicando que o usuário poderá colar/anexar no GitHub. Não afirmar que foi enviado apenas por abrir o navegador. Falhas para abrir o link são apresentadas na própria página sem perda da descrição.

## Proteção de dados
Substituir usuário, diretórios de perfil e nome da máquina por marcadores; remover parâmetros de URLs, e-mails, padrões de tokens/credenciais e limitar mensagens. Não incluir logs externos arbitrários ou arquivos do sistema automaticamente. Sanitização reduz exposição, mas não garante remover todo dado pessoal; manter prévia e revisão obrigatórias pelo usuário.

## Integração e limites
Adicionar serviço em Services, página parcial SettingsWindow e entrada na navegação. Conectar handlers cedo na inicialização e removê-los ao sair. Manter visual, recursos atuais e Modo Jogo; sem balões durante jogos nem novos serviços residentes. Acrescentar uma etapa ao mini tutorial de novidades para apresentar a área. Falhas nativas graves, encerramento forçado e queda de energia podem não produzir registro.

## Validação e entrega
Nesta etapa, somente revisão de código e diff; nenhum teste, type check, build ou instalador sem pedido explícito. Quando testes forem solicitados, validar sanitização, limites/rotação, título e URL, ausência de envio automático, falha de I/O e persistência do texto. Qualquer teste com falha bloqueia instalador e publicação. Nenhuma publicação automática de issue durante o desenvolvimento.
Decisão de implementação: auxiliares elevados não escrevem no histórico controlado pelo usuário. Falhas da comunicação com os coletores são registradas pela interface normal. O título da issue é fixo por versão e não reaproveita texto privado removido da prévia.
