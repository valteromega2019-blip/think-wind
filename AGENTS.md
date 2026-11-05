# Preferências permanentes do usuário

Não executar testes de qualquer tipo nem type check, salvo quando o usuário solicitar explicitamente. O pedido autoriza somente o escopo solicitado, não execuções automáticas futuras.

Não executar testes ou type check indiretamente por build, lint, hooks ou outros scripts. Inspecionar os comandos antes de executá-los. Não perguntar rotineiramente se deve testar; aguardar pedido explícito. Revisão por leitura de código e diff é permitida.

Quando não solicitados, informar que testes e type check não foram executados conforme a preferência do usuário. Nunca alegar que passaram sem execução.

Não gerar novos instaladores durante o desenvolvimento. Gerar o instalador final somente quando o usuário pedir explicitamente.

Quando uma atualização trouxer funcionalidades muito novas ou mudanças importantes de uso, criar um mini tutorial das novidades, separado do tutorial completo. Permitir pular/dispensar e rever pelas configurações, preservar o progresso anterior e evitar avisos durante jogos ou tela cheia. Não criar guias para toda correção pequena.

## Bloqueio de entrega por falha em testes

Se qualquer teste executado apresentar falha, interromper imediatamente a geração do instalador e a publicação. Informar a falha e consultar o usuário antes de prosseguir. Corrigir a causa e repetir os testes pertinentes somente com autorização explícita, conforme a regra de testes acima. Não continuar o empacotamento ou publicar apenas documentando a falha como limitação. Retomar a entrega somente após a correção e a confirmação de que os testes pertinentes passaram.