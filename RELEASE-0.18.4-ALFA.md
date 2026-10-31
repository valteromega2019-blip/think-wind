# Think Wind 0.18 Alfa — revisão 4

- Os ícones usados na ilha fechada permanecem na primeira linha; os controles adicionais aparecem em uma segunda linha nas expansões parcial e completa.
- Configurações do launcher e alternância de tema acompanham a linha secundária do painel completo.
- O botão do diagnóstico mostra o ícone da área principal enquanto o monitoramento está aberto e alterna corretamente entre as duas áreas, inclusive na linha secundária.
- Mantém as correções da revisão 3 para sensores, permissões, arrastar arquivos e dimensionamento do painel.

## Validação e limites

Publish Release win-x64 self-contained e instalador Inno Setup concluídos. Testes e type check separados não foram repetidos nesta revisão, conforme a preferência do usuário. Os 47 checks aprovados pertencem à revisão anterior.

O usuário confirmou que o FPS foi testado manualmente. Não houve nova medição independente de precisão ou latência. A temperatura real da CPU também foi observada na prévia das correções de sensores.

O pacote inclui o runtime e PawnIO oficial opcional; uma instalação compatível do driver é preservada. A atualização mantém as preferências e pode solicitar confirmação administrativa para atualizar os auxiliares protegidos. A interface permanece com permissões normais para receber arquivos arrastados.

Versão Alfa, sem assinatura digital. Publicada da branch `feature/inicializacao-prioritaria`, sem merge automático em `main`.

Instalador: `ThinkWind-Setup-0.18.4-alpha.exe` — 63812360 bytes.

SHA-256: `e429d891ae0df4cd31b2c8e4aedcba88382085853a1c4bef8e5c6920e72d305a`.
