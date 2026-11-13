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
        $r = Invoke-Tool -Arguments @('version'); $r.ExitCode -eq 0 -and $r.StdOut -like '*兼容子集*' -and $r.StdOut -notlike '*yswenli*'
    }
    Invoke-Case -Name '未知子命令报错' -Body {
        $r = Invoke-Tool -Arguments @('foo'); $r.ExitCode -eq 1 -and $r.StdErr -like '*不支持的子命令*'
    }
    Invoke-Case -Name 'x509 -help 退出 0 且列出 -inform' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-help'); $r.ExitCode -eq 0 -and $r.StdOut -like '*-inform*'
    }
    $src = Join-Path $tmp 'src'; $pem = Join-Path $src 'localhost.crt'; $der = Join-Path $tmp 'cert.der'; $back = Join-Path $tmp 'cert.pem'
    $null = Invoke-Tool -Arguments @('--self-signed', '--out', $src)
    Invoke-Case -Name 'x509 PEM→DER→PEM 往返' -Body {
        $a = Invoke-Tool -Arguments @('x509', '-in', $pem, '-outform', 'DER', '-out', $der)
        $b = Invoke-Tool -Arguments @('x509', '-in', $der, '-inform', 'DER', '-out', $back)
        $a.ExitCode -eq 0 -and $b.ExitCode -eq 0 -and
            ([System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([System.IO.File]::ReadAllText($pem)).Thumbprint -eq
             [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([System.IO.File]::ReadAllText($back)).Thumbprint)
    }
    Invoke-Case -Name 'x509 -subject 输出 oneline 姓名' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-subject', '-noout'); $r.ExitCode -eq 0 -and $r.StdOut -like '*CN =*'
    }
    Invoke-Case -Name 'x509 -dates 输出 notBefore' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-dates', '-noout'); $r.ExitCode -eq 0 -and $r.StdOut -like '*notBefore=*'
    }
    Invoke-Case -Name 'x509 -serial 输出十进制转为大写十六进制' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-serial', '-noout'); $r.ExitCode -eq 0 -and $r.StdOut -like '*serial=*'
    }
    Invoke-Case -Name 'x509 -fingerprint -sha256 格式' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-fingerprint', '-sha256', '-noout'); $r.ExitCode -eq 0 -and $r.StdOut -match '(?m)^SHA256 Fingerprint=([0-9A-F]{2}:){31}[0-9A-F]{2}$'
    }
    Invoke-Case -Name 'x509 -text 报暂不支持' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-in', $pem, '-text'); $r.ExitCode -eq 1 -and $r.StdErr -like '*暂不支持*'
    }
    $key = Join-Path $src 'localhost.key'; $p8 = Join-Path $tmp 'key.p8'; $enc = Join-Path $tmp 'enc.p8'
    Invoke-Case -Name 'pkcs8 -topk8 -nocrypt 输出 PRIVATE KEY' -Body {
        $r = Invoke-Tool -Arguments @('pkcs8', '-topk8', '-nocrypt', '-in', $key, '-out', $p8)
        $r.ExitCode -eq 0 -and (Get-Content $p8 -Raw) -like '*BEGIN PRIVATE KEY*'
    }
    Invoke-Case -Name 'pkcs8 错误口令退出 1 且报口令错误' -Body {
        $a = Invoke-Tool -Arguments @('pkcs8', '-topk8', '-in', $key, '-passout', 'pass:x', '-out', $enc)
        $b = Invoke-Tool -Arguments @('pkcs8', '-in', $enc, '-passin', 'pass:y', '-out', (Join-Path $tmp 'bad.p8'))
        $a.ExitCode -eq 0 -and $b.ExitCode -eq 1 -and $b.StdErr -like '*口令错误*'
    }
    $p8test = Join-Path $tmp 'key-test.p8'; $plain = Join-Path $tmp 'plain.pem'
    Invoke-Case -Name 'pkey -traditional 输出 RSA PRIVATE KEY' -Body { $o = Join-Path $tmp 'rsa.pem'; $r = Invoke-Tool -Arguments @('pkey', '-in', $key, '-traditional', '-out', $o); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN RSA PRIVATE KEY*' }
    Invoke-Case -Name 'pkcs8 -topk8 -passout 输出 ENCRYPTED PRIVATE KEY' -Body { $r = Invoke-Tool -Arguments @('pkcs8', '-topk8', '-in', $key, '-passout', 'pass:test', '-out', $p8test); $r.ExitCode -eq 0 -and (Get-Content $p8test -Raw) -like '*BEGIN ENCRYPTED PRIVATE KEY*' }
    Invoke-Case -Name 'pkey -passin 解密回明文 PRIVATE KEY' -Body { $r = Invoke-Tool -Arguments @('pkey', '-in', $p8test, '-passin', 'pass:test', '-out', $plain); $text = Get-Content $plain -Raw; $r.ExitCode -eq 0 -and $text -like '*BEGIN PRIVATE KEY*' -and $text -notlike '*ENCRYPTED*' }
    Invoke-Case -Name 'pkey -passin env:SAEA_TEST_PWD 成功' -Body { $env:SAEA_TEST_PWD = 'test'; $o = Join-Path $tmp 'env.pem'; $r = Invoke-Tool -Arguments @('pkey', '-in', $p8test, '-passin', 'env:SAEA_TEST_PWD', '-out', $o); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN PRIVATE KEY*' }
    Remove-Item Env:\SAEA_TEST_PWD -ErrorAction SilentlyContinue
    Invoke-Case -Name 'pkcs8 -v1 与 -v2 同给退出 1' -Body { $r = Invoke-Tool -Arguments @('pkcs8', '-topk8', '-v1', 'PBE-SHA1-3DES', '-v2', 'aes-256-cbc', '-in', $key, '-passout', 'pass:test', '-out', $p8test); $r.ExitCode -eq 1 -and $r.StdErr -like '*不能同时使用*' }
}
finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
'{0} passed, {1} failed' -f $script:passed, $script:failed
if ($script:failed -gt 0) { exit 1 }
exit 0