# Página pública Think Wind

Fonte da página publicada em https://valteromega2019-blip.github.io/think-wind-site/ .
Versão atual: 0.19 Alfa · revisão 1. Download e notas apontam para a release v0.19.1-alpha do repositório do aplicativo.

## Nova apresentação com Motion

Recriação preparada na branch `feature/site-motion`. A hospedagem continua no GitHub Pages; esta branch não altera a página pública até sua integração autorizada.

- Novo layout responsivo, tipografia e ilustrações do desktop, player e monitoramento. Informações de instalação, limitações, créditos e downloads preservadas.
- Motion 14.9.0 servido localmente em `assets/vendor`, com licença MIT incluída. Entradas discretas ao rolar e molas curtas nos botões e ícones da ilustração, sem loops contínuos.
- Paleta azul restaurada. Ilustrações montam seus componentes em sequência ao entrar na tela, incluindo player, sensores e ícones da barra.
- Demonstração dedicada de Dock magnético: campo de distância contínuo, ampliação máxima de 55%, influência nos vizinhos e deslocamento horizontal para evitar sobreposição. Usa `springValue` e `styleEffect`; só agenda atualização do alvo quando há interação. Teclado e toque também podem selecionar um ícone. O botão Repetir montagem repete a entrada dos ícones.
- Ilha ilustrativa com três estados, recorte animado, expansão ao passar o mouse, botões acessíveis e recolhimento após a saída. Uma apresentação automática curta mostra os estados uma vez, sendo cancelada pela interação. Não executa funções do aplicativo.
- Respeita movimento reduzido e mantém o conteúdo visível sem JavaScript. O exemplo visual não controla o aplicativo nem representa leituras reais.
- `motion-ai` 14.2.0 instalado e executado no escopo do projeto para Codex. Configurações locais de agentes não são publicadas.
- Não há etapa de build: HTML, CSS e JavaScript são servidos diretamente. Para reinstalar dependências: `npm ci --ignore-scripts --no-audit --no-fund`. O arquivo distribuído do Motion foi copiado de `node_modules/motion/dist/motion.js`.

Revisão por leitura de arquivos e diff. Testes, type check, auditorias e conferência em navegador não executados, conforme preferência do usuário. O instalador do app não foi modificado.
