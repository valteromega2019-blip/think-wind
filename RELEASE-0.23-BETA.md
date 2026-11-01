# Orby Launcher 0.23 Beta

## Novidades

- Teclado próprio com o tema do launcher, layouts PT-BR/US, acentos, modificadores, navegação e painel numérico opcional. Acesso pelo ícone existente e por Personalizar → Teclado.
- Teclado redimensionável pelas bordas/cantos, dimensões salvas automaticamente, teclas ampliadas por proximidade e afastamento lateral das vizinhas. O fundo acompanha material e opacidade da barra superior.
- Calculadora no lugar do antigo bloco de atalhos de notificações. O acesso às notificações da barra principal continua disponível.
- Clima reorganizado, com temperatura em destaque e sensação/mínima/máxima maiores.
- Seletores de módulos com seus próprios ícones e contraste no estado ativo.
- Correção da sincronização do Acrylic ao mover janelas de personalização.
- Nova seção Atualizações: consulta ao GitHub, comparação SemVer, notas, download após confirmação e progresso. Aviso por versão somente após dez minutos de atividade do Windows, adiado durante jogos/tela cheia.

## Limites importantes

O instalador e o launcher ainda não possuem assinatura digital. A instalação pelo atualizador integrado está bloqueada até existir uma versão-base assinada e instaladores com assinatura confiável do mesmo publicador. Esta release pode ser baixada e instalada manualmente; o SHA-256 publicado permite conferir a integridade, não substitui a autenticação do publicador.

O teclado próprio respeita as permissões do Windows. Predição, varredura, tela de entrada e operações protegidas continuam disponíveis no teclado nativo pelo botão Teclado Windows. Não há bypass de UAC nem equivalência integral de acessibilidade com osk.exe.

## Empacotamento

Distribuição Windows x64 autocontida, com ícone Orby e PawnIO oficial opcional para sensores. Preferências existentes são mantidas no perfil local; a atualização integrada prevê backup e não reaplica opções de primeira instalação.

Compilação e geração do instalador solicitadas pelo usuário. Nenhum novo teste, lint ou type check separado foi executado nesta publicação. Os testes anteriores do atualizador não validam as alterações posteriores do teclado e widgets. Acabamento, redimensionamento e interação em diferentes monitores ainda dependem de validação em uso real.

Consulte também ATUALIZADOR-GITHUB.md, TECLADO-VIRTUAL.md e CALCULADORA-WIDGET.md.
