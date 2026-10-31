# Think Wind 0.18 Alfa — revisão 3

- Corrige a distribuição do coletor administrativo: inclui as bibliotecas específicas do Windows e dependências usadas pelo monitoramento de FPS.
- Atualiza o auxiliar conforme a compilação, evitando manter uma prévia antiga com a mesma versão nominal. Remove componentes obsoletos ao trocar o tipo de distribuição.
- Inicia as leituras na abertura e mantém os dados disponíveis no desktop antes de abrir o painel. Em Modo Jogo, a coleta de preparação é suspensa e o overlay mantém somente o trabalho necessário.
- Reúne a preparação inicial de sensores e controle de janelas em uma autorização administrativa. A interface permanece com permissões normais para receber arquivos arrastados.
- Prioridade temporária AboveNormal na construção inicial da interface, por até cinco segundos, com retorno a Normal. Não utilizada ao identificar jogo na abertura.
- Corrige a medição completa da área de monitoramento, incluindo rodapé, largura final e escala de fontes. Reduz reajustes do fundo nas atualizações dos sensores.
- Estabiliza a adaptação de cor da barra superior e mantém registro de diagnóstico do coletor.

## Validação

**47 testes aprovados, sem falhas ou ignorados.** Publicação Release com avisos tratados como erros e instalador Inno Setup concluídos. Temperatura real da CPU confirmada na prévia; FPS e latência em jogo não foram medidos nesta entrega. Consulte `VALIDACAO-0.18.3-ALFA.md` para escopo e limites.

## Download

`ThinkWind-Setup-0.18.3-alpha.exe` — Windows x64, runtime incluído e PawnIO oficial opcional. Preserve suas configurações durante a atualização. O Windows solicitará autorização para os auxiliares administrativos quando necessário; a interface continua sem elevação.

SHA-256: `69d6e5690dd8605f5c9598e9a379777f1350df92338070435baad2df2c94f442`.

Versão Alfa; instalador sem assinatura digital. A compilação foi publicada da branch `feature/inicializacao-prioritaria`; não houve merge automático em `main`.
