# Prévias — controles gerais por aplicativo

Branch: feature/previas-controles-gerais, criada a partir do desenvolvimento
atual da iconografia, preservando as alterações anteriores.

Quando o grupo possui pelo menos duas janelas válidas, o painel de miniaturas
normais e o leque do navegador mostram Minimizar todas e Fechar todas abaixo.
As ações usam todas as janelas do grupo, inclusive de outras páginas de prévias,
não as abas individualmente. O grupo é capturado no clique, eliminando HWNDs
repetidos; não são incluídas novas janelas abertas depois desse clique.

Minimizar usa ShowWindowAsync, sem trazer cada janela para primeiro plano.
Fechar solicita WM_CLOSE, preservando diálogos de documentos não salvos. O
auxiliar administrativo existente é usado quando necessário; negar permissão
não encerra o processo à força. Bloqueio de concorrência e interação mantém o
painel durante a ação; ao terminar, ele recolhe. Falhas são registradas e
informadas, sem afirmar que documentos com confirmação foram encerrados.

Controles individuais preservados, materiais e recursos do tema reutilizados.
Não foram executados compilação, testes ou type check nesta alteração. Nenhuma
janela de usuário foi minimizada ou fechada durante o desenvolvimento. Sem
instalador ou publicação; validação prática permanece pendente.

## Atualização após encerramento

O ciclo já existente de 100 ms verifica apenas os HWNDs em cache para descartar
janelas encerradas. O Dock remove entradas sem janelas, mantém fixados, atualiza
os estados dos símbolos e solicita atualização da lista normal. Prévias abertas
retiram janelas inexistentes sem aguardar a leitura completa do navegador.
Sem novos timers ou enumerações de processos nesse caminho; suspensão do ciclo
em jogos continua preservada.

Fechamento individual e geral aguardam brevemente o pedido nativo. Se houver um
popup visível pertencente à janela, a ativação existente traz o diálogo para
frente; a janela permanece no grupo enquanto existir. Não se conclui que toda
janela lenta seja uma solicitação de salvar e não se força encerramento.
Diálogos personalizados que não sejam janelas nativas pertencentes podem não
ser detectados automaticamente. Nenhum teste, type check ou build foi executado
nesta revisão. Nenhuma janela do usuário foi fechada durante a implementação.

## Refinamento minimalista

Controles gerais mostram somente os símbolos de minimizar e fechar, sem rótulo
visível ou superfície Acrylic envolvendo a linha. Botões transparentes mantêm
área de clique, tooltip e nome acessível. Comandos individuais e ações gerais
não mudaram. Sem compilação, testes ou type check nesta alteração.
