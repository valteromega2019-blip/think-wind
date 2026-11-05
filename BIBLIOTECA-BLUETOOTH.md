# Biblioteca e Bluetooth — alterações em desenvolvimento

## Biblioteca
Botão direito em um aplicativo da Biblioteca/Todos → Ocultar da biblioteca. Use Ocultos para ver os itens ocultados manualmente e restaurar um item ou todos. A escolha é salva; não desinstala nem remove fixados do Dock ou do Iniciar. Apps identificados pelo AppUserModelID não são confundidos com outros IDs que usam o mesmo executável. A pesquisa em Todos também filtra atalhos/executáveis ocultos recebidos do índice do Windows.

O filtro automático usa nomes, destinos e IDs específicos de ferramentas administrativas, manutenção e atalhos auxiliares, sem excluir programas só porque contêm palavras como configuração/setup. Aplicativos cotidianos e fixados explícitos são preservados. Os itens eliminados pelo filtro automático não são a lista de Ocultos manuais.

## Bluetooth
Pareamento com confirmação, exibição/comparação de código e entrada segura de PIN em janela com o tema/Acrylic da central. Cancelar/Esc e fechamento do painel cancelam a interação. Nenhum PIN é salvo ou registrado pelo novo fluxo. O Windows ainda pode exibir sua própria confirmação de segurança.

Desconectar e Esquecer dispositivo são ações separadas. Desconectar solicita a interrupção da conexão Bluetooth clássica por endereço, quando o rádio/driver permite, mantendo o pareamento. Não desliga adaptadores, não desativa perfis e não instala drivers. Nenhuma elevação automática. A interface só diz que a solicitação foi aceita; o estado Conectado vem dos eventos do Windows.

Para BLE, falta de endereço/protocolo ou driver sem suporte, use Gerenciar conexão/Configurações do Windows, mantendo o vínculo. Dispositivos podem reconectar automaticamente conforme o próprio funcionamento. Esquecer dispositivo continua sendo uma ação explícita separada e pede confirmação.

## Validação
Revisão independente por leitura identificou uma reaparição via índice e uma colisão de identidade; os pontos foram ajustados. Não foram executados testes, type check, build, operações com hardware, instalador, instalação ou publicação. A implementação ainda precisa ser compilada e validada quando solicitado. A falha Bluetooth da entrega anterior permanece sem nova execução e bloqueia a geração de instalador.

Referências: https://learn.microsoft.com/en-us/windows/uwp/devices-sensors/pair-devices e https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/bthioctl/ni-bthioctl-ioctl_bth_disconnect_device .