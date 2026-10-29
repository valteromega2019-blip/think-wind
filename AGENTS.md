# Preferências permanentes do usuário

Não executar testes de qualquer tipo nem type check, salvo quando o usuário solicitar explicitamente. O pedido autoriza somente o escopo solicitado, não execuções automáticas futuras.

Não executar testes ou type check indiretamente por build, lint, hooks ou outros scripts. Inspecionar os comandos antes de executá-los. Não perguntar rotineiramente se deve testar; aguardar pedido explícito. Revisão por leitura de código e diff é permitida.

Quando não solicitados, informar que testes e type check não foram executados conforme a preferência do usuário. Nunca alegar que passaram sem execução.

Não gerar novos instaladores durante o desenvolvimento. Gerar o instalador final somente quando o usuário pedir explicitamente.
