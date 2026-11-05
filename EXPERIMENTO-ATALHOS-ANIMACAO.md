# Teste de atalhos da biblioteca

Branch experiment/atalhos-animacao-janelas. A animação experimental foi removida a pedido do usuário; abertura/restauração/minimização voltaram ao fluxo anterior.

Arrastar um app da Biblioteca para o desktop oferece um .lnk por CF_HDROP padrão, com Copy/Link. Soltar na barra fixa o destino original sem depender de objeto privado serializado. O payload interno é mantido somente durante a operação OLE. A biblioteca não é ocultada pela barra, pelo observador de clique externo ou pelo monitoramento de foco enquanto fornece o arraste. Soltar não executa o app acidentalmente.

Revisão por leitura encontrou conflitos no fechamento da origem e no formato exportado; ajustes aplicados. A correção ainda não foi compilada nem conferida por arraste real. Testes/type check não executados conforme preferência. Nenhum instalador ou publicação.