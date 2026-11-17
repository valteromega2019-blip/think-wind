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
