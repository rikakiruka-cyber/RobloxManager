# One-click release: bump version -> build exe -> push code -> create GitHub Release (what other people's apps update from).
# 1 cu bam: tang version -> build exe -> day code -> tao GitHub Release (app cua nguoi khac se tu cap nhat tu day).

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
[Console]::InputEncoding  = [Text.Encoding]::UTF8
$Root = Split-Path $PSScriptRoot -Parent
Set-Location $Root

function Say($en, $vi)  { Write-Host $en -ForegroundColor Cyan; Write-Host "  $vi" -ForegroundColor DarkGray }
function Die($en, $vi)  { Write-Host "[ERROR] $en" -ForegroundColor Red; Write-Host "        $vi" -ForegroundColor Red; exit 1 }
function Run {
    $exe = $args[0]; $rest = @(); if ($args.Count -gt 1) { $rest = $args[1..($args.Count - 1)] }
    & $exe @rest
    if ($LASTEXITCODE -ne 0) { throw "Command failed ($LASTEXITCODE): $exe $($rest -join ' ')" }
}

# ---------- 1. Requirements ----------
foreach ($t in @(
    @{n='git';    h='https://git-scm.com/download/win'},
    @{n='gh';     h='winget install GitHub.cli   (then: gh auth login)'},
    @{n='dotnet'; h='https://dotnet.microsoft.com/download  (.NET SDK 10)'})) {
    if (-not (Get-Command $t.n -ErrorAction SilentlyContinue)) {
        Die "'$($t.n)' is not installed: $($t.h)" "Chua cai '$($t.n)': $($t.h)"
    }
}
& gh auth status *> $null
if ($LASTEXITCODE -ne 0) { Die "Not logged in to GitHub. Run: gh auth login" "Chua dang nhap GitHub. Chay: gh auth login" }

# ---------- 2. Repo + version ----------
$appInfo = Get-Content 'App\Updater.cs' -Raw -Encoding UTF8
if ($appInfo -notmatch 'UpdateRepo\s*=\s*"([^"]+)"') { Die "UpdateRepo not found in App\Updater.cs" "Khong thay UpdateRepo trong App\Updater.cs" }
$Repo = $Matches[1]

$csproj = 'App\RobloxRAMManager.csproj'
$csprojBackup = Get-Content $csproj -Raw -Encoding UTF8
if ($csprojBackup -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') { Die "<Version> not found in csproj" "Khong thay <Version> trong csproj" }
$cur = [version]$Matches[1]

Write-Host ""
Write-Host "Repo: $Repo      Current version / Phien ban hien tai: $cur" -ForegroundColor Green
Write-Host "  [1] Patch  $($cur.Major).$($cur.Minor).$($cur.Build + 1)   (bug fixes / sua loi nho)  <- default"
Write-Host "  [2] Minor  $($cur.Major).$($cur.Minor + 1).0   (new features / them tinh nang)"
Write-Host "  [3] Major  $($cur.Major + 1).0.0   (big change / thay doi lon)"
$c = Read-Host "Choose 1/2/3 or type a version like 2.7.0 / Chon 1/2/3 hoac go so version"
if     ($c -match '^\d+\.\d+\.\d+$') { $new = [version]$c }
elseif ($c -eq '2') { $new = [version]"$($cur.Major).$($cur.Minor + 1).0" }
elseif ($c -eq '3') { $new = [version]"$($cur.Major + 1).0.0" }
else                { $new = [version]"$($cur.Major).$($cur.Minor).$($cur.Build + 1)" }
if ($new -le $cur) { Die "New version must be greater than $cur" "Version moi phai lon hon $cur" }
$tag = "v$new"

& gh release view $tag --repo $Repo *> $null
if ($LASTEXITCODE -eq 0) { Die "Release $tag already exists" "Release $tag da ton tai" }

# ---------- 3. Release notes (VI + EN) ----------
# Notes are typed in Notepad (the console loses Vietnamese accents when typing).
# Ghi chu nhap trong Notepad (go tieng Viet truc tiep trong CMD bi mat dau).
$inFile = Join-Path $env:TEMP "rm_notes_input.txt"
$tpl = "# v$new - What's new / Co gi moi`r`n# One item per line under each heading. Lines starting with # are ignored. Save (Ctrl+S) and close Notepad.`r`n# Moi dong 1 y duoi moi muc. Dong bat dau bang # se bi bo qua. Luu (Ctrl+S) roi dong Notepad.`r`n`r`n[vi]`r`n`r`n[en]`r`n"
[IO.File]::WriteAllText($inFile, $tpl, (New-Object Text.UTF8Encoding $true))
Write-Host ""
Say "Notepad will open: write the notes, save, then close it." "Notepad se mo: ghi chu, luu, roi dong lai."
Start-Process notepad.exe -ArgumentList "`"$inFile`"" -Wait

$notesVi = @(); $notesEn = @(); $sec = ''
foreach ($l in (Get-Content $inFile -Encoding UTF8)) {
    $t = $l.Trim()
    if ($t -match '^\[(vi|en)\]$') { $sec = $Matches[1]; continue }
    if ($t -eq '' -or $t.StartsWith('#')) { continue }
    $t = $t -replace '^[-*\u2022]\s*', ''
    if ($sec -eq 'vi') { $notesVi += "- $t" } elseif ($sec -eq 'en') { $notesEn += "- $t" }
}
if ($notesVi.Count -eq 0 -and $notesEn.Count -eq 0) { $notesVi = @('- Cải thiện và sửa lỗi nhỏ'); $notesEn = @('- Improvements and minor fixes') }
elseif ($notesVi.Count -eq 0) { $notesVi = $notesEn }
elseif ($notesEn.Count -eq 0) { $notesEn = $notesVi }

$body = "<!--vi-->`n### Tiếng Việt`n$($notesVi -join "`n")`n`n<!--en-->`n### English`n$($notesEn -join "`n")`n"
$notesFile = Join-Path $env:TEMP "rm_release_notes.md"
[IO.File]::WriteAllText($notesFile, $body, (New-Object Text.UTF8Encoding $false))

# ---------- 4. Bump version + update CHANGELOG.json + build ----------
$clFile   = Join-Path $Root 'CHANGELOG.json'
$clBackup = if (Test-Path $clFile) { Get-Content $clFile -Raw -Encoding UTF8 } else { $null }
try {
    # Append this version to CHANGELOG.json (embedded in the exe -> Settings > Update history)
    # Them version nay vao CHANGELOG.json (nhung vao exe -> Cai dat > Lich su cap nhat)
    $entries = @()
    if ($clBackup) { $entries = @($clBackup | ConvertFrom-Json) }
    $entries = @($entries | Where-Object { $_.version -ne "$new" })
    $plainVi = @($notesVi | ForEach-Object { $_ -replace '^- ', '' })
    $plainEn = @($notesEn | ForEach-Object { $_ -replace '^- ', '' })
    $newEntry = [pscustomobject]@{ version = "$new"; date = (Get-Date -Format 'yyyy-MM-dd'); vi = $plainVi; en = $plainEn }
    $entries = @($newEntry) + $entries
    $clJson = ConvertTo-Json -InputObject $entries -Depth 5
    [IO.File]::WriteAllText($clFile, $clJson, (New-Object Text.UTF8Encoding $false))

    $bumped = $csprojBackup -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$new</Version>"
    [IO.File]::WriteAllText((Resolve-Path $csproj), $bumped, (New-Object Text.UTF8Encoding $false))

    Say "Building $tag ..." "Dang build $tag ..."
    & cmd.exe /c "build_exe.bat nopause"
    $exe = Join-Path $Root "Release_EXE\RobloxManager_${new}_x64.exe"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw "Build failed / Build that bai" }
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Say "Built: $exe  ($mb MB)" "Da build: $exe  ($mb MB)"

    # ---------- 5. Git: commit + push ----------
    if (-not (Test-Path '.git')) { Run git init -b main }
    $origin = (& git remote get-url origin 2>$null)
    if (-not $origin) { Run git remote add origin "https://github.com/$Repo.git"; $origin = "https://github.com/$Repo.git" }

    if (-not (& git config user.name))  { $login = (& gh api user --jq .login); Run git config user.name $login }
    if (-not (& git config user.email)) { $login = (& gh api user --jq .login); $uid = (& gh api user --jq .id); Run git config user.email "$uid+$login@users.noreply.github.com" }

    Run git add -A
    Run git commit -m "$tag"

    & git fetch origin *> $null
    & git rev-parse --verify origin/main *> $null
    if ($LASTEXITCODE -eq 0) {
        & git merge-base HEAD origin/main *> $null
        if ($LASTEXITCODE -ne 0) {
            Say "Remote already has files (e.g. README) - merging..." "Remote da co file (vd README) - dang gop..."
            Run git pull origin main --allow-unrelated-histories -X ours --no-edit
        }
    }
    Run git push -u origin main

    # ---------- 6. GitHub Release ----------
    $args2 = @('release', 'create', $tag, $exe, '--repo', $Repo, '--title', $tag, '--notes-file', $notesFile)
    if ($origin -match [regex]::Escape($Repo)) { $args2 += @('--target', 'main') }
    Run gh @args2
}
catch {
    [IO.File]::WriteAllText((Resolve-Path $csproj), $csprojBackup, (New-Object Text.UTF8Encoding $false))
    if ($null -ne $clBackup) { [IO.File]::WriteAllText($clFile, $clBackup, (New-Object Text.UTF8Encoding $false)) }
    elseif (Test-Path $clFile) { Remove-Item $clFile -Force }
    Write-Host ""
    Die "$($_.Exception.Message)" "Da dung va khoi phuc version cu / Stopped and restored the old version."
}

Write-Host ""
Write-Host "DONE! / XONG!  $tag" -ForegroundColor Green
Write-Host "https://github.com/$Repo/releases/tag/$tag"
Write-Host "Other people's apps will offer the update on their next start (or via Settings > Check for updates)." -ForegroundColor Cyan
Write-Host "  App cua nguoi khac se hien thong bao cap nhat o lan mo toi (hoac Cai dat > Kiem tra cap nhat)." -ForegroundColor DarkGray
