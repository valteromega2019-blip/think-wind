# Segurança do Think Wind

Esta é uma versão Alfa. A seção Personalizar → Sobre e segurança reúne os links oficiais e as limitações conhecidas.

## Distribuição oficial

- Código e releases: https://github.com/valteromega2019-blip/think-wind
- Apresentação: https://valteromega2019-blip.github.io/think-wind-site/
- Compare o SHA-256 do instalador com o arquivo de checksums da mesma release. Isso confirma correspondência com o artefato publicado; não substitui assinatura digital ou auditoria de segurança.
- Os instaladores Alfa ainda não têm assinatura digital. A instalação não exige desativar antivírus, proteção do Windows ou outras medidas de segurança.

## Integração e dados locais

Preferências são armazenadas em %LOCALAPPDATA%\ThinkWind\settings.json. O instalador configura a ocultação automática da barra do Windows e o uso da barra do launcher. A troca explícita de tema também pode alterar o modo claro/escuro do Windows. Essas opções são ajustáveis pelo usuário. O widget de clima consulta o serviço Open-Meteo para a cidade escolhida.

A opção Desinstalar abre o mecanismo registrado pelo programa ou o gerenciamento nativo do Windows. O launcher não trata a exclusão de arquivos do aplicativo como desinstalação.

## Limites conhecidos

Bandeja, áudio, Bluetooth e redimensionamento dependem do aplicativo, dispositivo e permissões. O Windows pode impedir operações sobre processos elevados. O aplicativo é experimental; não há alegação de ausência de vulnerabilidades nem validação universal de latência ou compatibilidade.

## Relatos

Consulte a guia Security do repositório para verificar os canais privados disponíveis. Não publique credenciais, chaves, dados pessoais, dumps ou logs sensíveis em issues públicas. Se não houver canal privado disponível, envie apenas uma descrição não sensível para solicitar um meio de contato apropriado.