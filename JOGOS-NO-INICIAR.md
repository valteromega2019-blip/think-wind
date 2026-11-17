# Aba Jogos no Iniciar

Implementada na branch feature/previas-controles-gerais, por escolha do usuário.
A aba aparece somente quando o catálogo contém jogos identificados. Jogos ficam
exclusivamente nessa área do Iniciar, inclusive na pesquisa, sem alterar as listas
persistidas de fixados/recentes do usuário. Os launchers Steam e Epic continuam
na biblioteca de aplicativos. A aba mantém paginação, pesquisa por nome e animação.

A classificação roda no worker já existente do catálogo, usando as raízes locais
de instalação Steam/Epic/GOG disponíveis no detector existente e categorias Games/
Jogos. Atalhos .url Steam/Epic das pastas conhecidas do Menu Iniciar e do desktop
são incluídos; a leitura é limitada e não percorre instalações inteiras ou busca
ícones remotos. Apps sem atalho/catalogação e jogos Store sem categoria reconhecida
podem não ser detectados; não se promete identificação universal por nome.

Os jogos reconhecidos mantêm o ícone original, ignorando o pacote experimental e
sobreposições importadas enquanto identificados como jogo. Preferências e imagens
originais não são removidas. O reconhecimento é compartilhado entre Biblioteca,
Dock e favoritos por identidade; não faz consultas ao disco durante desenho.

Não foram executados compilação, testes ou type check neste pedido. Detecção,
layout e diferentes catálogos ainda precisam de conferência prática. Sem
instalador ou publicação.
