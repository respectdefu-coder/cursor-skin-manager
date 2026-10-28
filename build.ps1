param([switch]$Test)
$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& (Join-Path $PSScriptRoot 'tools\New-AppIcon.ps1') -OutputPath (Join-Path $output 'app.ico')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)){$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
if(-not (Test-Path -LiteralPath $compiler)){throw '.NET Framework C# compiler was not found.'}
$exe=Join-Path $output 'CursorSkinManager.exe'
$arguments=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll','/reference:System.Xml.dll',('/win32manifest:'+(Join-Path $PSScriptRoot 'src\app.manifest')),('/win32icon:'+(Join-Path $output 'app.ico')),('/out:'+$exe))
foreach($name in @('Engine','History','Library','ColorCorrection','Preview','Window','Checks','Program')){$arguments+=Join-Path $PSScriptRoot ('src\'+$name+'.cs')}
& $compiler $arguments
if($LASTEXITCODE -ne 0){throw 'Compilation failed.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $output 'README.md')
if($Test) {
 $artifacts=Join-Path $PSScriptRoot 'artifacts';New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
 $report=Join-Path $artifacts 'checks.txt'
 $process=Start-Process -FilePath $exe -ArgumentList @('--self-test',('"'+$report+'"')) -WindowStyle Hidden -PassThru -Wait
 if($process.ExitCode -ne 0){if(Test-Path -LiteralPath ($report+'.error.txt')){Get-Content -LiteralPath ($report+'.error.txt')};throw 'Functional checks failed.'}
 Get-Content -LiteralPath $report
}
Get-Item -LiteralPath $exe | Select-Object FullName,Length
