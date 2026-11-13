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
    Invoke-Case -Name 'req -new -newkey 输出 CERTIFICATE REQUEST' -Body { $k = Join-Path $tmp 'k.pem'; $o = Join-Path $tmp 'r.csr'; $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', $k, '-subj', '/CN=localhost', '-out', $o); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN CERTIFICATE REQUEST*' }
    Invoke-Case -Name 'req 缺 -subj 退出 1' -Body { $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'n.pem'), '-out', (Join-Path $tmp 'n.csr')); $r.ExitCode -eq 1 -and $r.StdErr -like '*需要 -subj*' }
    Invoke-Case -Name 'req -addext subjectAltName 生成 CSR 且含 SAN 扩展' -Body { $o = Join-Path $tmp 'san.csr'; $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'san.pem'), '-subj', '/CN=localhost', '-addext', 'subjectAltName=DNS:localhost,IP:127.0.0.1', '-out', $o); $der = [Convert]::FromBase64String((((Get-Content $o -Raw) -replace '-----[^-]+-----', '') -replace '\s', '')); $r.ExitCode -eq 0 -and (Get-Content $o -Raw) -like '*BEGIN CERTIFICATE REQUEST*' -and [BitConverter]::ToString($der).Replace('-', '').Contains('0603551D11') }
    Invoke-Case -Name 'req -new 与 -x509 同给退出 1' -Body { $r = Invoke-Tool -Arguments @('req', '-new', '-x509', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'both.pem'), '-subj', '/CN=localhost'); $r.ExitCode -eq 1 -and $r.StdErr -like '*不能同时使用*' }
    Invoke-Case -Name 'req -addext 未知扩展退出 1' -Body { $r = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'bad.pem'), '-subj', '/CN=localhost', '-addext', 'authorityInfoAccess=OCSP;URI:http://ocsp.example', '-out', (Join-Path $tmp 'bad.csr')); $r.ExitCode -eq 1 -and $r.StdErr -like '*不支持的扩展*' }
    $ca = Join-Path $tmp 'ca.crt'; $caKey = Join-Path $tmp 'ca.key'
    $csr = Join-Path $tmp 'leaf.csr'; $leaf = Join-Path $tmp 'leaf.crt'; $srl = Join-Path $tmp 'ca.srl'
    $null = Invoke-Tool -Arguments @('req', '-x509', '-newkey', 'rsa:2048', '-keyout', $caKey, '-subj', '/CN=SAEA Test CA', '-days', '3650', '-out', $ca)
    $null = Invoke-Tool -Arguments @('req', '-new', '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'leaf.key'), '-subj', '/CN=localhost', '-addext', 'subjectAltName=DNS:localhost,IP:127.0.0.1', '-out', $csr)
    Invoke-Case -Name 'x509 -req 用 CA 签发 CSR 并写序列号文件' -Body {
        $r = Invoke-Tool -Arguments @('x509', '-req', '-in', $csr, '-CA', $ca, '-CAkey', $caKey, '-CAcreateserial', '-days', '30', '-copy_extensions', 'copy', '-out', $leaf)
        $caCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem((Get-Content -LiteralPath $ca -Raw))
        $leafCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem((Get-Content -LiteralPath $leaf -Raw))
        $leafSan = ($leafCert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' }).RawData
        $csrDer = [Convert]::FromBase64String((((Get-Content -LiteralPath $csr -Raw) -replace '-----[^-]+-----', '') -replace '\s', ''))
        $r.ExitCode -eq 0 -and (Test-Path -LiteralPath $srl) -and
            ((Get-Content -LiteralPath $srl -Raw).Trim() -cmatch '^[0-9A-F]+$') -and
            ($leafCert.Issuer -eq $caCert.Subject) -and ($leafSan.Length -gt 0) -and
            ([Convert]::ToHexString($csrDer).Contains([Convert]::ToHexString($leafSan)))
    }
    $pfx = Join-Path $tmp 'leaf.pfx'
    Invoke-Case -Name 'pkcs12 -export 导出 PFX' -Body {
        $r = Invoke-Tool -Arguments @('pkcs12', '-export', '-in', $leaf, '-inkey', (Join-Path $tmp 'leaf.key'), '-out', $pfx, '-passout', 'pass:test')
        $r.ExitCode -eq 0 -and $r.StdErr -notmatch 'error' -and (Test-Path -LiteralPath $pfx) -and (Get-Item -LiteralPath $pfx).Length -gt 0
    }
    Invoke-Case -Name 'pkcs12 -in 读回私钥' -Body {
        $pfxKey = Join-Path $tmp 'pfx.key'
        $r = Invoke-Tool -Arguments @('pkcs12', '-in', $pfx, '-passin', 'pass:test', '-nocerts', '-nodes', '-out', $pfxKey)
        $r.ExitCode -eq 0 -and (Get-Content -LiteralPath $pfxKey -Raw) -match '-----BEGIN PRIVATE KEY-----'
    }
    Invoke-Case -Name 'pkcs12 -legacy 导出成功' -Body { $o = Join-Path $tmp 'legacy.pfx'; $r = Invoke-Tool -Arguments @('pkcs12', '-export', '-in', $leaf, '-inkey', (Join-Path $tmp 'leaf.key'), '-legacy', '-out', $o, '-passout', 'pass:test'); $r.ExitCode -eq 0 -and (Test-Path -LiteralPath $o) -and (Get-Item -LiteralPath $o).Length -gt 0 }
    Invoke-Case -Name 'pkcs12 -clcerts -nokeys 只输出证书' -Body { $r = Invoke-Tool -Arguments @('pkcs12', '-in', $pfx, '-passin', 'pass:test', '-clcerts', '-nokeys'); $r.ExitCode -eq 0 -and $r.StdOut -like '*BEGIN CERTIFICATE*' -and $r.StdOut -notlike '*PRIVATE KEY*' }
    Invoke-Case -Name 'pkcs12 -name 导出并读回' -Body { $named = Join-Path $tmp 'alias.pfx'; $e = Invoke-Tool -Arguments @('pkcs12', '-export', '-in', $leaf, '-inkey', (Join-Path $tmp 'leaf.key'), '-name', 'myalias', '-out', $named, '-passout', 'pass:test'); $r = Invoke-Tool -Arguments @('pkcs12', '-in', $named, '-passin', 'pass:test', '-out', (Join-Path $tmp 'alias.pem')); $e.ExitCode -eq 0 -and $r.ExitCode -eq 0 -and $r.StdErr -notmatch 'error' }
    Invoke-Case -Name 'pkcs12 错误口令退出 1 且报口令错误' -Body { $r = Invoke-Tool -Arguments @('pkcs12', '-in', $pfx, '-passin', 'pass:bad', '-out', (Join-Path $tmp 'bad.pem')); $r.ExitCode -eq 1 -and $r.StdErr -like '*口令错误*' }
    Invoke-Case -Name 'pkcs12 -export 缺 -inkey 退出 1' -Body { $r = Invoke-Tool -Arguments @('pkcs12', '-export', '-in', $leaf, '-out', (Join-Path $tmp 'nokey.pfx'), '-passout', 'pass:test'); $r.ExitCode -eq 1 -and $r.StdErr -like '*需要 -in 与 -inkey*' }
    $p7 = Join-Path $tmp 'b.p7b'
    Invoke-Case -Name 'crl2pkcs7 -nocrl 生成 PKCS7 PEM' -Body {
        $r = Invoke-Tool -Arguments @('crl2pkcs7', '-nocrl', '-certfile', $leaf, '-out', $p7)
        $r.ExitCode -eq 0 -and (Test-Path -LiteralPath $p7) -and ((Get-Content -LiteralPath $p7 -Raw) -like '*BEGIN PKCS7*')
    }
    Invoke-Case -Name 'pkcs7 -print_certs 输出包内证书' -Body {
        $null = Invoke-Tool -Arguments @('crl2pkcs7', '-nocrl', '-certfile', $leaf, '-out', $p7)
        $r = Invoke-Tool -Arguments @('pkcs7', '-in', $p7, '-print_certs')
        $r.ExitCode -eq 0 -and $r.StdOut -like '*BEGIN CERTIFICATE*'
    }
    Invoke-Case -Name 'crl2pkcs7 缺 -nocrl 退出 1 且报不支持 CRL' -Body {
        $r = Invoke-Tool -Arguments @('crl2pkcs7', '-certfile', $leaf)
        $r.ExitCode -eq 1 -and $r.StdErr -like '*不支持读取 CRL*'
    }
    Invoke-Case -Name 'pkcs7 -outform DER 有 -out 时输出非空文件' -Body {
        $d = Join-Path $tmp 'b.p7c'; $null = Invoke-Tool -Arguments @('crl2pkcs7', '-nocrl', '-certfile', $leaf, '-out', $p7)
        $r = Invoke-Tool -Arguments @('pkcs7', '-in', $p7, '-outform', 'DER', '-out', $d)
        $r.ExitCode -eq 0 -and (Test-Path -LiteralPath $d) -and (Get-Item -LiteralPath $d).Length -gt 0
    }
    Invoke-Case -Name '.NET 读回 PFX 含私钥' -Body {
        $c = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile($pfx, 'test')
        $c.HasPrivateKey -and $c.Subject -like '*CN=localhost*'
    }
    Invoke-Case -Name '.NET 读回 P7B 证书数不少于 1' -Body {
        # 自己重建输入，避免依赖前面用例是否跑过。
        $null = Invoke-Tool -Arguments @('crl2pkcs7', '-nocrl', '-certfile', $leaf, '-out', $p7)
        # $p7 是 PEM 封装，SignedCms.Decode 只吃 DER，先去掉 armor 再喂。
        $der = [Convert]::FromBase64String((((Get-Content -LiteralPath $p7 -Raw) -replace '-----[^-]+-----', '') -replace '\s', ''))
        $cms = [System.Security.Cryptography.Pkcs.SignedCms]::new()
        $cms.Decode($der)
        $cms.Certificates.Count -ge 1 -and $cms.Certificates[0].Subject -like '*CN=localhost*'
    }
    Invoke-Case -Name '.NET 读回 DER 证书主题一致' -Body {
        $pem = Join-Path $tmp 'c.pem'; $der = Join-Path $tmp 'c.der'
        $a = Invoke-Tool -Arguments @('x509', '-in', $leaf, '-out', $pem)
        $b = Invoke-Tool -Arguments @('x509', '-in', $pem, '-outform', 'DER', '-out', $der)
        $c = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificateFromFile($der)
        $a.ExitCode -eq 0 -and $b.ExitCode -eq 0 -and $c.Subject -like '*CN=localhost*'
    }
    Invoke-Case -Name 'pkcs12 -passin file: 读取无结尾换行的口令' -Body {
        $pass = Join-Path $tmp 'pass.txt'; $o = Join-Path $tmp 'file.pem'
        [System.IO.File]::WriteAllText($pass, 'test')
        $r = Invoke-Tool -Arguments @('pkcs12', '-in', $pfx, '-passin', ('file:' + $pass), '-nocerts', '-nodes', '-out', $o)
        $r.ExitCode -eq 0 -and (Get-Content -LiteralPath $o -Raw) -like '*BEGIN PRIVATE KEY*'
    }
    Invoke-Case -Name 'pkcs12 -passin stdin 从管道读口令' -Body {
        $o = Join-Path $tmp 'stdin.pem'
        $argv = @('pkcs12', '-in', $pfx, '-passin', 'stdin', '-nocerts', '-nodes', '-out', $o)
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            if ($ToolPath) { 'test' | & $ToolPath @argv *> $null }
            else { 'test' | & dotnet run --project $project -c Release -- @argv *> $null }
            $code = $LASTEXITCODE
        }
        finally { $ErrorActionPreference = $previous }
        $code -eq 0 -and (Get-Content -LiteralPath $o -Raw) -like '*BEGIN PRIVATE KEY*'
    }
    Invoke-Case -Name 'req -key 与 -newkey 同给退出 1' -Body {
        $r = Invoke-Tool -Arguments @('req', '-new', '-key', $key, '-newkey', 'rsa:2048', '-keyout', (Join-Path $tmp 'mx.key'), '-subj', '/CN=localhost')
        $r.ExitCode -eq 1 -and $r.StdErr -like '*不能同时使用*'
    }
    Invoke-Case -Name 'pkey -traditional 与加密算法同给退出 1' -Body {
        $r = Invoke-Tool -Arguments @('pkey', '-in', $key, '-traditional', '-aes-256-cbc')
        $r.ExitCode -eq 1 -and $r.StdErr -like '*不能*同时使用*'
    }
    Invoke-Case -Name '遗留 --self-signed/--verify 仍退出 0' -Body {
        $dir = Join-Path $tmp 'legacy'; $ver = Join-Path $tmp 'legacy-verify'
        $a = Invoke-Tool -Arguments @('--self-signed', '--out', $dir)
        $b = Invoke-Tool -Arguments @('--self-signed', '--verify', '--out', $ver)
        $a.ExitCode -eq 0 -and
            (Test-Path -LiteralPath (Join-Path $dir 'localhost.crt')) -and
            (Test-Path -LiteralPath (Join-Path $dir 'localhost.key')) -and
            $b.ExitCode -eq 0 -and $b.StdOut -like '*通过*'
    }
}
finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
'{0} passed, {1} failed' -f $script:passed, $script:failed
if ($script:failed -gt 0) { exit 1 }
exit 0