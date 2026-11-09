# Atualizar e desligar — correção de 08/11/2026

Sintoma confirmado pelo usuário: o computador desligava sem instalar a atualização do Windows.

Evidência no código: a detecção usa IsDownloaded, mas o clique chamava diretamente InitiateShutdownW com INSTALL_UPDATES | POWEROFF. Não havia etapa de instalação WUA ou verificação do seu resultado. Conteúdo baixado não equivale a uma atualização preparada pelo Windows para a fase de desligamento. Isso explica a lacuna do fluxo, sem afirmar que o estado real do Windows Update deste PC foi inspecionado.

O clique agora inicia uma função auxiliar do próprio executável, elevada pelo UAC e com janela visível. A interface normal continua sem elevação. O auxiliar consulta apenas metadados locais, seleciona atualizações de software já baixadas, verifica licenciamento/interação/reinício obrigatório e usa IUpdateInstaller.Install. Não aceita EULAs automaticamente, não faz download nem usa comandos não documentados do Update Orchestrator.

Só solicita desligamento depois de ResultCode de sucesso para a operação e para cada atualização. Falhas, consulta parcial, falta de permissão, instalação concorrente ou necessidade de reinicialização impedem o desligamento. Se for preciso reiniciar para concluir, informa que o usuário deve salvar seu trabalho e usar Atualizar e reiniciar do Windows; não reinicia silenciosamente. O caminho anterior não é usado como fallback. Cancelar UAC também não desliga o PC.

A janela elevada não inicializa os serviços normais do launcher. Há proteção contra dois auxiliares instalando simultaneamente, liberação dos objetos COM e execução fora do Dispatcher. O detector continua na inicialização e a cada hora, sem iniciar instalações automaticamente.

Não houve execução da ação, instalação de updates, desligamento, testes, type check ou compilação nesta correção. A revisão foi somente por leitura/diff. O comportamento com as atualizações reais ainda exige validação autorizada. Nenhum instalador ou release foi gerado.

Referências oficiais:
- https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-initiateshutdownw
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdateinstaller-install
- https://learn.microsoft.com/en-us/windows/win32/wua_sdk/iupdateinstallationresult-properties
