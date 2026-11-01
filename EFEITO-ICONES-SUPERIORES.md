# Ampliação magnética dos ícones superiores

Branch: `feature/acrylic-tipografia`, conforme escolha do usuário.

Aplicado à linha principal da ilha, aos ícones secundários e aos símbolos da
barra de status. O ícone próximo ao cursor cresce até aproximadamente 40%; os
vizinhos recebem ampliação progressivamente menor. O movimento usa distância
bidimensional e integração de mola baseada no tempo, reaproveitando FrameClock.

Somente o conteúdo gráfico do botão é transformado. Fundo Acrylic/Mica, tamanho
da janela, espaçamento do layout e áreas de clique permanecem fixos. A ampliação
cabe no espaço dos botões existentes, inclusive na barra de status de 28 DIP.
Relógio e textos de clima não são ampliados como símbolos. Os comandos originais
dos botões permanecem intactos.

Não há preferências novas nem dependência das configurações do Dock inferior.
O efeito respeita a desativação de animações do Windows, pausa em edição/arrasto
e interações incompatíveis, e é interrompido ao ocultar a interface. Não cria
timer, polling de entrada ou hook global. O relógio de composição só fica
inscrito enquanto há movimento a interpolar; é desligado ao estabilizar.
As transformações anteriores são preservadas e os eventos liberados ao fechar.

Arquivos: `Views/HeaderIconMagnifier.cs`, `IslandWindow.Magnification.cs` e
`IslandWindow.cs`.

Revisão por leitura de código e diff. Compilação, testes e type check não foram
executados nesta tarefa, conforme preferência permanente do usuário. A fluidez
percebida e os limites em diferentes monitores ainda precisam de conferência
na versão compilada.
