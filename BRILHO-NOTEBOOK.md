# Brilho do painel interno e monitores externos

Branch `fix/brilho-notebook`, baseada na integração da versão 0.23 Beta.

## Causa identificada por leitura

BrightnessService consultava apenas as APIs DDC/CI de dxva2.dll. Não havia consulta ao provedor WMI de brilho do Windows, usado por muitos painéis internos de notebook. Portanto, um notebook pode oferecer controle de brilho nas configurações do Windows enquanto o launcher informa indisponibilidade por DDC/CI. Essa lacuna foi confirmada no código; o comportamento do hardware do notebook do usuário não foi reproduzido nesta etapa.

## Alterações

- Novo caminho `root\wmi`: WmiMonitorBrightness lê valor e níveis suportados; WmiMonitorBrightnessMethods.WmiSetBrightness altera o brilho.
- Associação entre HMONITOR, interface PnP do monitor e InstanceName do WMI. Não escolhe simplesmente o primeiro painel ativo: se a associação falhar ou for ambígua, não altera outra tela.
- Valores WMI são porcentagens absolutas e são aproximados ao nível suportado mais próximo. DDC/CI mantém sua conversão de faixa nativa para 0–100%.
- Após a escrita, consulta novamente o driver. Falha ou recusa não é apresentada como alteração confirmada.
- DDC/CI permanece como alternativa para monitores externos compatíveis. Um desktop também pode controlar brilho se o monitor/driver oferecer suporte; a distinção não depende de presumir o tipo de computador.
- Slider, ícone, percentual e cápsula usam os mesmos recursos visuais do volume e acompanham as cores e transparências do tema.
- Sem provedor compatível, o slider é ocultado e a interface explica a limitação, mantendo o atalho Tela existente.
- Operações fora do Dispatcher, serializadas, com coalescência das mudanças de slider e intervalo de 160 ms entre ciclos de escrita. A leitura periódica existente continua a cada dez segundos somente enquanto a seção de conexões está visível. O temporizador novo só funciona enquanto há uma alteração pendente, parando ao ocultar/descarregar o controle.
- A interação impede o recolhimento da ilha enquanto o slider está sendo usado ou a alteração está em andamento. Não altera gamma, contraste, drivers nem políticas de brilho adaptativo. Não solicita elevação.
- System.Management já está disponível pelas dependências existentes; não foi instalada biblioteca adicional.

## Validação e limites

Revisão por leitura de código e diff. Nenhuma compilação, teste ou type check foi executado, conforme a preferência permanente do usuário. Não foi alterado o brilho deste computador para validar a implementação. São necessárias compilação e conferência no notebook antes de afirmar compatibilidade com seu driver.

Alguns drivers não publicam as classes de brilho, negam operações ou não permitem correlacionar sua interface ao painel. Nesses casos o controle não é simulado e o usuário mantém o acesso às configurações nativas. As opções de timeout WMI são solicitadas ao provedor; não constituem garantia contra um driver travado. Os mesmos limites nativos de DDC/CI existentes continuam válidos.

Arquivos: Services/BrightnessService.cs, Services/NotebookBrightness.cs, Views/BrightnessControl.cs e Views/IslandWindow.cs.

Referências Microsoft: [WmiMonitorBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightness), [WmiMonitorBrightnessMethods](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightnessmethods), [WmiSetBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmisetbrightness-method-in-class-wmimonitorbrightnessmethods).
