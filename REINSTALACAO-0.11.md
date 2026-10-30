# Reinstalação local — 0.11 Alfa corrigida

- Instalação anterior removida pelo desinstalador oficial (código 0); preferências preservadas e copiadas para artifacts/reinstall-v0.11/settings-before.json (arquivo local, não versionado).
- Publish Release win-x64 self-contained/single-file com avisos como erros e símbolos portáveis: concluído sem erros/avisos.
- Instalador Inno Setup compilado com sucesso. Instalação corrigida: código 0.
- Configuração automática executada pelo instalador sem falha registrada; HideWindowsTaskbar confirmado como true. O comando verifica ABS_AUTOHIDE por leitura de retorno antes de salvar a preferência.
- Processo instalado aberto e Responding=true; nenhum evento de falha ThinkWind.exe registrado na janela de observação inicial. Isso não comprova ausência de falhas prolongadas.
- Sem suíte automatizada de regressão ou type check separado nesta etapa. Transições com janela pequena/maximizada e convivência prolongada com Think Monitor ainda precisam de confirmação interativa.
- Instalador local: artifacts/reinstall-v0.11/ThinkWind-Setup-0.11.0-alpha-corrected.exe. Não substitui a release pública nesta etapa.