param([string]$Executable)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $Executable){$Executable=Join-Path $repo 'dist\CursorSkinManager.exe'}
$expected=@('.gitignore','README.md','LICENSE','build.ps1','tools/New-AppIcon.ps1','tools/Audit-Package.ps1','src/Engine.cs','src/History.cs','src/Library.cs','src/ColorCorrection.cs','src/Preview.cs','src/Window.cs','src/Program.cs','src/Checks.cs','src/app.manifest','.github/workflows/build.yml','downloads/CursorSkinManager.exe')
$files=@(Get-ChildItem -LiteralPath $repo -Recurse -File | Where-Object {
 $relative=$_.FullName.Substring($repo.Length+1).Replace('\','/')
 $relative -notmatch '^(\.git|dist|artifacts)/'
})
foreach($file in $files) {
 $relative=$file.FullName.Substring($repo.Length+1).Replace('\','/')
 if($relative -notin $expected){throw "Unexpected publish file: $relative"}
 if($file.Extension -eq '.exe') {
  if($relative -ne 'downloads/CursorSkinManager.exe'){throw 'Unexpected executable'}
 }else {
  $content=[IO.File]::ReadAllText($file.FullName)
  # No private absolute paths, downloadable artwork, or legacy embedded theme code.
  if($content -match '(?i)[A-Z]:[\\/](Users|ProgramData)[\\/]' -or $content -match 'GetManifestResourceStream\('){throw "Private/legacy material reference: $relative"}
 }
}
$assembly=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($Executable))
if($assembly.GetManifestResourceNames().Length -ne 0){throw 'Executable has embedded resources'}
if((Get-Item -LiteralPath $Executable).Length -gt 1024*1024){throw 'Unexpectedly large executable; inspect for bundled assets'}
[pscustomobject]@{CheckedFiles=$files.Count;EmbeddedResources=0;ExecutableBytes=(Get-Item -LiteralPath $Executable).Length;Result='PASS'}
