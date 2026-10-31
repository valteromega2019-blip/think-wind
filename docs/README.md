# Página pública Think Wind

Fonte da página publicada em https://valteromega2019-blip.github.io/think-wind-site/ .
Versão atual: 0.19 Alfa · revisão 1. Download e notas apontam para a release v0.19.1-alpha do repositório do aplicativo.

## Nova apresentação com Motion

Recriação preparada na branch `feature/site-motion`. A hospedagem continua no GitHub Pages; esta branch não altera a página pública até sua integração autorizada.

- Novo layout responsivo, tipografia e ilustrações do desktop, player e monitoramento. Informações de instalação, limitações, créditos e downloads preservadas.
- Motion 14.9.0 servido localmente em `assets/vendor`, com licença MIT incluída. Entradas discretas ao rolar e molas curtas nos botões e ícones da ilustração, sem loops contínuos.
- Paleta azul restaurada. Ilustrações montam seus componentes em sequência ao entrar na tela, incluindo player, sensores e ícones da barra.
- Terceiro destaque visual, após o Dock: exemplo de personalização Acrylic com alternância suave de quatro cores a cada 2,2 segundos, amostras selecionáveis e controle de transparência. Pausa fora da tela, em aba oculta ou durante interação. Trata-se de uma ilustração CSS do material, sem alterar o aplicativo ou o Windows.
- Demonstração dedicada de Dock magnético: campo de distância contínuo, ampliação máxima de 55%, influência nos vizinhos e deslocamento horizontal para evitar sobreposição. Usa `springValue` e `styleEffect`; só agenda atualização do alvo quando há interação. Teclado e toque também podem selecionar um ícone. O botão Repetir montagem repete a entrada dos ícones.
- Ilustração superior com três modos selecionáveis: Barra em toda a largura, Ilha central colada ao topo e Expandida abaixo da barra ou da ilha. Recorte animado, expansão ao passar o mouse, botões acessíveis e recolhimento após a saída. A apresentação automática alterna os modos a cada 1,4 segundo enquanto está visível. Pausa durante a interação, fora da tela ou com a aba em segundo plano; respeita movimento reduzido. Não executa funções do aplicativo.
- Respeita movimento reduzido e mantém o conteúdo visível sem JavaScript. O exemplo visual não controla o aplicativo nem representa leituras reais.
- Ao sair e voltar a cada área, as entradas e montagens Motion são reapresentadas. Ilha, pesquisa e cores retomam suas demonstrações automáticas, preservando campos e controles que ainda estejam com foco. Animações de entrada são concluídas ao sair para evitar elementos parcialmente ocultos.
- Pesquisa ilustrada: abertura do campo, digitação animada de Code, primeiro resultado destacado e Enter mostrando a abertura ilustrativa do aplicativo. Também aceita digitação e Enter reais no exemplo, sem executar programas; o clique na lupa do desktop leva à demonstração. A sequência repete em ciclos de 4,2 segundos enquanto visível e com a aba ativa. Interação interrompe o loop; o botão Repetir busca pode retomá-lo. Anúncios de leitor de tela ficam silenciosos durante a demonstração automática.
- `motion-ai` 14.2.0 instalado e executado no escopo do projeto para Codex. Configurações locais de agentes não são publicadas.
- Não há etapa de build: HTML, CSS e JavaScript são servidos diretamente. Para reinstalar dependências: `npm ci --ignore-scripts --no-audit --no-fund`. O arquivo distribuído do Motion foi copiado de `node_modules/motion/dist/motion.js`.

Revisão por leitura de arquivos e diff. Testes, type check, auditorias e conferência em navegador não executados, conforme preferência do usuário. O instalador do app não foi modificado.
