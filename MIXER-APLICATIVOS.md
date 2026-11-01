# Mixer por aplicativo

Na mini-ilha aberta pelo ícone de volume, a aba **Dispositivos** mantém saída, microfone e seleção de dispositivos. A nova aba **Aplicativos** lista sessões de reprodução ativas e sessões silenciadas que ainda existem, incluindo os sons do Windows.

Usa Core Audio oficial: IAudioSessionManager2, IAudioSessionEnumerator, IAudioSessionControl2 e ISimpleAudioVolume. Consulta todas as saídas ativas, agrupa sessões do mesmo executável quando sua identidade pode ser recuperada e oferece slider 0–100% e botão de mudo. Sem caminho acessível, agrupa pelo PID. Cada alteração é direcionada ao par dispositivo + identificador de instância da sessão; não usa o volume geral, não abre streams de captura e não encerra processos.

Se houver várias sessões de um aplicativo, o valor apresentado é o maior volume entre elas e mudo significa que todas estão silenciadas. Mover o slider ou usar mudo altera as sessões listadas desse grupo. O tooltip informa a quantidade de sessões. Navegadores podem agrupar várias abas no mesmo aplicativo; não se promete volume por aba. Sessões inativas não silenciadas e sessões expiradas não aparecem. “Ativa” é o estado informado pelo Windows e pode incluir streams temporariamente silenciosos; não é análise do conteúdo de áudio.

Atualização pela cadência já existente da mini-ilha, limitada a 1,5 segundo. Consulta apenas na aba Aplicativos visível; operações COM fora do Dispatcher e referências liberadas no mesmo contexto. Escritas são coalescidas por aplicativo em ciclos de 120 ms. O mixer cancela solicitações pendentes ao fechar ou trocar de aba, não fica monitorando em jogos nem em segundo plano. O sistema Windows mantém a política de persistência dos volumes; o launcher não grava valores no Registro por conta própria.

Arquivos: Native/AudioSessions.cs, Services/AppAudioMixer.cs, Views/AppMixerControl.cs e Views/MiniIslandWindow.cs.

Revisão por leitura e diff. Nenhuma compilação, teste ou type check executado nesta solicitação. Não foram alterados volumes deste computador para validar o código. Novas sessões são observadas por enumeração periódica enquanto o painel está aberto, não por captura de áudio; sessões muito breves entre consultas podem não ser mostradas. APIs e hardware podem recusar sessões protegidas ou dispositivos desconectados.

Referências: [IAudioSessionManager2](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessionmanager2), [ISimpleAudioVolume](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nn-audioclient-isimpleaudiovolume).
