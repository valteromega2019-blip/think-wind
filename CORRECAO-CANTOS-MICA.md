# Arredondamento personalizado do Mica

Causa identificada por leitura: o HWND do material Mica recebia `DWMWCP_ROUND`
ou `DWMWCP_ROUNDSMALL`, ao mesmo tempo que recebia um HRGN com o raio configurado
no launcher. Essas preferências do Windows não aceitam um raio numérico e
criavam uma segunda política de cantos, diferente da superfície WPF.

Alterações:

- Mica continua sendo o material nativo do DWM, mas seu arredondamento fica
  exclusivamente sob controle do recorte personalizado, como no Acrylic.
- O raio é limitado às dimensões reais da superfície após as margens
  transparentes e a conversão de DPI. O preenchimento WPF recebe o mesmo limite.
- A preferência automática de cantos do DWM fica desativada para esse HWND;
  valor zero, cápsulas e cantos superiores retos continuam respeitados.
- Trocar de material invalida o cache de geometria e desliga a composição
  Acrylic anterior ao usar Mica. Alterar somente opacidade ou dimensões não
  reinicializa o material nativo, se o tema e o material continuam iguais.
- Não há novos timers ou mudanças nos indicadores, animações e Modo Jogo.

Arquivos: `src/ThinkWind/Native/BackdropHost.cs` e
`src/ThinkWind/Views/FloatingWindow.cs`.

Revisão por código e diff. Testes, type check e compilação não executados nesta
tarefa, conforme preferência do usuário. A correção ainda requer conferência
visual da versão compilada em Mica, alternando raios, escalas e opacidades.

Referência: [preferências de cantos do DWM](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_window_corner_preference).
