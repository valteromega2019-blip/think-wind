# Diagnóstico e relatos — plano de implementação

> Execução: superpowers:executing-plans, na branch atual aprovada pelo usuário. Implementação nesta sessão; revisão independente final por leitura. A instrução do usuário proíbe executar testes/type check sem novo pedido.

**Objetivo:** registrar falhas locais e preparar issues revisáveis no GitHub.
**Arquitetura:** sanitizador + registro limitado + construtor de relatório; integração por eventos no App e página parcial nas configurações, sem dependências novas.
**Tecnologias:** C#/.NET 10, WPF, System.Text.Json, Regex com timeout, abertura HTTPS via navegador padrão.
**Spec:** docs/superpowers/specs/2026-11-05-diagnostico-relatos-design.md (aprovada).

## Restrições
- Sem envio automático, credenciais, hooks, polling, dump de memória ou inventário de processos.
- Três arquivos de até 512 KiB; eventos e pilhas limitados, deduplicação limitada em memória.
- URL HTTPS fixa, no máximo 1800 caracteres; relatório completo pode ser copiado/salvo.
- Sem testes, type check, build, instalador, publicação ou issue real nesta etapa.

## Pontos de revisão
Dados sensíveis em mensagens/pilhas; dados corrompidos/preexistentes; disco sem permissão/espaço; caracteres e URLs extensos; fechamento da tela durante operações. Nenhum deles pode provocar envio automático, travar o launcher ou perder a descrição digitada.

## Tarefa 1 — Diagnóstico local
- [x] Criar Services/DiagnosticSanitizer.cs: Redact(string?), truncamento, remoção de perfil/máquina/credenciais/URLs privadas.
- [x] Criar Services/DiagnosticLog.cs: Record(string, Exception, bool), Snapshot() e leitura segura, rotação, deduplicação e proteção best-effort.
- [x] Criar App.Diagnostics.cs: handlers WPF/AppDomain/TaskScheduler, sem marcar exceções fatais como tratadas; remover ao sair. Instrumentar falhas recuperáveis de startup/sensores/atualizador.
- [x] Revisar manualmente limites de I/O, concorrência, handlers e exposição de dados; commit localizado.

## Tarefa 2 — Relatório e tela
- [x] Criar Services/DiagnosticReport.cs: Build(description, entries), CreateIssue(title, report) com URL limitada e indicação de resumo.
- [x] Criar Views/SettingsWindow.Diagnostics.cs: lista sob demanda, descrição e prévia editável, copiar/salvar TXT, consentimento de revisão e abrir GitHub. Reutilizar tema e scroll atuais; trabalho de disco fora do dispatcher; feedback local e revisão inválida após editar.
- [x] Integrar navegação Aplicativo e eventos do tutorial; acrescentar etapa ao mini tutorial, mantendo progresso já concluído.
- [x] Revisar diff, dependências e lifecycle; revisão independente final por leitura e commit. Não afirmar validação em execução.

## Validação futura (somente quando solicitada)
Sanitização de usuário/tokens/e-mails/caminhos; limites e rotação; concorrência e I/O negado; URL longa/unicode; nenhuma requisição HTTP automática; handlers não impedem encerramento fatal; navegação/tema/teclado; persistência do texto ao falhar abertura. Falhas bloqueiam instalador/publicação.
## Registro da execução
- Implementação concluída na branch feature/barra-inferior-fixa; sem dependências novas.
- Ruling: manter um título fixo da issue para não republicar dados removidos da prévia. É intencional que a descrição não vire título automaticamente.
- Ruling: helpers administrativos não gravam neste diretório controlado pelo usuário; falhas de comunicação são registradas na interface normal, evitando escritas privilegiadas em hard links.
- Revisão independente por leitura identificou dois problemas (título derivado da descrição e credenciais em JSON); ambos ajustados e relidos sem novos achados concretos.
- A consulta de configuração confirmou repositório público com Issues habilitadas. Nenhuma issue foi criada.
- Testes, type check, lint e build não executados conforme preferência. Revisão de diff apenas. Sem instalador, instalação ou publicação.
- Bluetooth da entrega anterior continua sem nova validação. A regra de bloquear instalador diante de falhas permanece em vigor.