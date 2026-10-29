# Think Wind 0.08 Alfa — 0.8.0-alpha

## Ajustes

- Reserva superior do Dock calculada a partir da altura medida dos botões, incluindo indicadores, padding e overshoot de mola, com margem adicional de 12 DIP.
- Camada dos ícones permite overflow sem recorte de layout; fundo Acrylic mantém sua área compacta. Botão Iniciar acompanha a largura necessária para o ícone, em vez de ficar fixo em 40 DIP.
- Superfícies, controles e hover usam pincéis com alfa. Nova opção em Personalização → Aparência → **Opacidade dos blocos e controles**, de 10% a 90%, padrão 50%, com atualização imediata e persistência.
- Textos/ícones permanecem opacos. Popups de menus/seletores preservam superfície sólida própria, pois não compartilham o backdrop da janela. Alto contraste preserva os pincéis do sistema.

## Validação efetiva

Comando: `dotnet run --project checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release --no-launch-profile`.

**37 aprovados; 0 falhos; 0 ignorados.** Configurações isoladas em pasta temporária. Inclui os 34 cenários anteriores e três regressões:

1. Reserva de altura e largura do Iniciar com ícones de 18 a 72 DIP e intensidade de 40% a 60%.
2. Renderização WPF/nativa de um ícone ampliado fora do fundo Acrylic: pixel do ícone acima do material, alfa zero na reserva vazia e HWND do material menor que a janela de conteúdo.
3. Alteração de opacidade dos pincéis em claro/escuro, preservando texto e popups legíveis.

Publicação Release self-contained/single-file `win-x64` com `-warnaserror`, concluída com sucesso. Compilação Inno Setup concluída. Instalador: `artifacts/installer/ThinkWind-Setup-0.8.0-alpha.exe`.

## Publicação e limites

Release privada: https://github.com/valteromega2019-blip/think-wind/releases/tag/v0.8.0-alpha

Página pública: https://valteromega2019-blip.github.io/think-wind-site/

O código e o instalador continuam privados; o site contém somente a apresentação e informa a exigência de acesso autorizado para baixar. Instalador e SHA-256 anexados à pré-release.

O teste de recorte usa uma superfície controlada com o mesmo host/caminho de composição; não substitui inspeção de todos os ícones reais, animações, escalas físicas ou múltiplos monitores. Não houve medição de desempenho, validação completa do hook de teclado ou instalação em Windows limpo. Não foram alterados volume/mute para valores diferentes dos existentes nem trocadas conexões Wi-Fi. A instância aberta não foi substituída automaticamente.
