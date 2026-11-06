# Orby Launcher 0.31.1 Beta

Esta atualização contém somente duas correções sobre o pacote 0.31.0 Beta já republicado:

- Relógio: largura mínima reservada para evitar cortes da hora, inclusive com fontes ampliadas, tanto no modo ilha quanto na barra superior.
- Ilha: preparação do tamanho e do layout dos controles de mídia e áudio antes de revelar a expansão parcial, evitando a abertura cortada seguida de redimensionamento visível.

Preservados a agenda experimental, seu mini tutorial, backups e demais recursos da 0.31.0 Beta. A integração experimental com Google Home, o navegador em segundo plano e a dependência Playwright não fazem parte deste pacote.

## Compilação e validação

Pacote Windows x64 em Release. Nesta preparação não foram executados novos testes nem type check, conforme a preferência do usuário. O commit das duas correções teve 132 cenários aprovados na revisão anterior; isso não substitui nova regressão ou conferência visual desta instalação. Fontes personalizadas, escalas DPI e transições ainda devem ser conferidas em uso real.

Configurações preservadas. Instalador com sete idiomas e PawnIO oficial opcional. O SHA-256 e a assinatura RSA separada acompanham o executável para o atualizador integrado. A assinatura separada não é um certificado Authenticode e não elimina avisos do SmartScreen.
