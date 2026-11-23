# Barra superior fixa e escala da interface

Branch feature/barra-superior-fixa-escala. Desenvolvimento local, não publicado.

Personalizar → Aparência → Barra superior e escala:
- Ative Barra superior fixa de ponta a ponta para manter a faixa de status no desktop e reservar a área útil do topo.
- Escala geral do Orby: 60%–140%, padrão 100%; o Windows e a escala específica do overlay não são alterados.
- Restaurar escala volta a 100%.
- Ao alterar a escala do Windows, o aviso central permite prévia pelo slider, Aplicar ou Manter atual. Esc restaura o valor anterior. A detecção usa notificações nativas existentes e um debounce transitório; não há polling contínuo novo. No Modo Jogo o aviso aguarda retornar.

Validação nesta entrega: leitura de código e diff. Não executados testes, type check, build, prévia ou instalador, conforme a preferência do usuário. Especial atenção pendente à geometria dos popups ancorados, múltiplos monitores, recortes Acrylic e redimensionamento de Personalizar em DPI alto. Esta implementação ainda precisa de compilação e conferência visual autorizadas.