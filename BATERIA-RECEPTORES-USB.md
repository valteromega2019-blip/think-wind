# Bateria por receptores USB — investigação e primeira camada

Branch feature/barra-superior-fixa-escala, 24/11/2026.

Implementado sem executar testes ou build:
- Bateria de dispositivos presentes cujo driver publica System.Devices.BatteryLife, restrito a identidades USB/HID com VID.
- XInputGetBatteryInformation para os quatro slots, somente leitura uma vez por minuto na coleta compartilhada. Apenas tipos alcalina/NiMH válidos; não representar níveis discretos como porcentagens inventadas. Ignorar wired/unknown/disconnected.
- UI e overlay aceitam porcentagem ou nível textual.

Inventário observado: Logitech G435 046D:0ADF e receptor 8BitDo 2DC8:3106. Nenhum Razer ou VID conhecido Attack Shark foi identificado nesta leitura; mouse/teclado HID genéricos também presentes.

Não implementado ainda: protocolos proprietários HID++ da Logitech, G435 por sessão CRC, Razer e Attack Shark/Rongyuan/Xenta. Exigem whitelist por PID, coleção/usage exatas, timeout, isolamento em worker, liberação de handles e verificação de compatibilidade/licença. Não presumir que suporte de um modelo se aplica à marca toda. Não copiar driver GPL para o projeto sem avaliar licença.

Fontes primárias consultadas:
- https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_battery_information
- https://pwr-solaar.github.io/Solaar/features/
- https://github.com/HeyOkay/HaloBattery/blob/main/docs/protocols.md
- https://github.com/notjiccu/wireless-device-battery-monitor (inventário de protocolos; código GPL não incorporado)
- https://github.com/portyporty/g435-battery-percentage (referência apontada pelo protocolo G435; ainda não incorporada)

Pendente: modelos exatos, implementação dos protocolos proprietários autorizados e conferência real. Não declarar todas as marcas suportadas. Compilação, testes, hardware e type check não executados neste pedido.
## Continuação — G435 046D:0ADF
Adicionado transporte WinRT HID na coleção exata FF00:0001 do receptor 046D:0ADF. Consulta a tensão usando a sessão documentada por portyporty/g435-battery-percentage (MIT; licença incluída no pacote), com CRC-16/XMODEM, fila de até 32 respostas e limite de quatro segundos. Fechamento da sessão em finally, com prazo separado de 300 ms, seguido de remoção do evento e Dispose. Sem threads dedicadas, sem alterar volume/microfone/pareamento/firmware. A seleção remota do protocolo é transitória; o comando de sessão não é uma simples leitura genérica de HID. Não aplicado ao receptor antigo 0ACB.

Porcentagem estimada pela curva de tensão da referência, identificada com ≈ e descrição. Falha de acesso ou ausência de resposta não gera um valor fictício. Requer conferência no hardware; WinRT pode recusar acesso à coleção conforme permissões/driver. Não compilado nem testado nesta alteração, por preferência do usuário. Logitech HID++, Razer e Attack Shark continuam sem protocolo específico integrado. 8BitDo usa a leitura XInput anterior, quando o driver oferece nível válido; não prometer porcentagem exata nem chamar conexão wired de bateria cheia.
## Attack Shark X11 e 8BitDo Ultimate V1
Usuário confirmou modelos. X11: implementada escuta passiva da coleção 000A:0000 do receptor 1D57:FA60, report 03 55 40 ST NN. Exigir dois níveis consecutivos iguais para rejeitar 100% transitório ao reconectar. Janela máxima seis segundos uma vez por minuto, sem escritas; cancelar e liberar handle/evento ao terminar. Fonte: https://github.com/vasfvitor/attackshark-x11/blob/main/docs/PROTOCOL.md e observação de frames iniciais em https://github.com/daeoxide/attackshark-battery-windows/blob/main/attackshark_battery/protocol.py. Implementação própria, sem incorporar código desses projetos. WinRT pode negar acesso: ainda sem compilação ou verificação no hardware.
8BitDo Ultimate V1: identidade conectada 2DC8:3106, modo XInput. Nível discretizado já suportado quando fornecido; não converter artificialmente em %. Protocolo de porcentagem exata do modelo ainda não confirmado. Não aplicado protocolo do Ultimate 2 ou Pro 2 a esse PID.

## Transporte nativo e resposta à conexão
Implementado PeripheralHid próprio com cfgmgr32, capabilities hid.dll, SafeFileHandle/FileStream assíncrono, compartilhamento de leitura/escrita e I/O cancelável. Escopado a VID/PID/página/usage exatos. X11 e G435 migrados do WinRT para esse transporte. G435 continua usando referência MIT; nenhum driver GPL incorporado.

Ultimate V1 2DC8:3106: adicionada tentativa de telemetria nas coleções FF7A/FFA0 usage1 somente quando existentes, comandos de link e bateria com eco/tamanho/subcampo validados e timeout. Leitura Windows.Gaming.Input quando publica capacidade restante/total; fallback identifica receptor com "carga não informada", sem fabricar nível ou porcentagem. O caminho vendor no 3106 permanece não validado no hardware.

Atraso anterior: cache fixo de60s e publicação só após todos os leitores; receptor Ultimate descartado por emular XInput wired. Agora watchers nativos HID/Bluetooth invalidam snapshots na conexão; cache de apoio10s; fontes independentes preservam outros valores durante uma leitura e são publicadas assim que terminam. Resultados anteriores a uma mudança de conexão são rejeitados por revisão. Watchers encerrados no OnExit. Sem consulta de teclas/eixos e sem serviço administrativo.

Centralização: statusHost Grid com a mesma largura total da faixa de status; indicadores centrados nessa camada em vez de LastChildFill do DockPanel. Máximo34% da largura disponível para conter a área central. Rodapé expandido mantém seu alinhamento ao painel.

Validação: revisão de código/diff apenas. Nenhum teste, type check, compilação, execução ou leitura experimental no hardware nesta etapa. Pendente conferir a resposta real do Ultimate e a convivência de áudio do G435 no runtime. Não declarar esses dispositivos validados sem execução autorizada.