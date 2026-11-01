# Sincronização do fundo durante transições

O vídeo `2026-11-01 19-27-51.mp4` foi aberto por MediaElement e inspecionado por quadros extraídos. Os quadros mostram os estados compacto, painel de monitoramento e painel principal. Essa inspeção não mede o custo da composição nem permite afirmar FPS ou eliminar o stutter sem conferir a nova execução.

O código movia a janela WPF por SetWindowPos e, em seguida, reposicionava o HWND do material separadamente. Isso deixava as duas superfícies sujeitas a atualizações intermediárias distintas. A correção calcula o recorte com a geometria de destino e usa BeginDeferWindowPos/DeferWindowPos/EndDeferWindowPos para aplicar os dois retângulos em um mesmo lote nativo.

Preservados o layout interno fixo no tamanho final, duração, curva e cadência de renderização existentes. Não foram adicionados polling, prioridade elevada, FPS fixo nem DwmFlush bloqueante. Regiões nativas e dimensão do visual de composição ainda são atualizadas durante o crescimento; portanto, esta mudança reduz uma fonte de dessincronização, mas não equivale a migrar toda a transição para uma animação exclusiva da GPU. Falha no lote utiliza o posicionamento anterior como fallback.

Arquivos: Views/FloatingWindow.cs e Native/BackdropHost.cs.

Revisão somente por leitura/diff. Nenhuma compilação, teste, type check ou medição de desempenho executados nesta solicitação. A fluidez precisa ser conferida na próxima prévia; não está declarada resolvida sem essa validação.
