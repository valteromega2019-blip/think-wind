# Histórico do Think Wind

## 2.0.0-dev — 2026-10-29

- Identidade visual baseada no Think Monitor: grafite, ciano e recursos dinâmicos para temas claro, escuro e automático.
- Ícones sem fundos; recuperação pelo Shell/janelas; resolução de atalhos; agrupamento de janelas e indicadores de minimização.
- Ilha integrada ao topo, com acessos configuráveis, estados proporcionais, cartões translúcidos e calendário com tema.
- Material, tonalidade, escala, espaçamento, raio e posicionamento independentes por componente; contorno DWM desativado e recorte nativo.
- Editor visual com arraste de ícones, controles e módulos; linhas de inserção/alinhamento, listas acessíveis e restauração individual/completa.
- Pré-visualização instantânea, salvamento automático com debounce e migração das preferências antigas.

Revisão por leitura de código e diff. Sem testes, type check, build, publish ou novos instaladores, conforme preferências do usuário. Consulte `REFINAMENTOS-2.0.md` para limitações e verificação pendente.

## 0.2.0 — 2026-10-29

- Integração com Core Audio para enumerar saídas e entradas ativas, ler e alterar volume e mudo por dispositivo.
- Controles de áudio na Dynamic Island com opção de acompanhar o padrão multimídia do Windows e mensagens de indisponibilidade.
- Recuperação periódica após conexão/desconexão de dispositivos; consultas suspensas com a ilha recolhida, oculta ou o módulo desativado.
- Preferência persistente para exibir o módulo de áudio.
- Rolagem do conteúdo expandido e limite de altura pela área útil do monitor.
- Atualização da versão do aplicativo e do script do instalador.

Primeira distribuição experimental local, gerada após autorização explícita para testes: 18 checks aprovados, compilação Release sem avisos/erros, publish independente e instalador compilado. Corrigidas ambiguidades de tipo, espaçamento inválido, identificação legível no seletor de áudio, contraste local do calendário e tratamento de HRESULT de endpoints inválidos. Detalhes e limites em `TESTES.md`. Ainda sem release publicado no GitHub.

## 0.1.0 — 2026-10-29

- Base independente em WPF: dock, Dynamic Island, atalhos, calendário, preferências, bandeja e script de instalador.
