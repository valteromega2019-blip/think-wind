# Think Wind

## 0.015 Alfa — revisão 1 (0.15.1-alpha)

[Baixar instalador](https://github.com/valteromega2019-blip/think-wind/releases/download/v0.15.1-alpha/ThinkWind-Setup-0.15.1-alpha.exe) · [Release](https://github.com/valteromega2019-blip/think-wind/releases/tag/v0.15.1-alpha)

Associação de fixados por identidade nativa da janela e do atalho, com resolução AppsFolder/.lnk em segundo plano. **44 testes aprovados, zero falhas/ignorados**, incluindo regressão do novo mecanismo. Compilação e verificação de tipos sem erros ou avisos; instalador gerado. Confirmação visual final em todos os perfis do Edge ainda pendente. [Relatório e limites](VALIDACAO-0.15.1.md).


## 0.015 Alfa — 0.15.0-alpha

[Baixar instalador](https://github.com/valteromega2019-blip/think-wind/releases/download/v0.15.0-alpha/ThinkWind-Setup-0.15.0-alpha.exe) · [Notas da versão](https://github.com/valteromega2019-blip/think-wind/releases/tag/v0.15.0-alpha)

Inclui ajustes do alinhamento e associação de fixados no Dock, permanência da ilha no modo superior com janelas menores à frente, convivência com Think Monitor, tema claro/escuro do launcher e Windows, controle de fontes de 80% a 130% e configuração automática das barras durante a instalação.

Distribuição Release independente e instalador gerados. **Não foram executados novos testes nem type check separado nesta entrega**. Alterações recentes de fontes e tema precisam de confirmação interativa; limitações Bluetooth anteriores permanecem. Identificador técnico: `0.15.0-alpha`; nome de apresentação solicitado: **0.015 Alfa**.

## 0.11 Alfa — 0.11.0-alpha

[Baixar instalador](https://github.com/valteromega2019-blip/think-wind/releases/download/v0.11.0-alpha/ThinkWind-Setup-0.11.0-alpha.exe) · [Notas da versão](https://github.com/valteromega2019-blip/think-wind/releases/tag/v0.11.0-alpha)

Modo Jogo com reconhecimento por instalações Steam, Epic e GOG, inclusive em janela. Lista manual opcional e proteção ampla configurável para aplicativos não reconhecidos. A proteção ampla também oculta o launcher em aplicativos comuns fora das exceções. Jogos de outras origens podem precisar dessa opção ou da lista complementar; identificação por pasta pode incluir software instalado nas mesmas bibliotecas.

Distribuição Release e instalador gerados a pedido do usuário. Sem novos testes ou type check separado nesta entrega; resultados da 0.10 abaixo são históricos.

## 0.10 Alfa — 0.10.0-alpha

[Baixar instalador](https://github.com/valteromega2019-blip/think-wind/releases/download/v0.10.0-alpha/ThinkWind-Setup-0.10.0-alpha.exe) · [Notas da versão](https://github.com/valteromega2019-blip/think-wind/releases/tag/v0.10.0-alpha)

Modo Jogo automático, Dock adaptativo, miniaturas DWM, áudio e microfone sincronizados com o Windows, clima configurável e melhorias da Dynamic Island em múltiplos monitores. A tecla Windows isolada abre o Iniciar do launcher por um hook em thread dedicada, suspenso durante o Modo Jogo. Corrigida a lógica de fechamento prematuro do Iniciar por perda de foco.

Distribuição Release independente compilada e instalador gerado. Bateria atual: **42 aprovados, 1 falha, 0 ignorados**. A consulta de dispositivos Bluetooth excedeu o prazo; pareamento e conexão não foram validados. A correção recente do Iniciar ainda precisa de confirmação interativa. [Resultados e limites](VALIDACAO-0.10.md).

## 0.08 Alfa — 0.8.0-alpha

Correção de recortes em ícones grandes no Dock e controle de opacidade dos blocos/controles internos. **37 checks aprovados**, publicação Release sem erros/avisos e instalador atualizado. [Release pública 0.08 Alfa](https://github.com/valteromega2019-blip/think-wind/releases/tag/v0.8.0-alpha) · [Relatório de validação](VALIDACAO-0.08-ALFA.md).

[Página de apresentação](https://valteromega2019-blip.github.io/think-wind-site/) · [Release anterior 0.07 Alfa](https://github.com/valteromega2019-blip/think-wind/releases/tag/v0.7.0-alpha)

O código e os instaladores deste projeto são públicos. A apresentação continua publicada separadamente em `think-wind-site`.

## 0.07 Alfa — 0.7.0-alpha

Microfone também na primeira expansão da ilha e tecla Windows isolada integrada ao Iniciar do Think Wind enquanto o app estiver em execução. Compilação/verificação de tipos C#/XAML sem erros ou avisos; sem suíte de testes nesta etapa. Instalador solicitado em `artifacts/installer/ThinkWind-Setup-0.7.0-alpha.exe`. [Detalhes e limites](REFINAMENTOS-0.07-ALFA.md).

Launcher independente para Windows, sem dependências de código, serviços ou configurações do Think Monitor.

Repositório próprio: https://github.com/valteromega2019-blip/think-wind

Pasta de desenvolvimento: `D:\think-wind`. A branch `main` contém a versão publicada e a branch `developer` é a área de desenvolvimento. O histórico deste repositório é independente do Think Monitor.

## Atualização v0.06 — código em desenvolvimento

Última validação solicitada: **34 checks aprovados e build Release sem erros/avisos**. Ícones maiores e gaveta com dimensões estáveis ao trocar Fixados/Recentes. Executável: `artifacts/preview-v0.06-validada/ThinkWind.exe`; não iniciado nesta etapa. [Relatório e limites](VALIDACAO-0.06.md).

**Versão refinada compilada e iniciada a pedido do usuário, com zero erros e zero avisos.** Executável atual para teste: `artifacts/preview-v0.06-refinada/ThinkWind.exe`. Inclui os ajustes do Dock, grades, bandeja, encaixe da ilha e janela de personalização. Sem suíte de testes, type check separado, publish ou instalador; notas anteriores abaixo registram etapas históricas.

Refinamento posterior ao build: fundo compacto separado da camada transparente de ampliação do Dock, grades de aplicativos com ícone/nome e tentativa adicional de bandeja por MSAA. **Essas mudanças ainda não estão no executável compilado**, sem testes/type check/build nesta etapa. Consulte [REFINAMENTOS-0.06.md](REFINAMENTOS-0.06.md).

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
