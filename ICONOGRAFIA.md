# Iconografia unificada do Orby

Branch `feature/iconografia-propria`, reaproveitada a pedido do usuário. O cadastro
opcional foi revertido; o serviço online não foi publicado, e não houve coleta.

## Uso

Personalizar → Aparência → **Ícones unificados · fundo de destaque e símbolo branco**.
Desligada por padrão, preservando os ícones originais. A escolha é salva nas
preferências e afeta os ícones carregados imediatamente, sem reiniciar.

- Mesmo fundo arredondado, na cor de destaque escolhida, e símbolo branco.
- Aplicado ao Dock, Fixados, Biblioteca, Recentes, Todos, resultados da pesquisa,
  atalhos favoritos e bandeja de aplicativos em segundo plano.
- As imagens originais permanecem disponíveis para retornar ao modo original.
- Não muda atalhos, identidade dos aplicativos, arquivos, ícones do sistema ou
  imagens de capas/miniaturas. Os controles do launcher mantêm seus ícones atuais.

## Catálogo

Importação fixada em Simple Icons 16.38.0: 3.472 símbolos de marcas. Não significa
3.472 aplicativos instalados ou todos os aplicativos existentes. O pacote de origem
teve sua integridade SHA-512 conferida antes da extração. Nenhum script npm foi
executado nem uma dependência de runtime JavaScript foi adicionada.

Aliases reconhecem aplicativos de design, arquitetura, desenvolvimento, mídia,
comunicação e jogos. Os símbolos que não existirem no catálogo recebem monogramas;
configurações, pastas e alguns utilitários usam os glifos nativos do Windows. Os
demais aplicativos usam iniciais. Não se promete um logotipo específico para todo
programa. A correspondência não usa nomes de pastas ancestrais, evitando classificar
todos os jogos instalados no Steam como o próprio Steam.

O acabamento é do Orby; os logotipos de terceiros não são marcas de propriedade do
Orby. Licença, aviso de marcas e metadados de origem acompanham o projeto em
`licenses/SimpleIcons-*`. Os ícones da base não foram redesenhados individualmente.

## Recursos e validação

Vetores locais, sem downloads durante o uso, timers, hooks ou novas consultas ao
sistema. O catálogo só é lido ao usar o modo unificado. Geometrias e resoluções têm
caches limitados, e os controles removem suas assinaturas ao sair da tela.

Revisão por leitura e diff. Não foram executados build, testes, lint ou type check
nesta implementação, conforme preferência do usuário. Ainda é necessária conferência
visual com diferentes aplicativos, cores e escalas DPI. Nenhum instalador ou release
foi criado para essa função.
