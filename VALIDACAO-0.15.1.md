# Validação — 0.015 Alfa, revisão 1 (0.15.1-alpha)

Solicitação explícita: testes, type check, instalador e publicação no GitHub e landing page.

- Build Release do aplicativo e projeto de checks com `-warnaserror`: zero erros e zero avisos. Inclui verificação de tipos C#/XAML pelo compilador.
- Bateria final: **44 aprovados, 0 falhos, 0 ignorados**. Log completo anexado à release.
- Nova regressão cria uma janela isolada, define AppUserModelID só nessa janela, confirma sua leitura nativa e sua remoção. Também cria um atalho temporário e confirma associação ao executável Explorer.
- Demais checks incluem composição claro/escuro, recorte e ampliação do Dock, capacidade 5/10/15/20 itens, layout, DWM/fechamento individual, preferências, ícones e áudio sem alterar os níveis existentes.
- Bluetooth: 1 adaptador; consulta concluída, 0 endpoints em ambos seletores. Lifecycle retornou 0 watchers. Não comprova descoberta/pareamento/conexão de dispositivos reais. Nenhuma conexão ou rádio foi alterado.
- Publish Release independente, win-x64 single-file, avisos tratados como erros, concluído. Instalador Inno Setup compilado com sucesso.
- Sem teste interativo final no Edge ou instalação da revisão nesta etapa. Os testes de identidade sustentam o mecanismo, mas não comprovam todos os perfis/PWAs/canais de navegador.
- Sem nova comparação em jogos, medição física de latência ou validação prolongada em múltiplos monitores. Instalador sem assinatura digital.