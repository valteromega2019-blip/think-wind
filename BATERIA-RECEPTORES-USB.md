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
## Indicadores e identificação de teclado
UI compacta usa BatteryIcon já existente no notebook, com ícone do periférico e barra preenchida; porcentagem retirada da linha principal. Clique abre uma janela Acrylic usando os recursos atuais, com nome completo, leitura e carregamento; Esc/Fechar encerra. Uma janela por vez. Overlay reaproveita os indicadores, permanecendo não clicável como antes. Níveis discretos XInput são representados por preenchimentos categóricos, mantendo o texto real na janela; não exibem porcentagem calculada.

Charging agora é transportado pelos leitores X11 (estado3), Ultimate vendor (bit7) e Windows.Gaming.Input (BatteryStatus.Charging). G435 fornece tensão, não foi inferido carregamento sem fonte. Unknown usa os pontos do BatteryIcon, não uma barra vazia falsamente interpretada como0%.

Inventário do teclado: USB1EA7:0002 descrito pelo barramento como "2.4G RF Keyboard & Mouse". Interfaces HID MI00 e MI01 são do mesmo receptor. HID1D57:FA60 são funções auxiliares do mouse X11. HID046D:C232 pertence a LGHUBDevice, virtual. Exibida identificação genérica do receptor1EA7:0002 e aviso de bateria não informada; marca/modelo físico do teclado personalizado e protocolo da bateria continuam desconhecidos. Sem leitura de teclas, sem abrir a coleção de teclado para captura.

Validação nesta etapa: inventário PnP de leitura e revisão do código/diff. Nenhum teste, type check, build ou execução da nova UI. Necessária conferência visual autorizada antes de afirmar resultado em runtime.
## Investigação do teclado personalizado — 24/11/2026
Inventário PnP somente leitura: USB1EA7:0002, revisão0200, nome de barramento2.4G RF Keyboard & Mouse, input.inf/usb.inf genéricos. Não há propriedade Battery nos filhos HID examinados. Encontradas coleções proprietárias FFB5:0001 (MI01 Col01) e FFBC:0088 (MI01 Col07), além de teclas/mouse/consumer/system. Isso prova canais vendor, não bateria nem protocolo.

Não localizado configurador dedicado em entradas de desinstalação ou pastas principais pesquisadas (Downloads e Program Files por nomes de marcas/teclados). Attack Shark Software e ATTACKSHARKX11.zip são do mouse, não do teclado. Não executados/instalados utilitários de terceiros.

Pesquisa por1EA7:0002 não identificou um protocolo de bateria específico. A referência pública Acer PopGo tem páginaFFB5 mas outros VID/PID; portanto não copiar comandos dela. Também1EA7:0907 de Skyloong/Semitek é PID diferente. Nome de fabricante no catálogo USB não basta para afirmar fabricante da placa personalizada.

Pendente nome físico da base/kit/PCB fornecido pelo usuário para localizar software/firmware adequado. Não enviados comandos a esses canais desconhecidos, não capturadas teclas, não executados testes/type check/build. Nenhuma alteração na leitura do Orby nesta investigação.
## Estabilidade das leituras
Causas observadas no código: Invalidate apagava todas as fontes para qualquer evento HID; UpdateSource substituía fonte por array vazio em timeouts; a UI reconstruía todos os botões em cada alteração de leitura. Corrigido para preservar fontes independentes, confirmar ausência física do receptor antes de retirar apenas a fonte correspondente, e reutilizar Button/BatteryIcon ao atualizar carga. Eventos de conexão Bluetooth são associados ao nome conhecido do endpoint.

Última leitura válida retida por até120s em ausência de resposta, com indicação na janela de detalhes/tooltip; findo prazo, um receptor ainda presente permanece com nível desconhecido, sem perpetuar porcentagem antiga. Desconexões confirmadas removem o dispositivo. Snapshot idêntico não envia evento UI, e fontes duplicadas preferem valor conhecido/fresco. Não são filtros que inventam ou suavizam porcentagens reportadas: dados válidos novos continuam substituindo os anteriores.

Revisão por código e diff; não executados testes, type check, compilação ou nova prévia nesta correção. Efeito visual e desconexão ainda pendentes de conferência autorizada.
## Foto do módulo fornecida pelo usuário
Inscrição visual: BYKC-KBMD-HS6620-V1.0, data2021 04 02 LSH. Correspondência documental exata do hardware em relatório de exposição RF da Shenzhen Sunsonny para família N-J20PRO e variantes (fonte primária arquivada em https://device.report/m/967b06f9584c4cdbf604141be0630f52f30b142fb1f0b42bc8d64149490e0671). Relatório relaciona versão de software N/A, alimentação AAA*2 DC3V, Bluetooth/2.4GHz. Não extrapolar marca/modelo completo ou alimentação do teclado personalizado a partir do módulo comum.

Referência Wireless Device Battery Monitor continua indisponível na conta krisgfx9 via API nesta sessão. Driver generic.cpp do fork disponível reconhece usos HID padrão Battery Strength(0006:0020) e Absolute State Of Charge(0085:0065), consultando descriptors antes de pedir relatórios; recomendações do código evitam sondagem vendor indiscriminada. Não localizado driver explícito HS6620/1EA7:0002. Canais FFB5 e FFBC do receptor não provam que esse firmware forneça bateria.

Possível próximo caminho: identificar endpoint Bluetooth do próprio teclado e verificar Battery Service0x180F/characteristic0x2A19 caso disponíveis. Não mudar conexão/pairing automaticamente e não gravar firmware. Não executados testes, type check, build ou consultas HID desconhecidas nesta investigação. Foto permitiu identificação melhor, não uma integração funcional de bateria.