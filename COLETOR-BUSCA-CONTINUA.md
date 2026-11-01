# Busca contínua do coletor administrativo

AdvancedSensorClient não aplica mais o prazo de oito segundos à descoberta/conexão do pipe. Mantém uma conexão assíncrona pendente, retorna ao ciclo de telemetria enquanto aguarda e tenta iniciar novamente a tarefa a cada quinze segundos se a conexão continuar indisponível. A tarefa configurada usa MultipleInstances=2 (IgnoreNew), evitando duplicação do coletor ativo.

A troca de modo, ausência de demanda ou encerramento cancela/descarta a conexão pendente. A identidade do servidor continua sendo validada antes de consumir seus dados. Não altera autorização, elevação ou permissões.

O watchdog de oito segundos permanece somente na transmissão de amostras: uma conexão parada é descartada e uma nova busca é permitida após dois segundos, sem limite total de tentativas. Falhas de autorização/identidade continuam visíveis e não são tratadas como FPS válido. O worker mantém suas proteções de inatividade/transmissão. Dados ausentes não são inventados e amostras antigas continuam rejeitadas.

Arquivo: src/ThinkWind/Services/AdvancedSensorClient.cs. Revisão por leitura/diff. Compilação, testes e type check não executados nesta solicitação; reconexão prolongada ainda precisa de conferência em execução.
