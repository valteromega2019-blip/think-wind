# Orby Launcher 0.29.1 Beta

- Ícone vetorial de bateria na barra superior, ativado automaticamente quando o Windows informa uma bateria disponível. Em computadores sem bateria, permanece oculto.
- Nível de preenchimento acompanha a carga; raio indica carregamento e uma marca indica alimentação externa. Ao passar o mouse, amplia com o mesmo efeito dos outros ícones e acende na cor de destaque.
- No modo barra, fica à esquerda do Wi-Fi no grupo da direita. No modo ilha, fica à esquerda de Configurações do Windows.
- Porcentagem numérica opcional na configuração do ícone Bateria, desligada por padrão. Mantidos os controles para ocultar nas duas áreas ou mostrar nos acessos secundários.
- Clique abre Energia e bateria do Windows. Tooltip informa carga, estado e autonomia estimada, quando disponíveis.
- Espaço transparente reservado para a ampliação do ícone, com recorte local removido e largura estável durante mudanças da carga.
- Leitura nativa compartilhada entre monitores e widget de monitoramento, com cache de cinco segundos. Sem novo serviço, timer independente ou hooks de entrada.

## Compilação e limites

Compilação Release e instalador x64 preparados nesta publicação. Não foram executados testes, lint ou type check separados, conforme a preferência do usuário. A correção final do recorte foi revisada por leitura; bateria física de notebook, temas, fontes ampliadas e diferentes DPI/monitores ainda precisam de validação em uso real. Não há medição nova de CPU, memória, FPS ou input lag.

A prévia local usa bateria simulada somente quando iniciada explicitamente com `--simulate-battery`. A instalação e a inicialização normais usam os dados reais do Windows; a simulação não é salva como preferência.

Mantidos os recursos da 0.29.0 Beta. Não inclui nenhum dos experimentos de personalização do Explorer. Configurações preservadas, sete idiomas no instalador e PawnIO oficial opcional. Interface do aplicativo em português. Instalador acompanhado por SHA-256 e assinatura RSA separada para o atualizador integrado; essa assinatura não é Authenticode e não elimina avisos do SmartScreen.
