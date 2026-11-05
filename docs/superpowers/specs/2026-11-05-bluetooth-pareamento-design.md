# Bluetooth: pareamento com tema e desconexão sem remover

## Proposta e restrições
Continuar na branch aprovada para os ajustes ou criar outra, conforme escolha do usuário. Janela de pareamento usando FloatingWindow/Acrylic, recursos de tema, nome do dispositivo, confirmação/código/PIN, cancelar e feedback. Usar DeviceInformation.Pairing.Custom e deferral; nunca aceitar um pareamento sem ação do usuário. O Windows ainda pode exigir sua confirmação nativa de segurança.

Desconectar é separado de Esquecer/Remover. Para Bluetooth clássico com endereço válido e suporte do driver, solicitar somente a desconexão via IOCTL_BTH_DISCONNECT_DEVICE no rádio, sem UnpairAsync, desativar adaptador, remover driver ou alterar Registro. Trabalho nativo sob demanda fora do dispatcher; handles liberados. O recurso não promete suporte universal: BLE/perfis sem suporte ou acesso negado mantêm o vínculo e oferecem acesso ao gerenciamento nativo. Não pedir elevação automaticamente nem instalar drivers para isso. A interface deve mostrar falha real, sem simular desconexão.

## Alternativas
Remover/parear novamente não atende ao pedido. Desativar perfis/adaptador é persistente e afeta outros dispositivos, portanto foi descartado. A proposta usa desconexão dirigida quando disponível e preserva o acesso nativo para os demais casos.

## Referências
https://learn.microsoft.com/en-us/windows/uwp/devices-sensors/pair-devices
https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/bthioctl/ni-bthioctl-ioctl_bth_disconnect_device

## Validação e entrega
Revisão por leitura de código/diff. Nenhum teste, pareamento, desconexão de periférico, build, instalador ou publicação sem pedido explícito. APIs/driver podem negar a operação; isso deve ser tratado como limitação real. Falha de teste bloqueia instalador e publicação.