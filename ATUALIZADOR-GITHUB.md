# Atualizador integrado — implementação e validação

## Isolamento

- `feature/atualizador-github`: implementação do atualizador.
- `feature/teclado-virtual`: criada em `9462dd105b661cc5393fc7ba053df80cca712cac`, base da versão 0.22 beta, sem alterações do atualizador. Desenvolvimento do teclado reservado a essa branch.
- `feature/acrylic-tipografia` preservada no mesmo commit. Nenhum merge, push, publicação ou instalação realizado nesta entrega.
- Arquivos locais não rastreados de npm e skill-git.md preservados.

## Arquitetura

`GithubUpdates` consulta a API REST oficial paginada de Releases do repositório definido em `Repository`, atualmente `valteromega2019-blip/think-wind`. Compara o `tag_name` por SemVer (incluindo precedência numérica de pré-versões e ignorando metadados de build). O nome do asset é usado somente para selecionar o instalador, nunca para decidir a versão. Ignora drafts e metadados sem instalador/hash válidos. Instalações estáveis não aderem automaticamente a pré-versões; instalações beta podem receber beta e estável.

`UpdateController` mantém um cliente HTTP e um temporizador de 30 segundos, sem hooks. Consulta no primeiro ciclo seguro e depois a cada seis horas; falhas aguardam quinze minutos. Não consulta durante jogos/fullscreen. Transferências são assíncronas, sem processamento de download no Dispatcher; progresso limitado a aproximadamente sete eventos por segundo. Um download parado por 90 segundos é cancelado. O limite de consulta de 1.000 releases gera erro explícito em vez de afirmar que a busca foi completa.

Configurações → Atualizações exibe versão atual/disponível, notas publicadas como texto, estado, progresso e velocidade média. Botão Baixar e instalar exige confirmação explícita; cancelar interrompe a transferência. Falhas permitem nova tentativa do zero, sem executar arquivos parciais. Arquivos completos recusados são descartados.

O aviso usa `Environment.TickCount64`, tempo de atividade do Windows, e nunca aparece antes de 600.000 ms. Registra a versão em `%LOCALAPPDATA%\OrbyLauncher\Updates\notification.json`, separado das preferências, antes de exibir o balão. O balão não ativa a janela, fecha após quinze segundos e abre diretamente Atualizações ao clicar. Modo Jogo/fullscreen adia sua exibição e fecha um aviso já aberto. Um aviso interrompido por entrada em jogo não será repetido para a mesma versão. Se não for possível persistir, não mostra o aviso, evitando repetições. O tempo de atividade pode incluir suspensão/Fast Startup conforme a sessão do Windows.

## Segurança e instalação

1. Somente HTTPS e URLs do caminho de Releases do repositório oficial; redirecionamentos restritos ao GitHub e hosts de assets usados pelo GitHub.
2. Limites de tamanho, conferência do tamanho transferido e SHA-256 do campo `digest` da API oficial.
3. Authenticode validado pelo Windows (`Get-AuthenticodeSignature`); launcher e instalador devem ter assinatura válida e o mesmo certificado. Não se aceita qualquer certificado apenas porque é válido. O certificado não é baixado de um arquivo de configuração remoto nem é configurável pela interface.
4. Após validar, salvar preferências e copiar settings.json/tutorial.json para uma pasta Backup em Updates. A operação é cancelada se o salvamento/backup falhar.
5. Auxiliar PowerShell separado aguarda a saída do launcher, confere novamente hash/assinaturas e mantém o instalador aberto sem compartilhamento de escrita/exclusão até terminar sua execução.
6. Instalador executa silenciosamente sem reinicialização automática. `/ORBYUPDATE=1` preserva escolhas de inicialização/barra do usuário, evitando a configuração padrão de uma primeira instalação. Não desinstala previamente nem apaga o perfil.
7. Resultado e log do instalador ficam em Updates. O launcher é reaberto após sucesso ou falha, se estiver disponível; a seção Atualizações mostra o último resultado na próxima inicialização.

**Limitação de entrega:** launcher e instaladores publicados atualmente não possuem assinatura digital. A instalação integrada está deliberadamente bloqueada nesse cenário. Para habilitar o ciclo completo é necessário distribuir primeiro uma versão-base assinada e assinar os instaladores futuros com o mesmo certificado confiável. Não foi criada uma identidade de assinatura fictícia nem desativado o UAC. Renovação com troca de certificado exige planejamento de migração da confiança. SHA-256 isolado não autentica o publicador.

## Validação solicitada — 2026-11-01

Comandos inspecionados: csproj não possui alvos customizados de testes; build com analyzers e NuGet audit desativados. Somente a suíte de atualização foi executada; não foram executados lint nem testes gerais de periféricos.

```
dotnet build checks/ThinkWind.Checks/ThinkWind.Checks.csproj -c Release -p:RunAnalyzers=false -p:NuGetAudit=false -o artifacts/checks-updater
dotnet artifacts/checks-updater/ThinkWind.Checks.dll --updates
```

- Compilação: zero avisos e erros.
- Dez grupos aprovados, zero falhas: SemVer; consulta/ausência/drafts/hash ausente; bloqueio sem autorização; download autorizado/hash/adulteração/interrupção/cancelamento; origem restrita; rede/resposta inválida; fronteira de dez minutos/jogos; aviso persistente sem alterar configurações; recusa de arquivo não assinado; consulta real à API oficial.
- A consulta real retornou sem versão elegível mais nova que 0.22.0-beta.
- Download exercitado com transporte HTTP controlado e arquivo de teste; não baixou nem executou instaladores publicados.
- Dez minutos validados por valores de tempo injetados, não por espera real de dez minutos. Persistência exercitada por gravação, fechamento e reabertura em diretório temporário.
- Não foi realizada instalação assinada ponta a ponta, verificação visual do balão durante jogo real nem medição de desempenho nesta entrega. Esses resultados não são declarados aprovados.

## Arquivos

- Serviços novos: ReleaseVersion.cs, GithubUpdates.cs, UpdateController.cs.
- Interface nova: SettingsWindow.Updates.cs, UpdateNotice.cs, App.Updates.cs.
- Integração: App.xaml.cs, SettingsWindow.cs, ThinkWind.csproj.
- Auxiliar: UpdateInstaller.ps1; instalador: installer/ThinkWind.iss.
- Validação: checks/ThinkWind.Checks/UpdateChecks.cs e entrada --updates em Program.cs.

Referências: [GitHub Releases REST](https://docs.github.com/en/rest/releases/releases), [digest dos assets](https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/), [autenticação de assinaturas no Windows](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust).
