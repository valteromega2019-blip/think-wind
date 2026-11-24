# Análise da referência Wireless Device Battery Monitor

24/11/2026. Prévia Orby preview-receptores-g435-x11 compilada e iniciada antes desta análise; zero avisos e erros. Não executados testes.

## Origem e limites
URL fornecida krisgfx9/Wireless-Device-Battery-Monitor retorna 404 nesta sessão. Código consultado no fork notjiccu/wireless-device-battery-monitor, commit 79705baa15412ad1f96bd7c9d3798b5d981c0d2a. A descrição e README preservam as referências ao autor krisgfx9. Lista de releases desse fork vazia; latest retorna 404. Não baixado ou executado binário de terceiro. REA não foi executado: sua skill orienta usar ferramentas normais para repositório completo em código-fonte; não há pacote desse fork para analisar aqui. Não atribuir as conclusões a observação de runtime.

## Evidências de código
- src/hid.cpp: enumera interfaces HID por SetupAPI, consulta capabilities (uso/página/comprimento), abre handles compartilhados, I/O overlapped, espera limitada, cancelamento e fechamento.
- src/drivers/eightbitdo.cpp: consultas vendor nas páginas FF7A/FFA0 usage1, report81, resposta02; comandos 0120 (link), 0121 com subcampo06 (bateria) e 0100 (identidade). Valida eco do comando e tamanho. Modelos listados não incluem 3106; o código tenta coleção vendor em todo VID2DC8 compatível. Não é confirmação de suporte Ultimate V1.
- src/drivers/xinput.cpp: Windows.Gaming.Input para porcentagens reais onde disponíveis, XInput para níveis; filtra disconnected/wired/unknown. Percentual real não garantido no receptor 3106.
- src/drivers/xenta_protocol.cpp: frames03, eventos40 e41. Não ampliar parser Orby para41 sem confirmar semântica do X11; nossa referência direta do modelo confirma40 e alerta 100% transitório.
- src/drivers/logitech_g435_0adf.cpp: protocolo específico de sessão do G435, distinguindo antigo0ACB. Orby usa a referência MIT original separada, não esse código GPL.
- src/drivers/razer.cpp: feature report90bytes (mais ID), comandos classe07/80 bateria e07/84 carga; transaction ID varia por PID. Valida checksum, eco e busy e reconhece colisão do mailbox com Synapse. Não basta consultar só VID1532 indiscriminadamente.

## Plano aplicável ao Orby
1. Adaptador HID nativo compartilhado, limitado a coleções whitelisted; handles seguros, cancelamento de I/O e coleta em worker. Evitar abrir coleções de teclas/movimento. Acesso WinRT recusado continua hipótese até haver log.
2. Reutilizar esse transporte nos leitores G435 e X11 já implementados e comparar leituras no hardware quando autorizado.
3. Adicionar Windows.Gaming.Input para porcentagem de controle quando publicada; preservar níveis XInput quando não.
4. Inventariar apenas descriptors do Ultimate3106 antes de habilitar consultas vendor; aceitar resposta validada, jamais presumir que protocolo Ultimate2 funcione no V1.
5. Razer/Logitech HID++ por whitelist de modelos e coleções, sem copiar o driver GPL. Sem alteração de DPI, pareamento, firmware ou modo permanente.

## Licença
Referência GPL-3.0: não incorporado seu código ao Orby. Documentação de protocolo orienta implementação própria e consulta a referências originais compatíveis; avaliar obrigações antes de qualquer incorporação literal. Fontes baixadas só em artifacts/battery-monitor-reference, fora do pacote e do checkout de código.

Estado: análise concluída; transporte nativo e drivers adicionais não implementados nesta etapa. Não confundir prévia compilada com suporte hardware validado.