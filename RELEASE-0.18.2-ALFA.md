# Think Wind 0.18 Alfa — revisão 2

- Restaura o arrastar e soltar no Dock para arquivos do Explorador e conteúdo de navegadores: expansão, ativação de janelas e abertura de arquivos por aplicativos compatíveis.
- Interface permanece no nível de permissões do desktop. Se iniciada como administrador, é reaberta automaticamente no contexto normal da mesma conta; sensores e ações administrativas continuam nos auxiliares separados. UAC e proteções do Windows permanecem ativos.
- Corrige o início do coletor administrativo: o parâmetro sensors/frames é enviado como string aceita pelo Agendador de Tarefas. Falhas do coletor passam a ser informadas no painel e no overlay.
- Une os ícones da área expandida em uma única linha, mantendo as preferências de exibição e o botão de recolher no final.

## Instalação

Instalador Windows x64 com runtime e PawnIO oficial 2.2.0 opcional. Atualiza preservando preferências. A atualização do coletor protegido pode solicitar UAC. O driver compartilhado não é removido pelo Think Wind. O manifesto da interface continua asInvoker; não é necessário executar a barra como administrador para receber arquivos.

## Validação e limites

A prévia desta branch compilou com zero erros/avisos e o usuário confirmou o funcionamento após a correção do coletor. O pacote desta revisão foi recompilado, sem nova suíte de testes ou type check separado. Os 47 testes registrados anteriormente pertencem à rodada inicial da 0.18 e não validam especificamente todas as mudanças desta revisão.

Arrastar para aplicativos de destino elevados continua sujeito às regras do Windows. Entrega de arquivos depende da compatibilidade do programa. FPS/overlay dependem dos eventos gráficos e do modo de exibição; não houve novas medições de latência ou FPS nesta publicação. O instalador Think Wind não tem assinatura digital. Compare o SHA-256 publicado junto do instalador.