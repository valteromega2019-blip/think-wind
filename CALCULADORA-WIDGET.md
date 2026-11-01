# Calculadora na central de controle

Na branch `feature/teclado-virtual`, o antigo bloco Notificações (dois atalhos) foi substituído por uma calculadora. O controle de notificações da barra superior permanece disponível.

- Quatro operações, porcentagem, inversão de sinal, vírgula decimal, apagar dígito e limpar.
- Cálculo sequencial, como uma calculadora básica, com decimal do .NET; erros de divisão por zero e limites numéricos aparecem no visor. Não usa execução de expressões, processos externos, rede ou temporizadores.
- Botões, fontes e superfícies seguem os recursos de tema do launcher; operações usam a cor de destaque. O bloco participa da grade responsiva e do editor de layout existentes.
- Teclado físico funciona quando um controle da calculadora tem foco; Enter calcula e Backspace apaga. Nenhum atalho global foi adicionado.
- O identificador de módulo antigo `notifications` é migrado para `calculator`, preservando posição e preferência de ocultação. A opção de mostrar/ocultar aparece como Calculadora no editor de módulos. O estado do cálculo é local à janela e não é salvo nas preferências.

Arquivos: Services/CalculatorState.cs, Views/CalculatorWidget.cs, Services/LayoutCatalog.cs, Services/SettingsStore.cs e Views/IslandWindow.cs.

Revisão por leitura e diff. Compilação, testes e type check não executados nesta solicitação, conforme a preferência do usuário. A versão aberta não recebe a mudança até compilar e executar esta branch.
