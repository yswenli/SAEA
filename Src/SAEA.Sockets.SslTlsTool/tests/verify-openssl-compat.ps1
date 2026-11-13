param([string]$ToolPath)
if ($PSVersionTable.PSVersion.Major -lt 7) { [Console]::Error.WriteLine('需要 PowerShell 7（pwsh）。'); exit 2 }
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..'
$script:passed = 0; $script:failed = 0

function Invoke-Tool {
    param([string[]]$Arguments)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # 采集 stderr 时不能中断脚本
    try {
        $raw = if ($ToolPath) { & $ToolPath @Arguments 2>&1 } else { & dotnet run --project $project -c Release -- @Arguments 2>&1 }
        $code = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previous }
    $out = [System.Collections.Generic.List[string]]::new(); $err = [System.Collections.Generic.List[string]]::new()
    foreach ($item in $raw) {
        if ($item -is [System.Management.Automation.ErrorRecord]) { $err.Add($item.ToString()) } else { $out.Add([string]$item) }
    }
    [pscustomobject]@{ ExitCode = $code; StdOut = ($out -join "`n"); StdErr = ($err -join "`n") }
}
function Invoke-Case {
    param([string]$Name, [scriptblock]$Body)
    try {
        if ((& $Body) -eq $true) { $script:passed++; Write-Host "PASS $Name" } else { $script:failed++; Write-Host "FAIL $Name：断言未通过" }
    }
    catch { $script:failed++; Write-Host "FAIL $Name：$($_.Exception.Message)" }
}
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('saea-ssltls-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
try {
    Invoke-Case -Name 'version 输出兼容子集标记' -Body {
        $r = Invoke-Tool -Arguments @('version'); $r.ExitCode -eq 0 -and $r.StdOut -like '*兼容子集*'
    }
    Invoke-Case -Name '未知子命令报错' -Body {
        $r = Invoke-Tool -Arguments @('foo'); $r.ExitCode -eq 1 -and $r.StdErr -like '*不支持的子命令*'
    }
    Invoke-Case -Name 'x509 -help 退出 0 且列出 -inform' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-help'); $r.ExitCode -eq 0 -and $r.StdOut -like '*-inform*'
    }
}
finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
'{0} passed, {1} failed' -f $script:passed, $script:failed
if ($script:failed -gt 0) { exit 1 }
exit 0