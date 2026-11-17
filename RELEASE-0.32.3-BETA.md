# Orby Launcher 0.32.3 Beta

- Nova aba Jogos ao lado de Biblioteca no Iniciar, exibida quando jogos são detectados. Jogos usam os ícones originais e ficam separados das outras abas. Leitura de atalhos e registros de instalação da Steam, incluindo jogos sem atalho; preservada a classificação dos atalhos que apontam para steam.exe.
- Prévias com o texto Todos e controles compactos para minimizar ou solicitar o fechamento de todas as janelas do aplicativo. Fechamento usa WM_CLOSE e preserva os avisos de documentos não salvos.
- Clique simples no Dock acessa a janela usada mais recentemente; duplo clique restaura o grupo parcialmente minimizado ou minimiza todas quando o grupo já está restaurado, sem depender de o Dock manter o foco no aplicativo.
- Navegadores abertos têm Abrir nova guia no menu do botão direito.
- Janelas encerradas são removidas das prévias e do Dock sem esperar a próxima enumeração completa. Avisos nativos de salvamento são trazidos à frente após solicitar fechamento.

## Validação

85 testes aprovados: 61 gerais, 10 de grupos de janelas, 9 de iconografia e 5 de jogos. Nenhuma falha. Testes nativos confirmaram minimização de duas janelas e que minimizar novamente não as restaura. Registros locais reconheceram WARDOGS, R.E.P.O., ROB RUN e demos instaladas. Compilação Release x64 e instalador em sete idiomas, com PawnIO oficial opcional, SHA-256 e assinatura RSA separada para o atualizador. A assinatura RSA não é Authenticode.

## Limites

A descoberta de jogos depende dos metadados e atalhos disponíveis; não garante reconhecer jogos portáteis ou todos os títulos da Microsoft Store. Abas do navegador e comandos com privilégios superiores continuam sujeitos às permissões do Windows. O clique simples de grupos aguarda o intervalo de duplo clique configurado no Windows. Testes automatizados não substituem a conferência manual em todos os aplicativos, jogos e monitores.