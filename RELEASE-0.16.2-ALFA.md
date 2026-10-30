# Think Wind 0.16 Alfa — revisão 2

- Ajustes de enquadramento de janelas e reconhecimento da ilha ao minimizar/restaurar aplicativos em modo de compatibilidade.
- Abertura e restauração pela barra com tentativas limitadas, identificação de aplicativos e suporte à abertura elevada pelo UAC.
- Recuperação de ícones: identidade de aplicativos empacotados, fontes alternativas e rejeição de ícones totalmente transparentes.
- Alça inferior mais visível, maior área de aproximação e permanência aberta enquanto o cursor estiver nessa região.
- Atalho opcional de teclado virtual na ilha, oculto por padrão e configurável em Personalizar.

## Validação e limites

Publish Release com avisos tratados como erros concluído sem erros ou avisos; instalador Inno Setup gerado com sucesso. Sem execução de testes automatizados ou type check separado, conforme preferência do usuário.

O usuário confirmou a abertura de aplicativos, o ajuste de Configurações em uma prévia e o enquadramento inicial do Illustrator. Depois relatou espaço em Configurações e perda do modo da ilha ao restaurar o Illustrator; as últimas correções desses ciclos ainda precisam de confirmação visual.

Aplicativos incompatíveis com redimensionamento maximizado podem ser enquadrados como janela normal ocupando o espaço disponível. Essa alternativa não preserva o estado maximizado nativo e não altera a área útil global do desktop. Aplicativos elevados/UIAccess podem limitar ativação, redimensionamento e Drag & Drop; executar como administrador exige aprovação do UAC.

Instalador sem assinatura digital. Confira o SHA-256 publicado. A instalação preserva preferências e configura o uso da barra do Think Wind e a ocultação automática da barra original conforme o instalador existente. Veja COPYRIGHT.md e SECURITY.md para créditos e segurança.