# Biblioteca e Bluetooth — plano de implementação

> Execução direta na branch feature/barra-inferior-fixa, aprovada pelo usuário; revisão independente por leitura ao final. Sem testes/type check/build nesta etapa.

**Objetivo:** ocultação manual reversível e filtros de biblioteca, pareamento com tema e desconexão dirigida sem remover vínculo.
**Specs:** docs/superpowers/specs/2026-11-05-biblioteca-ocultar-design.md e 2026-11-05-bluetooth-pareamento-design.md, aprovadas.
**Tecnologias:** .NET/WPF, WinRT CustomPairing, Win32 Bluetooth radio/DeviceIoControl.

## Tarefa 1 — Biblioteca
- [x] Acrescentar identidade resolvida opcional a AppEntry e lista persistente HiddenLibraryApps, normalizada e limitada.
- [x] Reforçar AppVisibility por nomes/arquivos/IDs administrativos específicos e documentos/auxiliares; preservar aplicativos cotidianos e não filtrar por palavras genéricas.
- [x] Acrescentar Mostrar ocultos/Restaurar todos e ação Ocultar/Restaurar no menu existente. Biblioteca/Todos usam identidade estável; fixados permanecem acessíveis. Nada é desinstalado.
- [x] Revisar filtros, persistência e atualização imediata por leitura; commit localizado.

## Tarefa 2 — Bluetooth
- [x] Criar BluetoothPairingWindow usando FloatingWindow/tema da central, confirmação, exibição de PIN e entrada segura de PIN, Cancelar/Esc e liberação de resultado ao fechar.
- [x] Integrar Pairing.Custom/PairingRequested/deferral, consentimento explícito e cancelamento ao ocultar/fechar o painel; sem autoaceitação.
- [x] Criar Native/BluetoothConnection: somente endereço clássico validado, enumeração/fechamento de rádios, DeviceIoControl IOCTL_BTH_DISCONNECT_DEVICE sob demanda e fora do dispatcher. Falha/acesso negado sem elevação automática, sem desligar rádio/perfil ou remover pareamento.
- [x] Separar Desconectar de Esquecer dispositivo; confirmar ação, observar estado real via watcher; fallback para gerenciamento Windows quando não suportado.
- [x] Revisão independente de privacidade, recursos, handles, deferrals, foco, cancelamento e compatibilidade por leitura. Sem parear/desconectar hardware real, testes, build ou instalador.

## Pontos de revisão
Identidade muda entre atalhos/AppId; filtros que ocultam IDEs/jogos/Configurações; atalhos já fixados; restauração após reiniciar; dispositivos BLE/sem endereço; recusa de permissão; fechamento durante PIN; deferrals e handles nunca retidos; cancelamento sem remover vínculo; não registrar PIN em diagnóstico.

## Validação futura, somente quando solicitada
Persistência/normalização e filtros com amostras reais; ocultação/restauração em UI; cerimônias ConfirmOnly/ConfirmPinMatch/DisplayPin/ProvidePin com cancelamento; falhas nativas e ausência de Unpair na desconexão; confirmação do estado com hardware autorizado. Qualquer falha bloqueia instalador/publicação.
## Registro da execução
- Ocultação persistente por AppId/destino e restauração implementadas; filtro específico ampliado. Mapa de destinos já resolvidos cobre o índice sem reconsultar Shell por item.
- Ruling: entradas com AppId próprio usam sua identidade antes do destino, preservando perfis/PWAs que compartilham executável; o mapa de destino resolve entradas do índice sem identidade.
- Pareamento customizado usa janela temática e deferral com liberação no finally. Cancelamento fecha a janela; não aceita sem consentimento. Código ausente para comparação não é aceito.
- Desconexão usa a solicitação nativa apenas em Classic com endereço validado, sem elevação. BLE/driver sem suporte mantém acesso nativo. Não foi usado UnpairAsync no fluxo de desconexão.
- Revisão independente por leitura apontou reaparição pelo índice e colisão entre AppIds; ajustes realizados. Nenhum teste/type check/build executado, conforme regra do usuário. Nenhum periférico foi alterado.