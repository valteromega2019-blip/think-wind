# Orby Launcher 0.32.1 Beta

## Iconografia experimental: escolha pelo exemplo

- Personalizar → Aparência apresenta três cartões clicáveis: Original, Com fundo e Sem fundo. Um clique aplica e salva o estilo; a seleção fica marcada e as prévias acompanham a cor de destaque.
- Os três exemplos usam o Orby. O estilo Original mantém o logo colorido; os dois estilos personalizados usam exatamente o PNG monocromático escolhido pelo usuário, preservando sua transparência e proporção.
- O novo símbolo do Orby também está no pacote experimental. Importações manuais por aplicativo continuam tendo prioridade.
- O exemplo Sem fundo mostra a cor de destaque de um aplicativo ativo, sem ativar o experimento ao desenhar a prévia.
- Preservados os destaques de janelas visíveis entre monitores, apps abertos na Biblioteca e os botões de energia lado a lado no Iniciar.

O pacote segue opcional e desligado por padrão para novos perfis. Alguns aplicativos ainda usam símbolos genéricos ou iniciais. Marcas de terceiros pertencem aos respectivos titulares.

## Compilação e limites

Compilação Release e instalador x64. Não foram executados novos testes, lint ou type check separado nesta revisão, conforme a preferência do usuário. Os 69 testes informados na 0.32.0 são de uma revisão anterior e não validam os cartões ou o símbolo agora adicionados. Conferência visual dos exemplos em diferentes fontes/DPI permanece pendente.

Preferências e aplicativos fixados preservados. Instalador com sete idiomas e PawnIO oficial opcional. SHA-256 e assinatura RSA separada acompanham o instalador para o atualizador integrado; não equivalem a Authenticode nem eliminam avisos de editor desconhecido do Windows.
