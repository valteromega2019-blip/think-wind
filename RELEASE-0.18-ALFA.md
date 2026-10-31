# Think Wind 0.18 Alfa

- Dynamic Island secundária desligada por padrão, com opção em Personalizar > Monitores. Iniciar e pesquisa somente na tela principal do Windows.
- Ajustes de abertura de aplicativos Electron, acesso à ilha em vídeos fullscreen, tecla Windows durante jogos/vídeos e foco da pesquisa ao abrir o Iniciar.
- Overlay com Acrylic nativo, temperatura e uso nas linhas originais de CPU/GPU, um pouco maiores e na cor de destaque, escala e posição horizontal/vertical com presets de canto.
- Captura ETW local de FPS quando elevado; coletor separado atualizado para frames-v2 quando executado normalmente. Nenhum FPS é simulado.
- Blocos expandidos, mídia e calendário com medição por conteúdo para reduzir recortes.

## Distribuição

Instalador: artifacts/installer/ThinkWind-Setup-0.18.0-alpha.exe. Inclui runtime x64 e PawnIO oficial 2.2.0 opcional. Preserva preferências e não remove o driver compartilhado ao desinstalar. O coletor atualizado utiliza o fluxo normal de autorização UAC; a interface mantém manifesto asInvoker. A prévia foi aberta como administrador por solicitação do usuário.

## Validação

Na rodada anterior desta versão, 47 verificações foram aprovadas, sem falhas ou ignoradas. Depois disso, foram ajustados o foco nativo da pesquisa e a apresentação das métricas do overlay. Esses ajustes finais foram compilados, mas não tiveram novos testes executados nesta publicação. Detalhes em VALIDACAO-0.18-ALFA.md. Não foram medidos FPS/latência dentro de um jogo nesta rodada; abertura por teclado, Acrylic do overlay sobre jogos e transições fullscreen/multimonitor ainda precisam de confirmação interativa. Nem todos os caminhos gráficos emitem eventos DXGI/D3D9. Tela cheia exclusiva pode não exibir o overlay. Instalador Think Wind sem assinatura digital.

Release pública 0.18 Alfa com instalador e SHA-256. Página de apresentação atualizada para o mesmo download. O código publicado corresponde à tag v0.18.0-alpha na branch de correções.