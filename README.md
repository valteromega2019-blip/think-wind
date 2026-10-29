# Think Wind

Launcher independente para Windows, sem dependências de código, serviços ou configurações do Think Monitor.

Repositório próprio: https://github.com/valteromega2019-blip/think-wind

Pasta de desenvolvimento: `D:\think-wind`. A branch `main` contém a base inicial e a branch `developer` é a área de desenvolvimento. O histórico deste repositório é independente do Think Monitor.

## Primeira implementação — 0.1.0

- Dock nativo flutuante: enumeração das janelas visíveis, ícones de executáveis quando acessíveis, indicadores de execução e foco, ativação, restauração e minimização.
- Atalhos persistentes: adicionar executáveis ou `.lnk`, fixar uma janela com executável acessível, mover e remover pelo menu de contexto. Janelas são exibidas individualmente; atalhos `.lnk` ainda não são associados ao processo aberto.
- Botão Iniciar envia a tecla Windows ao sistema, preservando o menu original.
- Ilha com relógio, data, expansão animada, calendário local e recolhimento ao perder foco ou pressionar Escape.
- Comandos reais de volume, mudo e mídia por `SendInput`. O aplicativo de mídia ativo decide se aceita esses comandos; não há metadados nem seletor próprio de sessões nesta etapa.
- Acessos às páginas nativas de Wi-Fi, Bluetooth, modo avião, áudio, microfone, notificações e barra de tarefas. São acessos às configurações do Windows, não interruptores diretos nem leitura de notificações ou dos ícones da bandeja.
- Uso real de RAM e bateria quando disponível; coleta da ilha somente expandida.
- Preferências em `%LOCALAPPDATA%\ThinkWind\settings.json`, salvas por substituição de arquivo temporário. Escala, tonalidade do Acrylic, recolhimento automático, módulos RAM/bateria e seleção de monitor.
- Inicialização opcional pela chave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, somente ao salvar a opção. Sem privilégios de administrador.
- Ícone na bandeja para personalizar, mostrar e sair; instância única por sessão.

## Arquitetura

`src/ThinkWind` é um aplicativo C# / WPF para .NET 10. `Native` concentra interoperabilidade Win32/DWM; `Services` contém persistência e ações do sistema; `Views` contém dock, ilha e configurações. Não há dependências NuGet externas.

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
3. Áudio por Core Audio, dispositivos e microfone; sessões de mídia com metadados; conectividade com estado real e permissões apropriadas.
4. Notificações com consentimento, integração da bandeja respeitando limitações oficiais, CPU/GPU/rede e atalhos personalizados.
5. Temas claros/escuros/automáticos, cores, perfis, cantos e posicionamento livre, arrastar atalhos, animação de ocultação e múltiplas barras simultâneas.
6. Atualizador com manifesto autenticado, assinatura e verificação de integridade do pacote, rollback e distribuição assinada. Não baixar nem executar atualizações sem essa cadeia de confiança.

## Segurança e restauração

Não há substituição do shell, injeção no Explorer, alteração permanente da barra original ou telemetria. Sair pelo ícone da bandeja encerra as janelas. Os únicos dados persistentes são as preferências e, quando selecionada pelo usuário, a entrada de inicialização. Atalhos são executados pela associação padrão do Windows; adicione apenas arquivos confiáveis.
