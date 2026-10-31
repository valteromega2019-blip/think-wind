# Think Wind 0.18 Alfa

- Dynamic Island secundária desligada por padrão, com opção em Personalizar > Monitores. Iniciar e pesquisa somente na tela principal do Windows.
- Ajustes de abertura de aplicativos Electron, acesso à ilha em vídeos fullscreen, tecla Windows durante jogos/vídeos e foco da pesquisa ao abrir o Iniciar.
- Overlay com Acrylic nativo, temperaturas de CPU/GPU no topo, uso com o mesmo destaque, escala e posição horizontal/vertical com presets de canto.
- Captura ETW local de FPS quando elevado; coletor separado atualizado para frames-v2 quando executado normalmente. Nenhum FPS é simulado.
- Blocos expandidos, mídia e calendário com medição por conteúdo para reduzir recortes.

## Entrega local

Instalador: artifacts/installer/ThinkWind-Setup-0.18.0-alpha.exe. Inclui runtime x64 e PawnIO oficial 2.2.0 opcional. Preserva preferências e não remove o driver compartilhado ao desinstalar. O coletor atualizado utiliza o fluxo normal de autorização UAC; a interface mantém manifesto asInvoker. A prévia foi aberta como administrador por solicitação do usuário.

## Validação

47 verificações aprovadas, zero falhas e zero ignoradas. Detalhes em VALIDACAO-0.18-ALFA.md. Não foram medidos FPS/latência dentro de um jogo nesta rodada; abertura por teclado, Acrylic do overlay sobre jogos e transições fullscreen/multimonitor ainda precisam de confirmação interativa. Nem todos os caminhos gráficos emitem eventos DXGI/D3D9. Tela cheia exclusiva pode não exibir o overlay. Instalador Think Wind sem assinatura digital.

Esta entrega gera o instalador local. Publicação no GitHub e na página não foi solicitada nesta etapa.