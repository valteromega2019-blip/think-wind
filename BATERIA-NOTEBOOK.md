# Ícone de bateria — branch feature/bateria-notebook

Base: versão publicada v0.29.0-beta. Nenhuma integração com as branches experimentais do Explorer.

- No modo ilha, Bateria aparece imediatamente à esquerda de Configurações do Windows. No modo barra, fica no grupo da direita, imediatamente à esquerda do Wi-Fi. A apresentação posiciona a bateria conforme o modo sem reordenar as preferências dos outros controles. Se o vizinho estiver oculto, a bateria fica no final dos controles disponíveis desse grupo.
- Ativação automática por presença de bateria reportada pelo Windows, não por heurísticas do nome do computador/instalador. Computadores sem bateria não exibem o ícone. A detecção também cobre configurações antigas, inserindo o novo controle uma vez sem apagar posições e escolhas já existentes.
- Ícone vetorial com preenchimento proporcional à carga, raio de carregamento e marca de alimentação externa. Percentual desconhecido mostra um indicador de leitura indisponível, sem inventar um valor. Tooltip inclui carga, alimentação, economia e autonomia estimada quando informada pelo Windows.
- Porcentagem numérica desligada por padrão. Em Personalizar, na configuração do ícone Bateria, a opção Mostrar porcentagem em número aplica e salva a escolha. Mantém os controles existentes para mover o ícone à área secundária, reorganizá-lo ou ocultá-lo nas duas áreas.
- Clique abre Energia e bateria do Windows; não altera o plano de energia nem solicita elevação.
- Leitura GetSystemPowerStatus com cache de cinco segundos compartilhado pelo ícone, monitores e widget compacto. Reutiliza o temporizador existente da ilha: sem novo timer, WMI, processo auxiliar, evento global ou monitoramento que continue sozinho em Modo Jogo. Uma falha transitória preserva a última leitura bem-sucedida.
- Largura do botão estável enquanto a carga varia, fontes/cores seguindo recursos do tema, desenho vetorial para diferentes DPI e identificação acessível do estado.

Revisão por leitura e diff. Não compilado ou executado nesta entrega. Testes e type check não executados, conforme preferência do usuário; comportamento em notebook, alimentação conectada/desconectada, temas e DPI ainda não validados em execução. Nenhum instalador ou publicação criado.

## Prévia solicitada com simulação

Adicionado argumento explícito `--simulate-battery`, sem preferência persistente ou alteração do Windows. Alterna a cada 15 segundos entre 72% em uso, 18% com economia, 45% carregando e 100% na tomada. Tooltip e nome acessível indicam SIMULAÇÃO. A execução normal continua nativa. Esta prévia também evita reconfigurar a inicialização automática e os auxiliares administrativos para o caminho de desenvolvimento.

Compilação Release/publish concluída sem erros/avisos emitidos. Prévia aberta em `artifacts/preview-battery/OrbyLauncher.exe --simulate-battery` e processo confirmado respondendo. Nenhum teste automatizado ou type check separado executado. A avaliação visual e a leitura de bateria física permanecem pendentes; não foi gerado instalador.

## Hover do ícone

O magnificador existente reconhecia apenas glyphs TextBlock, deixando o desenho vetorial da bateria de fora. Estendido o mesmo magnificador para reconhecer BatteryStatusButton e animar seu BatteryIcon: ampliação com a mesma mola contínua e contorno na cor de destaque durante hover, restaurando o recurso do tema ao sair. A porcentagem mantém seu espaço fixo. Sem timer ou motor de animação adicional. Correção revisada por leitura; não compilada nem executada nesta etapa, sem testes/type check.

Após relato de corte no hover, incluído um contêiner transparente com espaço reservado para o ícone ampliado, alinhamento central e ClipToBounds desativado na hierarquia local. A largura já reserva o espaço antes da animação e a porcentagem tem uma coluna fixa, sem redimensionamento por movimento do mouse. Correção ainda não compilada ou validada visualmente; nenhum teste/type check executado.
