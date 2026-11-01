# Câmera nas prévias de chamadas

Branch: `feature/acrylic-tipografia`, conforme escolha do usuário.

Adicionado `CallAction.Camera` ao adaptador de acessibilidade existente. Meet,
Discord, Teams, Zoom e outros candidatos já reconhecidos podem mostrar ligar e
desligar câmera quando expõem um botão visível, habilitado e único com uma ação
reconhecida em português ou inglês. A chamada ainda exige os controles de
microfone e sair; a câmera não faz uma tela de pré-entrada ser tratada como chamada.

O botão usa ícone de vídeo, indicação "Câmera ligada" ou "Câmera desligada" e
uma linha diagonal vermelha quando desligada. A indicação ligada acompanha a
cor de destaque. Os textos e ícones dos demais controles também acompanham a
cor do botão. Com quatro comandos, a disposição usa duas linhas.

O significado dos rótulos de ação é respeitado: "Ativar câmera" indica câmera
atualmente desligada. O estado não reutiliza a interpretação inversa do toggle
de mute do Discord. Após clicar, o controle é localizado novamente e acionado
por InvokePattern/TogglePattern; a interface é atualizada pela leitura seguinte,
sem simular sucesso. Não são enviados atalhos globais.

Limites: idioma, versão, janela minimizada, aba inativa, acessibilidade ou
permissões podem impedir a descoberta. Um controle genérico chamado somente
"Câmera" não é acionado como se fosse um toggle. Sem uma ação reconhecida, o
botão não aparece. Eventuais permissões de câmera continuam sendo tratadas pelo
aplicativo/navegador; não há acesso ao vídeo, captura da webcam, alteração das
permissões de privacidade do Windows ou confirmação automática de diálogos.
O suporte em chamadas reais ainda não foi validado nesta alteração.

Arquivos alterados: `src/ThinkWind/Services/CallIntegration.cs` e
`src/ThinkWind/Views/CallPreviewCard.cs`.

Revisão por leitura de código e diff. Testes, type check e compilação não
executados nesta tarefa, conforme preferência permanente do usuário.
