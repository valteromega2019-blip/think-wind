# Barra fixa e escala — plano
Spec: ../specs/2026-11-23-barra-superior-escala-design.md
Aprovado pelo usuário com detecção de mudança de escala Windows.
- Persistir IslandFixedBar e InterfaceScale (60–140%).
- Reusar faixa de status e reserva existentes, mantendo fullscreen e jogo.
- Aplicar escala na composição e dimensões das FloatingWindow, overlay independente.
- Expor opções em Aparência, restauração para 100%.
- Detectar DPI via eventos de janela existentes, coalescer e adiar no modo jogo; popup com prévia e confirmar/cancelar.
- Revisar código/diff. Não executar testes, type check, build ou empacotamento sem pedido.
