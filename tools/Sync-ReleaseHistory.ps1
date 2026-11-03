param([string]$Repository = 'valteromega2019-blip/think-wind')
$ErrorActionPreference = 'Stop'
$gh = (Get-Command gh.exe -ErrorAction SilentlyContinue).Source
if (!$gh) { $gh = Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe' }
$raw = & $gh api ('repos/' + $Repository + '/releases?per_page=100') --paginate --slurp
if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel consultar as releases oficiais.' }
$pages = ($raw -join "`n") | ConvertFrom-Json
$entries = New-Object 'System.Collections.Generic.List[object]'
foreach ($page in $pages) {
    foreach ($release in $page) {
        if ($release.draft) { continue }
        $entries.Add([ordered]@{
            id = $release.tag_name; title = $release.name; state = 'published'
            date = $release.published_at; url = $release.html_url; description = $release.body
        })
    }
}
if ($entries.Count -eq 0) { throw 'Nenhuma release recebida. Historico preservado.' }
$entries.Add([ordered]@{
    id='proxima-versao'; title='Proximas atualizacoes'; state='planned'; date=$null; url=$null
    description='Planejamento em revisao, sem data definida. As proximas alteracoes serao apresentadas aqui quando houver um escopo confirmado. Consulte as versoes publicadas para conhecer os recursos disponiveis.'
})
# Serialize plain entries explicitly. Wrapping ConvertFrom-Json arrays in @()
# and serializing their PowerShell metadata previously produced nested value lists.
$json = ConvertTo-Json -InputObject ($entries.ToArray()) -Depth 5
$path = Join-Path $PSScriptRoot '..\docs\updates.json'
[IO.File]::WriteAllText([IO.Path]::GetFullPath($path), $json, [Text.UTF8Encoding]::new($false))
Write-Output ('Historico sincronizado: ' + ($entries.Count - 1) + ' releases publicadas.')
