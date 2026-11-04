# Ícone de bateria — branch feature/bateria-notebook

Base: versão publicada v0.29.0-beta. Nenhuma integração com as branches experimentais do Explorer.

- Controle Bateria incluído ao lado do relógio na ordem inicial da ilha. No modo barra acompanha a ordem dos acessos da direita, como os demais ícones.
- Ativação automática por presença de bateria reportada pelo Windows, não por heurísticas do nome do computador/instalador. Computadores sem bateria não exibem o ícone. A detecção também cobre configurações antigas, inserindo o novo controle uma vez sem apagar posições e escolhas já existentes.
- Ícone vetorial com preenchimento proporcional à carga, raio de carregamento e marca de alimentação externa. Percentual desconhecido mostra um indicador de leitura indisponível, sem inventar um valor. Tooltip inclui carga, alimentação, economia e autonomia estimada quando informada pelo Windows.
- Porcentagem numérica desligada por padrão. Em Personalizar, na configuração do ícone Bateria, a opção Mostrar porcentagem em número aplica e salva a escolha. Mantém os controles existentes para mover o ícone à área secundária, reorganizá-lo ou ocultá-lo nas duas áreas.
- Clique abre Energia e bateria do Windows; não altera o plano de energia nem solicita elevação.
- Leitura GetSystemPowerStatus com cache de cinco segundos compartilhado pelo ícone, monitores e widget compacto. Reutiliza o temporizador existente da ilha: sem novo timer, WMI, processo auxiliar, evento global ou monitoramento que continue sozinho em Modo Jogo. Uma falha transitória preserva a última leitura bem-sucedida.
- Largura do botão estável enquanto a carga varia, fontes/cores seguindo recursos do tema, desenho vetorial para diferentes DPI e identificação acessível do estado.

Revisão por leitura e diff. Não compilado ou executado nesta entrega. Testes e type check não executados, conforme preferência do usuário; comportamento em notebook, alimentação conectada/desconectada, temas e DPI ainda não validados em execução. Nenhum instalador ou publicação criado.
