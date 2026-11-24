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