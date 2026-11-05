# Biblioteca: ocultar aplicativos e refinar filtros

## Proposta
No botão direito dos aplicativos, adicionar Ocultar da biblioteca. Salvar a escolha por identidade estável do aplicativo e atualizar imediatamente a biblioteca. Uma opção Mostrar ocultos permite restaurar cada item ou todos os itens ocultados manualmente. Ocultar não desinstala nem remove aplicativos fixados no Dock/Iniciar. Biblioteca e Todos respeitam a ocultação; aplicativos fixados explicitamente continuam acessíveis.

## Filtro automático
Revisar nomes, destinos resolvidos, AppUserModelID e metadados já disponíveis no catálogo para retirar utilitários administrativos, manutenção, desinstaladores, documentos de ajuda e atalhos auxiliares. Usar regras específicas, sem excluir pastas inteiras ou palavras genéricas como configuração/setup, para preservar programas legítimos, jogos, players e aplicativos cotidianos do Windows. Não acrescentar polling nem novas consultas durante animações.

## Alternativas
Apenas filtragem automática não resolve todos os casos; somente ocultação manual exige trabalho desnecessário do usuário. A proposta combina filtro mais específico com ocultação manual reversível.

## Escopo
Preferência persistente em SettingsStore; filtros em AppVisibility/ApplicationCatalog; ação e restauração na biblioteca existente com os mesmos estilos. Preservar recursos e ícones originais. Sem desinstalações, alterações globais do Windows ou novos recursos fora da biblioteca.

## Validação
Revisão de código e diff nesta etapa. Testes, type check e build somente com pedido explícito. Nenhum instalador ou publicação. Uma falha em testes bloqueia qualquer entrega posterior.