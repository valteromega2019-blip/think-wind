# v0.19 — personalização visual em desenvolvimento

- Em Aparência, cor própria para o fundo da barra inferior e da ilha, independente da cor de destaque. Desativar a opção restaura o fundo do tema. Preferências persistentes, aplicadas imediatamente; opacidade e material continuam nos controles existentes. Na barra superior, a cor própria tem prioridade sobre acompanhar a cor do aplicativo.
- Seletor cromático WPF compartilhado com o overlay: círculo de matiz/saturação, luminosidade, prévia e hexadecimal. Superfície, bordas e controles seguem os recursos do tema do launcher. Mouse com captura durante arrasto; setas para matiz/saturação e Esc para fechar. Nenhuma alteração do tema do Windows ao escolher cores.
- Área horizontal de clique de Mostrar área de trabalho ampliada de 18 para 34 DIP, mantendo o indicador de 2 × 22 DIP e sua altura original. A largura reservada no Dock considera a área maior para evitar sobreposição.
- Clima com o mesmo ícone de condição usado no cabeçalho; os dois usam um único mapeamento de códigos meteorológicos.
- Área com Clima, Notas rápidas (até 4.000 caracteres, salvas localmente) e Atalhos favoritos (até seis destinos reais, com ícones do sistema). Nenhum temporizador ou serviço adicional para os dois novos widgets.
- Aba Widgets acessível pelo botão Editar widgets: prévia da ordem, mostrar/ocultar, arrastar por puxadores, setas e restauração da disposição sem apagar notas ou favoritos. Editor de notas e favoritos com importação dos atalhos da barra, seleção de arquivo, remoção e ordenação. Mudanças estruturais atualizam a ilha; digitar uma nota não reconstrói sua organização.

Revisão por leitura de código e diff. Sem compilação, testes, type check ou conferência visual nesta etapa, conforme a preferência do usuário. A branch `feature/v0.19` não foi publicada e o instalador existente não foi alterado.
