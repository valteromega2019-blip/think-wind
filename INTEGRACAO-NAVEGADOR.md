# Música e reunião nas prévias do navegador

Branch: `feature/acrylic-tipografia`, conforme escolha do usuário.

## Correções

- Removido o painel de Meet baseado em nome de aba e removida a classificação
  `IsMeet` por título. Uma pesquisa, documentação ou aba apenas contendo “Meet”
  continua sendo uma aba comum.
- Uma reunião requer documento com origem HTTPS `meet.google.com` e os controles
  reais, habilitados, de microfone e sair da chamada. A barra de endereço nativa
  é fallback apenas quando existe um único documento sem URL própria. Campos
  dentro da página não comprovam a origem, e uma URL de outro site no documento
  não é substituída pelo texto digitado na barra de endereço.
- Documentos de Meet expostos pela acessibilidade podem ser inspecionados sem
  exigir que a janela esteja em primeiro plano. `IsOffscreen` sozinho não elimina
  um controle desse documento verificado; os comandos permanecem únicos e são
  redescobertos antes de executar. Não há restauração automática no hover.
- Música é associada ao aplicativo que publicou a sessão GSMTC, sem depender
  apenas do título da janela selecionada. O grupo do navegador reconhece também
  suas identidades usuais (por exemplo, msedge/Microsoft Edge).
- A memória de abas mantém até 64 títulos por janela para associação de mídia,
  enquanto a apresentação continua limitada a quatro cartões. A associação de
  um item PWA continua mais restrita, para não tomar a mídia de outro aplicativo.
- Vídeos e imagens explicitamente identificados como tal são rejeitados. Uma
  sessão precisa disponibilizar controles e identificação de música. Havendo
  várias sessões compatíveis, não é escolhida uma delas arbitrariamente.

## Prioridade e alternância

Quando reunião e música forem confirmadas, a reunião aparece primeiro. Dois
botões, **Reunião** e **Música**, permitem trocar o cartão sem abrir o navegador,
pausar a música ou sair da reunião. A escolha manual é preservada nas atualizações
da mesma prévia; uma nova reunião volta a ter prioridade. Se uma integração
deixar de estar disponível, a outra pode continuar sendo exibida.

A descoberta é atualizada pelo temporizador já existente da prévia, mesmo sem
mudança no título da aba. A atualização de estado da chamada não é confundida
com uma interação manual que impediria descobrir música. As ações do usuário e
os menus continuam protegidos contra reconstrução durante a interação.
O player é vinculado à sessão selecionada, sem controlar outro player global
por engano. As inscrições e atualizações visuais param ao ocultar a prévia.

## Limites e validação

Nenhuma extensão foi reintroduzida. Chrome/Edge e outros navegadores podem deixar
de expor documentos e controles ao minimizar a janela ou desativar uma aba.
Nessa situação não é possível confirmar uma chamada ativa pelo nome da aba, e
o Orby não inventa o estado. A descoberta inicial e os comandos de uma aba
totalmente inacessível continuam limitados pelo navegador. Não existe promessa
de funcionamento universal em segundo plano sem acesso aos dados da chamada.
Janelas/idiomas/versões diferentes também podem limitar a acessibilidade.

Não foram executados testes, type check ou compilação nesta alteração, conforme
preferência permanente do usuário. A validação em uma chamada real com música
simultânea, troca de aba e janela minimizada ainda está pendente.

Arquivos: `Services/BrowserTabs.cs`, `CallIntegration.cs`, `MediaService.cs`,
`MusicSessionPolicy.cs`; `Views/CallPreviewCard.cs`, `DockWindow.Previews.cs`,
`MediaCard.cs`, `WindowPreview.cs`, `WindowPreview.Browser.cs`.
O rascunho anterior de integração genérica dos players foi incluído neste commit,
pois a correção do navegador utiliza sua seleção de sessão e identidade.

## Validação adicional da sala

A página inicial (`/` ou `/meet/`) não é aceita como sala. URLs com código de
reunião ou `lookup` são apenas candidatas: os botões de microfone e sair continuam
obrigatórios. Controles sem área de apresentação não mantêm uma chamada ativa
quando o navegador conserva partes antigas da interface. A condição é revalidada
antes de executar um comando. Ter um código aberto não comprova participação.

A execução em abas que o navegador torna inacessíveis depende da solução escolhida
pelo usuário: ativação da aba após clique explícito ou uma integração opcional no
navegador. Não foi adicionada extensão nem ativação automática de abas neste ajuste.
Esta revisão recebeu somente leitura de código e diff, sem testes, type check ou build.
