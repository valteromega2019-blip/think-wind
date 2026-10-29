# Validação v0.06 — 2026-10-29

Escopo solicitado: bateria de testes e correção de ícones pequenos/encolhimento ao trocar Fixados por Recentes. Sem instalador.

## Correções

- Ícones da gaveta de 40 para 56 DIP e nomes de 12 DIP em duas linhas; células de 128 DIP.
- Janela de até 720 × 660 DIP, limitada ao espaço acima da barra. Linhas, colunas e altura da grade são fixadas pelo espaço disponível, independentemente do número de resultados.
- Fixados, Recentes, Todos e Biblioteca mantêm a mesma reserva para o filtro. Trocar de aba não calcula mais o tamanho da janela a partir da altura dos resultados; pesquisa e paginação preservadas.
- Corrigido acesso à UI do Wi-Fi fora da thread visual: retornos assíncronos de leitura/erro/operação/finalização passam explicitamente pelo dispatcher.

## Resultados

Comando: `dotnet run --project checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release --no-launch-profile`.

Execução final: **34 aprovados, 0 falhos, 0 ignorados**, saída 0. Configurações isoladas em pasta temporária; preferências reais não foram alteradas pelos checks. A primeira execução revelou a exceção de thread no Wi-Fi e um teste de grade sem largura de contêiner, ambos corrigidos antes da execução final.

Cobertura: persistência/normalização/erros de arquivo; enumeração de janelas/RAM; estados compacto/hover/completo/mini; snapshots de rede; composição nativa e alfa/recortes em claro/escuro; transições de composição; fechamento de mini-ilhas por Esc/foco; enumeração, leitura e regravação dos valores atuais de volume/mute de saída e captura; endpoint inválido; limites de gaveta em diferentes tamanhos; dimensão da grade com zero/um/vários itens; ausência de interseção dos blocos e alinhamento da base; retorno assíncrono de Wi-Fi ao dispatcher; persistência das novas preferências.

Foram encontrados cinco endpoints ativos de saída e três de entrada. A escrita preservou o volume/mute atuais. O check de Wi-Fi consulta redes/estado; não troca conexão, não escreve senhas nem alterna rádio.

Build Release adicional: **zero erros e zero avisos**, executável em `artifacts/preview-v0.06-validada/ThinkWind.exe`. A versão aberta anteriormente não foi substituída/reiniciada nesta etapa.

## Limites

Os testes de grade usam medida/arranjo WPF e verificam estabilidade geométrica, sem afirmar aprovação visual da navegação completa. Não validam todos os casos de DPI/monitores físicos, todos os menus/ícones do Explorer, conexões reais com senha, troca audível de dispositivo padrão, restauração após falha forçada do helper, recebimento de arquivos em cada aplicativo ou desempenho prolongado. Não há medição de fluidez/performance nem teste completo de instalação/atualização. Nenhum publish ou instalador foi gerado.
