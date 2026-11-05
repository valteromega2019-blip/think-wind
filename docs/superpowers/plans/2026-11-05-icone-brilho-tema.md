# Ícone de brilho e tema — plano de implementação

> Execução direta na branch feature/barra-inferior-fixa aprovada. Spec: docs/superpowers/specs/2026-11-05-icone-brilho-tema-design.md. Sem testes/type check/build nesta etapa, conforme instrução do usuário.

**Objetivo:** acesso rápido de brilho com mini painel real e tema na linha de ferramentas.
**Arquitetura:** LayoutCatalog/SettingsStore alimentam as preferências existentes; mini painel usa BrightnessControl/BrightnessService atuais; roteamento unificado para ilha, acessos adicionais e barra adaptativa.

- [x] Adicionar brightness ao catálogo/ordem, inicialmente nos acessos adicionais. Migrar perfis existentes uma única vez sem substituir escolhas posteriores.
- [x] MiniIslandKind.Brightness: título, controle real, estado de detecção/indisponibilidade, reserva inicial de tamanho e acesso às configurações. IsInteracting inclui slider; refresh somente visível com cadência já existente.
- [x] Roteamento e indicação ativa em header, secundária e modo barra; manter semântica de recolhimento, Esc, clique fora e troca de painel.
- [x] Mover tema da linha secundária para junto de Editar/três pontos no painel completo, mantendo sincronização com Windows, sem duplicar.
- [x] Revisão independente por leitura de rotas, migração, lifecycle e tamanho. Atualizar documentação e registrar commit localizado. Sem ajuste de brilho real, instalador ou publicação.

## Pontos de revisão
Perfil antigo/novo; mover/ocultar/desativar ícone; monitor secundário; slider capturado; sem suporte físico; painel aberto antes da resposta assíncrona; tema não duplicado ao alternar modos. Qualquer teste futuro com falha bloqueia entrega.
## Registro da execução
- Brightness adicionado ao catálogo e migrado inicialmente para a área secundária, com visibilidade/ordem/ocultação pelas opções existentes.
- Mini painel reutiliza WMI/DDC/CI, mostra detecção/indisponibilidade, reserva tamanho inicial e respeita o monitor herdado do owner. Sem novas leituras permanentes: usa a cadência visível existente; resultados recebidos já oculto não redesenham o controle.
- Tema movido somente para o header do painel completo, ao lado de ferramentas/editor, em ambos os modos; comportamento de alterar Windows e launcher mantido.
- Revisão independente por leitura não encontrou achado concreto importante. Nenhum teste, type check, lint, build ou ajuste real de brilho foi executado. Sem instalador/publicação.