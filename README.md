# Think Wind

Launcher independente para Windows, sem dependências de código, serviços ou configurações do Think Monitor.

Repositório próprio: https://github.com/valteromega2019-blip/think-wind

Pasta de desenvolvimento: `D:\think-wind`. A branch `main` contém a base inicial e a branch `developer` é a área de desenvolvimento. O histórico deste repositório é independente do Think Monitor.

## Implementação atual — 0.2.0

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

`src/ThinkWind` é um aplicativo C# / WPF para .NET 10. `Native` concentra interoperabilidade Win32/DWM; `Services` contém persistência e ações do sistema; `Views` contém dock, ilha e configurações. Não há dependências NuGet externas.

O áudio fica separado em `Native/CoreAudio.cs` (interfaces COM documentadas), `Services/AudioService.cs` (enumeração, leitura e escrita de endpoints) e `Views/AudioEndpointControl.cs` (controle reutilizado para saída e entrada). As referências COM e os valores PROPVARIANT são liberados ao final de cada operação. A leitura de volume do microfone não abre um fluxo de gravação. O botão de mudo altera o endpoint e pode afetar todos os aplicativos que usam aquele dispositivo; não altera permissões de privacidade do Windows.

Referências de áudio: [MMDevice](https://learn.microsoft.com/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immdeviceenumerator), [EndpointVolume](https://learn.microsoft.com/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume) e [propriedades dos dispositivos](https://learn.microsoft.com/windows/win32/coreaudio/device-properties). A troca de entrada/saída padrão é feita pelo usuário na página nativa de áudio; não é usada a interface privada `IPolicyConfig`.

O Acrylic usa `DwmSetWindowAttribute` com `DWMSBT_TRANSIENTWINDOW`, documentado para Windows 11 build 22621 ou superior. A janela usa fundo sólido se a API não estiver disponível. A aparência também depende das opções de transparência/acessibilidade do Windows.

Referência: https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type

O manifesto solicita DPI por monitor V2. A seleção de monitor e os eventos de DPI/topologia reposicionam as janelas; a experiência em monitores com escalas diferentes ainda precisa de verificação em execução. A barra ocupa a área útil acima da barra original e não reserva espaço de trabalho via AppBar.

## Execução e empacotamento

Pré-requisitos: Windows 11, SDK .NET 10 com ferramentas de desktop e, para o instalador, Inno Setup 6. O ambiente de desenvolvimento inspecionado tem o host `dotnet`, mas não apresentou SDK instalado.

Os comandos abaixo compilam o aplicativo. **Não foram executados**: a preferência atual do usuário proíbe testes e type check, inclusive por build, sem solicitação explícita. Eles são apenas documentação para quando essa execução for solicitada.

```powershell
dotnet run --project '.\src\ThinkWind\ThinkWind.csproj'
dotnet publish '.\src\ThinkWind\ThinkWind.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o '.\artifacts\publish'
ISCC.exe '.\installer\ThinkWind.iss'
```

O script Inno Setup prepara um instalador `.exe` por usuário e remove a entrada de inicialização ao desinstalar. Atualizações manuais podem usar o mesmo AppId, com o aplicativo encerrado. Não há executável ou instalador gerado nesta entrega. As configurações pessoais são preservadas na desinstalação.

## Próximas etapas

Esta é a estrutura inicial implementada em código, ainda sem compilação ou execução verificada. Não representa todos os recursos do produto final.

1. Verificação autorizada de compilação e funcionamento no Windows 11, incluindo DPI misto, múltiplos monitores e acessibilidade.
2. Ocultação reversível da barra original com processo supervisor e restauração após falhas. Nesta etapa ela permanece visível; o launcher não altera o Explorer.
3. Sessões de mídia com metadados; conectividade com estado real e permissões apropriadas. Volume e mudo de endpoints já estão implementados em código, aguardando verificação em execução autorizada.
4. Notificações com consentimento, integração da bandeja respeitando limitações oficiais, CPU/GPU/rede e atalhos personalizados.
5. Temas claros/escuros/automáticos, cores, perfis, cantos e posicionamento livre, arrastar atalhos, animação de ocultação e múltiplas barras simultâneas.
6. Atualizador com manifesto autenticado, assinatura e verificação de integridade do pacote, rollback e distribuição assinada. Não baixar nem executar atualizações sem essa cadeia de confiança.

## Segurança e restauração

Não há substituição do shell, injeção no Explorer, alteração permanente da barra original ou telemetria. Sair pelo ícone da bandeja encerra as janelas. Os únicos dados persistentes são as preferências e, quando selecionada pelo usuário, a entrada de inicialização. Atalhos são executados pela associação padrão do Windows; adicione apenas arquivos confiáveis.
