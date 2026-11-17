# Iconografia unificada do Orby

Branch `feature/iconografia-propria`, reaproveitada a pedido do usuário. O cadastro
opcional foi revertido; o serviço online não foi publicado, e não houve coleta.

## Uso

Personalizar → Aparência → **Experimental · ícones unificados**.
Desligada por padrão, preservando os ícones originais. A escolha é salva nas
preferências e afeta os ícones carregados imediatamente, sem reiniciar.

- Mesmo fundo arredondado, na cor de destaque escolhida, e símbolo branco.
- Aplicado ao Dock, Fixados, Biblioteca, Recentes, Todos, resultados da pesquisa,
  atalhos favoritos e bandeja de aplicativos em segundo plano.
- As imagens originais permanecem disponíveis para retornar ao modo original.
- Não muda atalhos, identidade dos aplicativos, arquivos, ícones do sistema ou
  imagens de capas/miniaturas. Os controles do launcher mantêm seus ícones atuais.

## Personalização por aplicativo

No botão direito de um aplicativo no Dock ou nas gavetas (Fixados, Biblioteca,
Recentes e Todos), use **Personalizar ícone…**. O seletor aceita PNG/ICO de até
2 MB, com transparência e dimensões de até 1024 × 1024 pixels. A cópia é recortada
ao símbolo, reduzida para até 256 pixels e convertida para branco, preservando o
alfa. O original não é modificado, e o desenho é colocado sobre a cor de destaque.

O arquivo normalizado fica incorporado às preferências, sem referência ao caminho
original; assim, acompanha o backup das configurações. São permitidos até 64
registros, com orçamento total de aproximadamente 1 MB para as imagens codificadas.
A escolha usa AppUserModelID e/ou destino resolvido do atalho. **Restaurar ícone
padrão** remove essa escolha e retorna ao catálogo unificado ou ao ícone original,
conforme a opção global.

A personalização individual é explícita: funciona mesmo com o modo unificado
global desligado, sem ativá-lo para os outros aplicativos. Por padrão não há
ícones personalizados. O arquivo é processado em tarefa de fundo após a seleção;
os controles existentes permanecem protegidos contra fechamento por clique fora
enquanto o seletor está aberto. Esta adição foi revisada por leitura e diff, ainda
sem nova compilação, testes ou type check. Os cinco testes citados abaixo são da
implementação anterior e não validam a importação personalizada.

## Base de símbolos

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

Validação autorizada em 17/11/2026: compilação Release sem erros nem avisos e cinco
testes focados aprovados. Cobertura: padrão desligado e perfis anteriores,
persistência em arquivo temporário, compatibilidade dos 3.472 vetores com WPF,
renderização de símbolos e monogramas em quatro escalas DPI, mudança de cor e
retorno à imagem original. Uma falha inicial da captura off-screen foi corrigida
processando UpdateLayout antes de capturar a imagem novamente; não houve falha
na geometria do catálogo. Nenhuma preferência real foi alterada pelos testes.
Não foi executada nova regressão completa, lint ou type check separado. Ainda é
necessária conferência visual em uso real. Nenhum instalador ou release foi criado.
