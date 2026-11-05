# Revisão gradual do Orby — plano

> Execução na sessão com `superpowers:executing-plans`; revisão independente por leitura. A preferência do usuário proíbe testes e type check, inclusive indiretos. Ela prevalece sobre as etapas TDD das skills.

**Objetivo:** reduzir montagem e consultas repetidas, melhorar o início e corrigir inconsistências de materiais sem alterar recursos e visual.

**Arquitetura:** C#/WPF e composição nativa existentes. App é proprietário dos recursos compartilhados; cada janela mantém seu estado visual e de interação.

**Especificação:** `docs/superpowers/specs/2026-11-05-revisao-geral-orby-design.md`.

## Restrições e foco da revisão

- Sem bibliotecas novas, testes, type check, build, execução ou publicação.
- Preservar múltiplos monitores, jogos já ativos no início, arrasto OLE e sensores autorizados.
- Desligar início automático remove seus registros; falhas do Scheduler não eliminam o fallback.
- Popups repetidos, tema/alto contraste e bindings mantêm o fundo original.
- Operações assíncronas não devem publicar após descarte/suspensão nem prender recursos COM.
- Não alegar ganhos medidos sem medição.

## Tarefas

- [x] 1. Inicialização/monitores: App.xaml.cs, App.Monitors.cs, DockWindow.cs e ApplicationCatalog.cs. Criar só as janelas selecionadas; um catálogo e seus observadores; suspender/destruir no proprietário. Adiar preparação secundária até a interface estar montada, preservando início da coleta.
- [x] 2. Logon: SystemActions.cs, StartupPreparation.cs, novo StartupRegistration.cs e limpeza do instalador. Registrar tarefa limitada sem atraso e prioridade normal; manter fallback Run e preferência existente. Remover classe vazia de prioridade. Revisar pela documentação oficial e código, sem alterar registros do PC durante o desenvolvimento.
- [x] 3. Acrylic/tema: PopupBackdrop.cs e ThemeService.cs. Abertura idempotente, restauração do fallback e dependências completas do cache. Inspecionar minis e reduzir redimensionamento apenas se o conteúdo/posição continuarem corretos.
- [x] 4. Recursos/mídia: MediaService.cs, MediaCard.Volume.cs e dependências demonstradas pela auditoria. Evitar metadados repetidos, capturar dispositivos indisponíveis e revisar descarte/concorrência. Preservar player persistente e chamadas.
- [x] 5. Revisão integral do diff e relatório de evidências, limites e pendências; commits locais específicos com hooks desabilitados. Sem merge/push/instalação.

## Registro

- 05/11: abordagem gradual aprovada; branch criada da base publicada. Auditorias independentes somente por leitura para materiais e recursos.
- Regra: não criar outro worktree — a branch própria no diretório atual já foi escolhida na conversa; não mover arquivos não rastreados.
- Regra: verificação por leitura substitui a execução de testes/TDD por instrução explícita permanente do usuário. Não confundir revisão com aprovação funcional.
- Execução: mantida arquitetura aprovada. Revisores por leitura identificaram cancelamento propagado como falha, limpeza incompleta de Run e geometria assíncrona do clima; corrigidos antes de versionar.
- Ampliação justificada pela auditoria: busca de abas sem percorrer Document, decode de ícones dimensionado por DPI e lista reutilizada na magnificação. Mesmas funções; nenhum serviço/hook novo.
- Resultado detalhado: `REVISAO-DESEMPENHO-2026-11-05.md`. Nenhuma medição, teste ou compilação nesta etapa.
- Implementação registrada em commits separados de inicialização/segundo plano, runtime/mídia e aparência. Nenhuma alteração preexistente foi incluída.
