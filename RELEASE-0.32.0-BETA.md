# Orby Launcher 0.32.0 Beta

## Destaque: iconografia personalizada — Experimental

Um pacote opcional para dar aos aplicativos a identidade do seu desktop.
Desligado por padrão em novas instalações; suas escolhas existentes são mantidas.

- Em Personalizar → Aparência, escolha Originais, símbolo branco com fundo na cor de destaque ou símbolos flutuantes sem fundo.
- No modo flutuante, apps com janelas visíveis e não minimizadas em qualquer monitor acendem na cor de destaque. Na Biblioteca, todos os apps com janelas abertas acendem, inclusive minimizados. Não é feita análise pixel a pixel de janelas cobertas por outras.
- Destaque atualizado pelo ciclo leve de 100 ms já existente no Dock, sem aguardar a enumeração completa de janelas a cada dois segundos. Sem novos hooks ou timers.
- Símbolos Adobe com contornos vetoriais das letras; Creative Cloud, ChatGPT, VS Code, Hex2Think e Tretalk; Microsoft Store, Office, Teams, OneDrive, Xbox e utilitários; adaptações do pacote ArtCraft.
- Botão direito → Personalizar ícone permite importar PNG ou ICO transparente por aplicativo. Restaurar ícone padrão desfaz a importação.
- Corrigido o controle que podia ficar com tamanho zero quando a imagem nativa não estava disponível; corrigida também a leitura do catálogo suplementar que provocava fechamento.
- Mini tutorial destaca o experimento sem ativá-lo automaticamente, com estado separado dos tutoriais anteriores. Alguns aplicativos usam símbolo genérico ou iniciais; a cobertura está em avaliação.

## Iniciar

Desligar, Suspender e Reiniciar aparecem lado a lado no rodapé. Atualizar e desligar fica visível diretamente quando a verificação existente do Windows Update indica pendência, sem abrir submenu ou consultar atualizações ao clicar.

## Validação e limites

Release compilado sem erros ou avisos. Na revisão da iconografia passaram 61 testes gerais e 8 focados, incluindo os 3.472 vetores base, 37 complementos, persistência e renderização. Uma falha no tamanho do controle sem imagem foi corrigida e os testes pertinentes repetidos com autorização. As alterações finais de destaque entre monitores e botões de energia foram compiladas, mas não receberam nova execução de testes. Os 69 resultados não validam essas alterações posteriores. Sem novos testes, lint ou type check separado nesta preparação, conforme a preferência do usuário. Conferência visual e ações reais de energia/Windows Update permanecem pendentes; nenhum desligamento, suspensão ou reinicialização foi acionado.

Configurações, fixados e personalizações preservados. Instalador Windows x64 com sete idiomas e PawnIO oficial opcional. SHA-256 e assinatura RSA separada acompanham o instalador para validação pelo atualizador integrado. Essa assinatura não é Authenticode e não elimina avisos de editor desconhecido/SmartScreen. Símbolos e marcas de terceiros pertencem aos respectivos titulares.
