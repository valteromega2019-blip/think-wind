# Think Wind 0.07 Alfa — 0.7.0-alpha

- Microfone adicionado à primeira expansão por hover, junto à saída de áudio: mesmo controle compartilhado com o painel completo, seleção do endpoint a controlar, ganho percentual e mute por Core Audio. O volume do microfone não muda o dispositivo padrão de captura nem grava áudio.
- Controle reencaixado entre o hover e painel completo, sem duplicar instâncias. Atualização de entrada/saída em ambos os estados, respeitando o uso dos sliders e menus.
- Tecla Windows isolada abre/fecha o Iniciar próprio enquanto o launcher estiver em execução. Combinações como Win+E, Win+R, Win+L e modificadores preexistentes continuam encaminhadas ao Windows. As duas teclas Windows são observadas; eventos sintetizados não provocam recursão.
- Implementação por hook de teclado de baixo nível, sem armazenar texto/histórico de teclas. Ao interceptar a liberação isolada, envia uma sequência neutra Ctrl/liberação de Win para impedir a abertura simultânea do menu original. Se a injeção for bloqueada, encaminha a liberação original ao sistema para não deixar a tecla presa. Hook removido ao sair; telas seguras/aplicativos elevados e políticas do Windows podem limitar o comportamento.
- Metadados e instalador atualizados para 0.7.0-alpha, com ícone personalizado.

## Validação autorizada

`dotnet build src/ThinkWind/ThinkWind.csproj -c Release --nologo -warnaserror`: concluído com **zero erros e zero avisos**, incluindo verificação de tipos C# e compilação XAML. Publicação self-contained single-file `win-x64` com avisos tratados como erros, seguida de compilação Inno Setup.

Instalador: `artifacts/installer/ThinkWind-Setup-0.7.0-alpha.exe`. Nesta etapa o instalador é gerado, sem iniciar automaticamente a instalação ou substituir a instância atualmente aberta.

Suíte de testes e testes interativos não foram executados neste escopo. Microfone, foco do menu, combinações de teclado e ausência de abertura simultânea do Iniciar nativo precisam de avaliação em execução. Os 34 checks históricos da v0.06 não validam automaticamente o novo hook.
