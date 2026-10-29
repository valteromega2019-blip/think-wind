# Think Wind

Launcher independente para Windows, sem dependências de código, serviços ou configurações do Think Monitor.

Repositório próprio: https://github.com/valteromega2019-blip/think-wind

Pasta de desenvolvimento: `D:\think-wind`. A branch `main` contém a base inicial e a branch `developer` é a área de desenvolvimento. O histórico deste repositório é independente do Think Monitor.

## Atualização v0.06 — código em desenvolvimento

Bandeja 6 × 4 com favoritos e paginação, calendário mensal aberto no painel completo, gerenciamento WLAN em mini-ilha, seleção real de saída padrão de áudio e refinamento das animações. Botão Iniciar com símbolo vetorial do Windows. Detalhes de uso e limitações em [REFINAMENTOS-0.06.md](REFINAMENTOS-0.06.md).

**Compilação Release da v0.06 realizada posteriormente a pedido do usuário: zero erros e zero avisos.** Executável para teste em `artifacts/preview-v0.06/ThinkWind.exe`. Testes, type check separado, execução e publish não realizados; nenhum instalador gerado. A cópia anterior em `src/ThinkWind/bin/Release` foi preservada.

Refinamento adicional: bandeja sem redirecionamento automático ao Windows e opção **Sistema → Usar somente a barra do Think Wind**, com ocultação temporária da barra original e auxiliar de restauração. Ícones ocultos não expostos pelo Explorer continuam sujeitos às limitações descritas no documento da v0.06.

## Atualização anterior: v0.05

Menu Iniciar próprio, biblioteca de aplicativos por AppsFolder/atalhos do Windows, pesquisa em painel próprio com Windows Search, bandeja superior em mini-ilha e Drag & Drop OLE. A barra recolhida usa uma faixa visível na borda física inferior, independente do layout expandido, com espera configurável. Detalhes de uso, compatibilidade e limites em [REFINAMENTOS-0.05.md](REFINAMENTOS-0.05.md).

**Compilação Release da v0.05 realizada a pedido do usuário: zero erros e zero avisos. O aplicativo atualizado foi iniciado. Testes, type check separado e publish não foram executados; nenhum instalador foi gerado.** Executável: `src/ThinkWind/bin/Release/net10.0-windows10.0.19041.0/ThinkWind.exe`.

## Atualização anterior: v0.04

Dynamic Island com os três primeiros módulos em destaque e quatro blocos menores abaixo, mídia e volume já no hover, engrenagem no cabeçalho e recolhimento configurável de 0 a 10 segundos, com opção de desativar. Grade responsiva sem paginação dos módulos e transições de posição dos cartões. Detalhes em [REFINAMENTOS-0.04.md](REFINAMENTOS-0.04.md).

**Testes, type check, build e publish não foram executados nesta atualização. Nenhum instalador foi gerado. O executável local permanece na v0.03 e não contém a v0.04.**

## Atualização anterior: v0.03

Barra e ilha sem barras de rolagem, ajuste proporcional e paginação, expansão limitada, mídia integrada às sessões do Windows, bandeja por acessibilidade, controles em blocos e sliders renovados. A personalização mantém sua janela independente e atualiza a aparência sem forçar a expansão da ilha. Detalhes e limites em [REFINAMENTOS-0.03.md](REFINAMENTOS-0.03.md).

**Compilação Release da v0.03 realizada a pedido do usuário: zero erros e zero avisos. Testes e publish não foram executados; nenhum instalador foi gerado.** Executável atualizado: `src/ThinkWind/bin/Release/net10.0-windows10.0.19041.0/ThinkWind.exe`. As verificações do compilador fazem parte do build autorizado; não foi executado type check separado.

## Atualização anterior: v0.21

Correção estrutural da transparência por pixel e do material nativo com `HostBackdropBrush`; ilha com estados compacto, hover e painel completo; mini-ilhas independentes para volume e rede. **29 checks aprovados e build Release sem erros/avisos** no escopo de validação solicitado. A transparência real foi confirmada pelo usuário sobre o fundo colorido de verificação. Detalhes e limites em [VALIDACAO-0.21.md](VALIDACAO-0.21.md).

Executável local dessa etapa: `src/ThinkWind/bin/Release/net10.0-windows10.0.19041.0/ThinkWind.exe`. Nenhum instalador novo foi gerado; o conteúdo de `artifacts` continua na versão antiga.

## Etapa anterior: refinamentos 2.0-dev

O código atual implementa a nova direção visual, ilha presa ao topo, ícones sem fundos, recuperação de ícones pelo Shell/janelas, temas claro/escuro/automático, ajustes independentes, personalização instantânea com salvamento automático e editor visual com arraste, guias, visibilidade e restauração de layouts. Detalhes de uso e limites em [REFINAMENTOS-2.0.md](REFINAMENTOS-2.0.md).

**Esta etapa foi revisada somente por leitura de código e diff. Testes, type check, build e publish não foram executados. Nenhum instalador novo foi gerado.** Os arquivos 0.2.0 citados abaixo são da entrega anterior e não contêm os refinamentos 2.0. O histórico de testes da 0.2.0 não valida a versão em desenvolvimento.

## Versão experimental anterior — 0.2.0

- Dock nativo flutuante: enumeração das janelas visíveis, ícones de executáveis quando acessíveis, indicadores de execução e foco, ativação, restauração e minimização.
- Atalhos persistentes: adicionar executáveis ou `.lnk`, fixar uma janela com executável acessível, mover e remover pelo menu de contexto. Janelas são exibidas individualmente; atalhos `.lnk` ainda não são associados ao processo aberto.
- Botão Iniciar envia a tecla Windows ao sistema, preservando o menu original.
- Ilha com relógio, data, expansão animada, calendário local, rolagem em telas menores e recolhimento ao perder foco ou pressionar Escape.
- Volume percentual e mudo da saída e do microfone por Core Audio (`IAudioEndpointVolume`), com leitura do estado atual e seleção do dispositivo a controlar. A opção inicial acompanha o padrão multimídia do Windows; selecionar outro dispositivo controla seu volume, sem alterar o dispositivo padrão.
- Atualização dos níveis a cada segundo e da lista de dispositivos a cada cinco segundos, somente com a ilha visível, expandida e o módulo habilitado. Desconexões desabilitam o controle afetado e apresentam uma mensagem; a próxima atualização tenta recuperar o acesso. Uma escolha explícita é mantida durante a desconexão, sem redirecionar comandos para outro dispositivo. Alterações de seleção não mudam volume ou mudo por conta própria.
- Controles de mídia por `SendInput`. O aplicativo de mídia ativo decide se aceita esses comandos; não há metadados nem seletor próprio de sessões nesta etapa.
- Acessos às páginas nativas de Wi-Fi, Bluetooth, modo avião, áudio, microfone, notificações e barra de tarefas. São acessos às configurações do Windows, não interruptores diretos nem leitura de notificações ou dos ícones da bandeja.
- Uso real de RAM e bateria quando disponível; coleta da ilha somente expandida.
- Preferências em `%LOCALAPPDATA%\ThinkWind\settings.json`, salvas por substituição de arquivo temporário. Escala, tonalidade do Acrylic, recolhimento automático, módulos áudio/RAM/bateria e seleção de monitor. A seleção temporária do dispositivo de áudio não é persistida; ao reabrir o aplicativo, os controles acompanham o padrão do Windows.
- Inicialização opcional pela chave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, somente ao salvar a opção. Sem privilégios de administrador.
- Ícone na bandeja para personalizar, mostrar e sair; instância única por sessão.

## Arquitetura

`src/ThinkWind` é um aplicativo C# / WPF para .NET 10. `Native` concentra interoperabilidade Win32/DWM/Windows Composition; `Services` contém persistência, transições e ações do sistema; `Views` contém dock, ilha, mini-ilhas e configurações. A integração WinRT usa as projeções oficiais do SDK Windows.

O áudio fica separado em `Native/CoreAudio.cs` (interfaces COM documentadas), `Services/AudioService.cs` (enumeração, leitura e escrita de endpoints) e `Views/AudioEndpointControl.cs` (controle reutilizado para saída e entrada). As referências COM e os valores PROPVARIANT são liberados ao final de cada operação. A leitura de volume do microfone não abre um fluxo de gravação. O botão de mudo altera o endpoint e pode afetar todos os aplicativos que usam aquele dispositivo; não altera permissões de privacidade do Windows.

Referências de áudio: [MMDevice](https://learn.microsoft.com/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immdeviceenumerator), [EndpointVolume](https://learn.microsoft.com/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume) e [propriedades dos dispositivos](https://learn.microsoft.com/windows/win32/coreaudio/device-properties). A troca de entrada/saída padrão é feita pelo usuário na página nativa de áudio; não é usada a interface privada `IPolicyConfig`.

O Acrylic usa `DwmSetWindowAttribute` com `DWMSBT_TRANSIENTWINDOW`, documentado para Windows 11 build 22621 ou superior. A janela usa fundo sólido se a API não estiver disponível. A aparência também depende das opções de transparência/acessibilidade do Windows.

Referência: https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type

O manifesto solicita DPI por monitor V2. A seleção de monitor e os eventos de DPI/topologia reposicionam as janelas; a experiência em monitores com escalas diferentes ainda precisa de verificação em execução. A barra ocupa a área útil acima da barra original e não reserva espaço de trabalho via AppBar.

## Execução e empacotamento

Para usar a versão experimental: Windows 11 x64. O executável distribuído incorpora o runtime .NET; não exige instalação separada do .NET. Para desenvolver: SDK .NET 10 com ferramentas de desktop e, para gerar o instalador, Inno Setup 6.

Em 29/10/2026, o usuário autorizou os testes e a entrega da primeira versão. A compilação Release, os 18 checks automatizados, o publish independente e a geração do instalador foram executados com sucesso. Essa autorização vale para esta entrega; execuções futuras continuam dependendo de pedido explícito, conforme `AGENTS.md`. Veja [TESTES.md](TESTES.md) para os resultados e limites.

```powershell
dotnet run --project '.\src\ThinkWind\ThinkWind.csproj'
dotnet publish '.\src\ThinkWind\ThinkWind.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o '.\artifacts\publish'
ISCC.exe '.\installer\ThinkWind.iss'
```

O instalador desta entrega está em `artifacts/installer/ThinkWind-Setup-0.2.0.exe`. Alternativamente, execute `artifacts/publish/ThinkWind.exe` sem instalar. São artefatos locais, ainda sem assinatura digital nem publicação de release no GitHub.

O script Inno Setup prepara um instalador por usuário e remove a entrada de inicialização ao desinstalar. Atualizações manuais podem usar o mesmo AppId, com o aplicativo encerrado. As configurações pessoais são preservadas na desinstalação. A instalação e a desinstalação completas ainda não foram verificadas nesta entrega.

## Verificações e recursos ainda pendentes

Os resultados de compilação e checks acima pertencem à 0.2.0. Os refinamentos 2.0 estão implementados em código e ainda precisam de verificação solicitada pelo usuário. O aplicativo não representa todos os recursos do produto final.

1. Ampliar a verificação em Windows 11: DPI misto, múltiplos monitores, acessibilidade, instalação/desinstalação e todas as ações interativas.
2. Ocultação reversível da barra original com processo supervisor e restauração após falhas. Nesta etapa ela permanece visível; o launcher não altera o Explorer.
3. Sessões de mídia com metadados; conectividade com estado real e permissões apropriadas. A integração de volume e mudo foi verificada nos dispositivos locais por leitura e regravação dos valores atuais, sem alterar o áudio do usuário.
4. Notificações com consentimento, integração da bandeja respeitando limitações oficiais, CPU/GPU/rede e atalhos personalizados.
5. Verificar os novos temas, materiais, recortes, salvamento automático e editor de layout. Perfis nomeados e múltiplas barras simultâneas continuam pendentes; a ilha permanece ancorada ao topo e permite deslocamento horizontal.
6. Atualizador com manifesto autenticado, assinatura e verificação de integridade do pacote, rollback e distribuição assinada. Não baixar nem executar atualizações sem essa cadeia de confiança.

## Segurança e restauração

Não há substituição do shell, injeção no Explorer, alteração permanente da barra original ou telemetria. Sair pelo ícone da bandeja encerra as janelas. Os únicos dados persistentes são as preferências e, quando selecionada pelo usuário, a entrada de inicialização. Atalhos são executados pela associação padrão do Windows; adicione apenas arquivos confiáveis.
