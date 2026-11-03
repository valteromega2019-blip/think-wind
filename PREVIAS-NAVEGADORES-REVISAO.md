# Revisão das prévias de navegador

## Diagnóstico por leitura e imagem enviada

- Mistura de cartões 3D com janelas DWM 2D e recortes distintos.
- Backdrops nativos criados como janelas pertencentes ao conteúdo WPF: o Windows mantém uma janela owned acima de sua owner, contrariando a intenção de pôr o Acrylic atrás dos títulos.
- Várias abas do mesmo HWND eram apresentadas como prévias independentes, embora a origem DWM corresponda à janela inteira.

## Correção

- Cartões 2D alinhados, com cabeçalho de altura estável, efeito de hover e espaço reservado para a imagem.
- Backdrops independentes, posicionados atrás do conteúdo pelo mecanismo existente, liberados ao ocultar/fechar.
- Uma prévia real por HWND, até quatro janelas; janelas não enumeradas pela leitura de abas entram por fallback a partir da enumeração de janelas do Dock.
- Abas identificadas continuam no menu de contexto do cartão; sua seleção só ocorre por clique explícito. O menu impede fechamento e reconstrução do painel durante interação.
- Clique no cartão restaura sua janela. Hover não ativa janelas nem troca abas reais.

## Limites

O Windows fornece thumbnails de janelas top-level. Abas ocultas não recebem uma imagem independente por esta API; não é apresentada a imagem de outra aba como substituta. Conteúdo protegido, descarte de superfícies pelo navegador e janelas minimizadas podem limitar a atualização da imagem.

Referências consultadas:
- https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail
- https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features

Validação: leitura de código e diff. Sem compilação, testes, type check ou verificação visual em execução nesta etapa, conforme preferência do usuário.

## Até seis janelas e expansão

- Leitura limitada a seis janelas de navegador, com até 64 metadados de abas por janela; os metadados não são truncados para quatro itens antes do agrupamento.
- Indicador acima do último cartão informa quantas outras janelas estão recolhidas. Expandir todas apresenta as imagens em grade de duas colunas; Recolher retorna ao leque.
- A largura e altura do painel acompanham a grade e o espaço útil disponível. O Viewbox existente reduz a apresentação quando necessário.
- Removido o recorte retangular dos cartões. Em estado recolhido, a área da imagem é ocultada e a altura real do cartão é reduzida, preservando os cantos e a sombra fora do cartão.
- Reposicionamento nativo só após medida e arranjo válidos.
- A contagem é de janelas reais. Duas janelas com seis abas continuam sendo duas fontes DWM, com as abas nos menus correspondentes.

Esta revisão não foi compilada nem testada.

## Organização por janela e abas

- `BrowserWindowFans` apresenta até seis grupos de janelas lado a lado.
- Cada grupo usa seu próprio leque com até seis abas, contador e expansão independentes.
- A largura externa soma a largura real dos grupos; a altura acompanha o maior grupo. O redimensionamento fica limitado à área de apresentação existente.
- Hover só reorganiza cartões; clicar no cartão de uma aba usa a seleção de aba existente. Imagens ao vivo só são anexadas a abas selecionadas ou representações da janela sem informação de abas, evitando atribuir a imagem de uma aba à outra.
- Rótulos de abas em segundo plano permanecem disponíveis, mas o DWM não produz imagens independentes delas.
- Sem compilação, testes ou type check nesta etapa.

## Recorte antigo preservado na janela reutilizada

O caminho `SuppressMaterial` retornava antes de limpar o `Grid.Clip`. Após abrir uma prévia normal ou de mídia e depois o leque do navegador, o recorte anterior podia continuar aplicado ao novo conteúdo. Agora esse caminho remove o recorte externo e invalida seu cache; ao voltar ao modo com fundo, o recorte é recalculado normalmente. Verificação por leitura; sem compilação ou testes nesta alteração.
