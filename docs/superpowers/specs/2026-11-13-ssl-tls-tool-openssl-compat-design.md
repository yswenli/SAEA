# SslTlsTool 的 openssl 兼容子命令（生成与格式转换）设计

## 1. 为什么做

现有工具只有 `--self-signed` 与 `--ca` 两种模式，产物固定为 PEM 证书 + 明文私钥 + PFX。实际排查问题时常需要反向操作：把别人给的 PKCS#8 私钥转成 PKCS#1、把 DER 证书转 PEM、把证书打成 P7B 交给 Windows 某个只认 PKCS#7 的组件、把 PFX 拆成可读 PEM。Windows 上常常没有 openssl，临时装一套成本又高。

| 现状 | 缺口 |
| --- | --- |
| 只产出 PEM + PFX | 无 DER、PKCS#8、PKCS#1、P7B/P7C 写法 |
| 只生成，不转换 | 无法读取外部产物做格式互转 |
| 主题/扩展由用途硬编码 | 无法按用户指定扩展签 CSR |

因此做一批与 openssl 同名的子命令，让既有脚本和肌肉记忆能直接用；不追求完整替代 openssl。

## 2. 目标

- 提供子命令 `req`、`x509`、`pkcs12`、`pkcs8`、`pkey`、`crl2pkcs7`、`pkcs7`、`version`。
- 覆盖 PEM、PFX/P12、P7B/P7C、DER、PKCS#8、PKCS#1 之间的互转，以及 CSR 的生成与签发。
- 不支持的参数与命令显式报错，不静默忽略。
- 现有 `--self-signed`、`--ca`、`--verify` 与交互模式的行为、输出、退出码保持不变。

## 3. 非目标

`s_client`、`s_server`、`dgst`、`enc`、`genrsa`、`ca`、`verify`、CRL 生成与吊销、OCSP、ENGINE、`-config` 完整解析、`-text` 完整 ASN.1 dump、JKS、ECDSA、Ed25519 均不做；不改 `SAEA.Sockets` 公开 API；不改任何 `.csproj` 的 `Version` 或 `PackageReference`；不安装到系统信任库。

## 4. 反目标

做到以下任一条即视为失败：

- 未知参数被静默忽略，用户以为生效了。
- 遗留命令的输出、文件布局或退出码发生变化。
- 产出的 PEM/PFX/P7B 不能被 .NET 自身读回。
- 口令出现在 stdout、异常消息或日志里。
- 把 IP 写进 `dNSName`、或在未给 `-addext` 时凭空塞 SAN 这类陷阱重演。

## 5. 现状约束

| 项 | 现状 | 对兼容层的约束 |
| --- | --- | --- |
| 工具目标框架 | `net10.0`，`Nullable` + `ImplicitUsings` 开启 | 兼容层可用新语法；参数与返回值按可空处理 |
| 工具的包依赖 | 只 `ProjectReference` `SAEA.Sockets`；BouncyCastle 2.6.2 传递可用 | 解析/写出各格式无需新增 `PackageReference` |
| 库目标框架 | `netstandard2.0`，`LangVersion` 8.0 | 引擎若上提到库，受 C# 8 与公开面约束 |
| 证书相关测试 | `Ssl/` 目录无专项单测 | 兼容层行为靠工具自测与外部 openssl 产物对照取证 |
| 单测工程 | 12 个 MSTest 工程，`net10.0` / `LangVersion` 10.0 / MSTest 3.1.1，均不在 sln | 兼容层不新增测试工程，取证改由独立 PowerShell 脚本承担，脚本不进 sln 也不进 CI |
| 工具与 sln | 工具工程在 sln 内 | 兼容层改动进入全量构建，构建失败即阻断 |
| 遗留退出码 | 0 / 1 / 2 / 3（3 = 全部跳过未写入） | 兼容分支不得复用 2 / 3 的语义 |
| `SaeaCertificateLoader.FromPem` | 用 `X509CertificateParser().ReadCertificate` 只取首个 `CERTIFICATE`，并按「证书路径\|私钥路径\|口令」做进程内缓存 | 不能靠库 API 读 PEM 链；链解析必须自己按 PEM 块切分 |
| 私钥写出 | 库只写不加密 PKCS#8（`PrivateKeyInfo`）与 PFX | 加密 PKCS#8、PKCS#1 的读写需兼容层自实现 |
| PFX 读取 | `FromPfx` 只返回叶子 `X509Certificate2` | 库不回传 PFX 内链，`pkcs12` 读取需直接解析 `Pkcs12Store` |
| 生成能力 | 固定 `SHA256WITHRSA`，扩展由用途决定，无 CSR、无自定义扩展入参 | `x509 -req` 签外部 CSR 必须自建签名与扩展装配路径 |
| `.gitignore` | 已忽略 `*.pfx`、`*.key`、`certs/` | 自测产物默认落 `certs/`，不会误入版本库 |

## 6. 方案比选与结论

| 维度 | 方案 1：兼容层全放工具工程 | 方案 2：引擎上提到 `SAEA.Sockets.Ssl` 公开 API | 方案 3：新建 `SAEA.Sockets.Certificates` 包 |
| --- | --- | --- | --- |
| 影响面 | 仅工具工程，库与包不动 | 扩大 `netstandard2.0` 公开面，需 XML 注释、C# 8 写法 | 新增包与依赖关系，sln、README、发布流程都要跟 |
| 复用性 | 差：约 200 行密钥/证书构建与库内 `SaeaCertificateWriter` 重复 | 好：库用户与工具共用 | 好：边界最干净 |
| 成本 | 最低 | 中：公开面一旦定型难回收 | 最高：新包要定版本、写双语文档 |
| 风险 | 两处实现可能漂移，需在文档与自测中标注 | 改动库即改包内容，版本需用户处理 | 包边界与依赖链引入长期维护负担 |

结论：选方案 1。理由是本次需求只服务命令行兼容，重复的构建代码量可控，且不动库与包意味着没有版本与公开面风险。以后若要库用户复用这套引擎，再单独提上提方案，属新请求。

## 7. CLI 契约

| 规则 | 约定 |
| --- | --- |
| 分派 | `args[0]` 与子命令名精确匹配（小写、区分大小写）时进入兼容分支；`args[0]` 以 `-` 开头或无参数时走遗留分支；其它取值的首 token 报错并列出支持的子命令 |
| 交互模式 | 同一套分派：逐行输入的首 token 同样可命中子命令 |
| 退出码 | 兼容分支：0 成功，1 任何失败（含用法错误）；遗留分支保持 0 / 1 / 2 / 3 |
| 输出流 | 错误写 stderr，显式请求的 `-help` 写 stdout；数据写 `-out` 指定的文件，无 `-out` 时写 stdout |
| 二进制输出 | `-outform DER` 且无 `-out` 时，stdout 直接写原始二进制，不得混入任何文本 |
| 日志隔离 | 进度、提示、警告一律不得进入 stdout，避免污染管道输出；兼容分支不触发 `SAEA.Common` 的启动横幅（`ConsoleHelper` 静态构造会打版权横幅到 stdout），遗留分支保持原行为 |
| 子命令帮助 | 每个子命令的 `-help` 打印本工具实际支持的参数并退出 0；不声称与 openssl 逐字一致 |
| 版本 | `version` 输出 `<工具名> <版本>（openssl 兼容子集）`，并声明与 `openssl version` 不兼容 |
| 未知参数 | 一律 `error: 不支持的参数 "-xxx"。` 并附该子命令的支持清单提示 |
| `-help` | 写 stdout 并退出 0；即使同时给了其它参数，也不执行任何文件读写 |
| `-info` | 摘要写 stderr；stdout 只保留 `-out` 数据或无 `-out` 时的 PEM/DER 数据 |
| `-out` 已存在 | 直接覆盖（openssl 语义）；兼容分支永不返回 3，「跳过已有文件」只属于遗留生成分支 |
| 交互模式限制 | 禁用 `-passin stdin`，报 `交互模式下不支持 -passin stdin；请用 pass:、env: 或 file:。`；`-outform DER` 且无 `-out` 报 `交互模式下二进制输出必须给 -out。` |

## 8. 子命令与参数矩阵

### 8.1 `req`

| 参数 | 取值 | 语义 | 默认 |
| --- | --- | --- | --- |
| `-new` | 开关 | 生成 CSR | — |
| `-x509` | 开关 | 直接自签成证书而非 CSR | 关 |
| `-newkey` | `rsa:<bits>`，1024/2048/3072/4096 | 新建密钥，必须配 `-keyout` | 键长 2048 |
| `-key` | 路径 | 使用既有私钥 | — |
| `-keyout` | 路径 | 私钥输出路径 | — |
| `-nodes` / `-noenc` | 开关 | 私钥不加密 | 关 |
| `-days` | 正整数 | `-x509` 时的有效天数 | 30 |
| `-subj` | openssl 斜杠格式 | 主题 DN，形如 `/CN=a/O=b`，分隔符 `/` 与 `,` 均可，支持 `\/`、`\,` 与 `\\` 转义，不支持 `+` 多值 | — |
| `-addext` | `name=value`，可重复 | 追加扩展 | 无 |
| `-sha256` | 开关 | 签名摘要 SHA-256 | 开（唯一选项） |
| `-out` | 路径 | CSR 或证书输出 | stdout |
| `-passout` | 口令源（见 §10） | 私钥/PFX 口令 | 无 |

必填与互斥：`-new` 与 `-newkey`、`-key` 至少给一个；`-newkey` 必须配 `-keyout`；`-newkey` 与 `-key` 互斥。未给 `-subj` 时直接报错 `req: 需要 -subj；交互式 DN 输入未支持。`；不做交互式 DN 问答，stdin 留给口令与交互模式。

扩展键白名单：`subjectAltName`、`basicConstraints`、`keyUsage`、`extendedKeyUsage`。SAN 支持 `DNS:`、`IP:`、`email:`、`URI:`，`IP:` 用 `IPAddress.TryParse` 校验。`critical` 前缀原样透传。

`-new` 产出 `CERTIFICATE REQUEST`；`-x509` 自签时若未给 `-addext` 则不写 SAN（与 openssl 一致）。文档需提醒：浏览器要求 SAN，只给 CN 会报证书无效。

报错示例：

```
req: 不支持的扩展 "authorityInfoAccess"；支持：subjectAltName、basicConstraints、keyUsage、extendedKeyUsage。
```

### 8.2 `x509`

| 参数 | 取值 | 语义 | 默认 |
| --- | --- | --- | --- |
| `-in` | 路径 | 输入证书或 CSR | stdin |
| `-inform` | `PEM` / `DER` | 输入格式 | PEM |
| `-out` | 路径 | 输出证书 | stdout |
| `-outform` | `PEM` / `DER` | 输出格式 | PEM |
| `-noout` | 开关 | 不输出证书本体，仅打印所选信息 | 关 |
| `-subject` / `-issuer` | 开关 | 打印主题 / 签发者 | 关 |
| `-dates` | 开关 | 打印生效与到期时间 | 关 |
| `-serial` | 开关 | 打印序列号 | 关 |
| `-fingerprint` | 开关 | 打印指纹 | SHA-1 |
| `-sha256` | 开关 | 指纹改用 SHA-256 | 关 |
| `-pubkey` | 开关 | 打印公钥 PEM | 关 |
| `-req` | 开关 | 以 CSR 为输入并由 CA 签发 | 关 |
| `-CA` / `-CAkey` | 路径 | 签发者证书 / 私钥 | — |
| `-CAcreateserial` | 开关 | 写 `<CA 文件名>.srl` 并取其中序列号 | 关 |
| `-days` | 正整数 | 新证书有效天数 | 30 |
| `-extfile` / `-extensions` | 路径 / 段名 | 从扩展文件取指定段 | 无 |
| `-copy_extensions` | `copy` / `none` | 从 CSR 复制扩展 | `none` |

`-copy_extensions copy` 只复制 `subjectAltName`、`basicConstraints`、`keyUsage`、`extendedKeyUsage` 四类。

序列号文件规则：

- 给了 `-CAcreateserial`：写或更新 `<CA 文件名>.srl`，内容为大写十六进制序列号加一个换行，新号在旧值上加 1。
- 未给 `-CAcreateserial`：`<CA 文件名>.srl` 存在就读取，不存在则用 64 位正随机数，不落盘。
- 不支持 `-CAserial`，遇到报 `x509: 不支持的参数 "-CAserial"；序列号文件固定为 <CA 文件名>.srl。`

不支持项：`-text` 报 `x509: -text 暂不支持；请用 -subject/-issuer/-dates/-serial/-fingerprint，或用外部工具查看完整 ASN.1。`；`-nameopt` 不支持，姓名统一 oneline 风格（`CN = localhost, O = SAEA`）。

### 8.3 `pkcs12`

| 参数 | 取值 | 语义 | 默认 |
| --- | --- | --- | --- |
| `-export` | 开关 | 导出 PFX | 关（读取模式） |
| `-in` | 路径 | 导出时是叶子证书，读取时是 PFX | stdin |
| `-inkey` | 路径 | 导出用私钥 | — |
| `-certfile` | 路径，可重复 | 追加到 PFX 的证书 | 无 |
| `-name` | 字符串 | friendlyName，给了才写 | — |
| `-out` | 路径 | 输出 PFX 或导出的 PEM | stdout |
| `-passout` / `-passin` | 口令源 | 写出 / 读取口令 | 无 |
| `-legacy` | 开关 | RC2-40 / 3DES 算法族 | 关 |
| `-nodes` | 开关 | 不加密 | 关 |
| `-nokeys` | 开关 | 读取时不输出私钥 | 关 |
| `-nomac` | 开关 | 导出时不写 MAC，与 `-passout` 互斥 | 关 |
| `-nocerts` / `-clcerts` / `-cacerts` | 开关 | 读取时的筛选：只要私钥 / 只要叶子 / 只要链 | 全给 |
| `-info` | 开关 | 打印摘要 | 关 |

默认算法为 AES-256-CBC + PBKDF2，`-legacy` 改用旧算法族以兼容旧系统：证书加密 `PBE-SHA1-RC2-40`、私钥加密 `PBE-SHA1-3DES`、MAC 摘要 SHA-1，迭代仍为 2048。未给 `-name` 时不写 friendlyName，给了才写（openssl 3 仅在 `-name` 或证书自带 alias 时设置）。`-nodes`/`-nomac` 与 `-passout` 同给报错。`-nokeys` 只作用于读取模式，`-nomac` 只作用于导出模式。`-info` 的文本摘要保留信息量但不逐字等同于 openssl。

读取模式的输出顺序：私钥（PKCS#8 PEM）在前，随后叶子证书，再按 PFX 内顺序写链；`-nocerts` 只写私钥，`-clcerts` 只写叶子，`-cacerts` 只写链，`-clcerts` 与 `-cacerts` 同给报错。

### 8.4 `pkcs8`

| 参数 | 取值 | 语义 | 默认 |
| --- | --- | --- | --- |
| `-topk8` | 开关 | 输出 PKCS#8 | 关 |
| `-in` / `-out` | 路径 | 输入 / 输出 | stdin / stdout |
| `-inform` / `-outform` | `PEM` / `DER` | 输入 / 输出格式 | PEM |
| `-nocrypt` | 开关 | 不加密输出 | 关 |
| `-v1` | `PBE-SHA1-3DES` | 旧式 PBE 加密 | — |
| `-v2` | `aes-128-cbc` / `aes-192-cbc` / `aes-256-cbc` / `des3` | 现代 PBE 加密 | `aes-256-cbc` |
| `-passin` / `-passout` | 口令源 | 读取 / 写出口令 | 无 |

不带 `-topk8` 时输出传统 PKCS#1 PEM，此时给 `-passout` 报错。加密头为 `ENCRYPTED PRIVATE KEY`，明文头为 `PRIVATE KEY`。

`-topk8` 未给 `-nocrypt` 且未给 `-v1`/`-v2` 时，按 `-v2 aes-256-cbc` 处理，需要口令（走 §10）。

### 8.5 `pkey`

| 参数 | 取值 | 语义 | 默认 |
| --- | --- | --- | --- |
| `-in` / `-out` | 路径 | 输入 / 输出 | stdin / stdout |
| `-inform` / `-outform` | `PEM` / `DER` | 输入 / 输出格式 | PEM |
| `-pubout` | 开关 | 输出公钥 | 关 |
| `-traditional` | 开关 | 输出 PKCS#1 | 关 |
| `-aes-128-cbc` / `-aes-192-cbc` / `-aes-256-cbc` / `des3` | 开关 | 加密输出的 PKCS#8 | 明文 |
| `-passin` / `-passout` | 口令源 | 读取 / 写出口令 | 无 |

默认输出明文 PKCS#8；`-traditional` 输出 PKCS#1；给 cipher 则输出加密 PKCS#8。`-traditional` 与 cipher 同给报错。读取支持旧式加密 PKCS#1 PEM（`Proc-Type: 4,ENCRYPTED` 头），该格式只读不写。

### 8.6 `crl2pkcs7`

| 参数 | 取值 | 语义 | 默认 |
| --- | --- | --- | --- |
| `-nocrl` | 开关 | 必需；声明不读 CRL | — |
| `-certfile` | 路径，可重复 | 装入 PKCS#7 的证书 | 无 |
| `-out` / `-outform` | 路径 / `PEM`\|`DER` | 输出 | stdout / PEM |

缺 `-nocrl` 报 `crl2pkcs7: 不支持读取 CRL，请加 -nocrl。`。输出 certs-only PKCS#7，PEM 头为 `PKCS7`。

### 8.7 `pkcs7`

| 参数 | 取值 | 语义 | 默认 |
| --- | --- | --- | --- |
| `-in` / `-inform` | 路径 / `PEM`\|`DER` | 输入 | stdin / PEM |
| `-print_certs` | 开关 | 输出包内全部证书 PEM | 关 |
| `-out` / `-outform` | 路径 / `PEM`\|`DER` | 输出 | stdout / PEM |
| `-noout` | 开关 | 抑制证书本体输出 | 关 |

`-print_certs` 输出多张 PEM 证书。本工具不输出 openssl 的 `subject=` / `issuer=` 摘要行，因此 `-noout` 是空操作，差异见 §11。

### 8.8 通用规则

未列出的参数一律 `error: 不支持的参数 "-xxx"。`，并打印该子命令的支持清单。所有参数名与 openssl 同名同序，取值解析区分大小写。

## 9. 格式与转换矩阵

表中单元格为完成该转换的命令片段；`pkcs12` 指该子命令的导出或读取模式。

| 输入 \ 输出 | PEM 证书/链 | DER 证书 | PFX | P7B/P7C | PKCS#8 | PKCS#1 | CSR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| PEM 证书 | 原样 / `pkcs12 -out` 拼接 | `x509 -outform DER` | `pkcs12 -export -in … -inkey …` | `crl2pkcs7 -nocrl -certfile …` | — | — | — |
| PEM 链 | 拼接 / `pkcs7 -print_certs` | —（多张无单证书 DER 语义） | `pkcs12 -export -certfile chain.pem` | `crl2pkcs7 -nocrl -certfile chain.pem` | — | — | — |
| DER 证书 | `x509 -inform DER -out` | `x509 -inform DER -outform DER` | 需先转 PEM 再 `-export` | 需先转 PEM | — | — | — |
| PEM PKCS#8 | — | `pkey -outform DER` | `pkcs12 -export -inkey …` | — | `pkey`（可加 cipher） | `pkey -traditional` | `req -new -key` |
| 加密 PKCS#8 | — | `pkey -passin … -outform DER` | `pkcs12 -export -inkey … -passin …` | — | `pkey -passin …`（保持或换 cipher） | `pkey -traditional -passin …` | —（`req` 无 `-passin`） |
| PEM PKCS#1 | — | `pkey -outform DER` | `pkcs12 -export -inkey …` | — | `pkey` | `pkey -traditional` | `req -new -key` |
| 加密 PKCS#1 | — | `pkey -passin … -outform DER` | `pkcs12 -export -inkey … -passin …` | — | `pkey -passin …` | `pkey -traditional -passin …`（转明文） | —（`req` 无 `-passin`） |
| PFX | `pkcs12 -in … -out all.pem` | —（读取模式无 `-outform`） | `pkcs12 -in … -out …`（重打包） | 需先导 PEM | `pkcs12 -in … -nodes -out` | —（再走 `pkey -traditional`） | — |
| P7B/P7C PEM | `pkcs7 -print_certs` | —（包内证书需逐张转） | —（无配套私钥） | `pkcs7 -outform DER` | — | — | — |
| P7B/P7C DER | `pkcs7 -inform DER -print_certs` | — | — | `pkcs7 -inform DER`（重写） | — | — | — |
| CSR | `x509 -req -CA … -CAkey …` | `x509 -req … -outform DER` | —（需另配 `-inkey`） | — | — | — | `req -new`（从零生成） |

补充约定：

- PKCS#12 可同时承载叶子证书、链、私钥、口令与 friendlyName，并可按 `-legacy` 选择旧算法族；它是唯一能一次装齐「证书 + 链 + 私钥 + 保护」的容器。
- `.p7b` / `.p7c` 只是扩展名，实际格式由 `-outform` 决定，与文件名无关。
- P7B/P7C 不承载私钥，因此任何「P7B → PFX」的路径都必须另给私钥，矩阵中标为不可直达。
- 加密私钥的读取依赖 `-passin`；`req` 的参数集不含 `-passin`，所以 `req` 只能用明文私钥或 `-newkey` 现生成。

## 10. 口令来源与提示

`-passin` 与 `-passout` 共用一套取值语法，只有方向不同。

| 取值形式 | 语义 | 边界 |
| --- | --- | --- |
| `pass:<明文>` | 取冒号后的全部内容 | 原样保留，含空格与特殊字符；空串等同空口令 |
| `env:<变量名>` | 读环境变量 | 变量不存在或为空按「未给口令」处理，不回退到交互提示 |
| `file:<路径>` | 读文件首行 | 去掉行尾 `CR` / `LF`，其余字节原样；文件不存在或为空文件报错 |
| `stdin` | 从标准输入读一行 | 读到 `LF` 或流结束为止，同样去掉行尾 `CR`；只读一行 |

裸字符串、`fd:<n>` 与其它前缀一律 `error: 不支持的口令来源 "<值>"；支持 pass:、env:、file:、stdin。`

口令缺失与交互：

| 场景 | 行为 |
| --- | --- |
| 操作需要口令、未给口令源、`Console.IsInputRedirected == false` | 交互提示，输入不回显 |
| 操作需要口令、未给口令源、标准输入已重定向 | 报 `需要口令：请用 -passin/-passout，或从交互终端运行。`，退出 1 |

显式给了 `-passout` 但来源解析为空（如 `env:` 变量缺失）时，需要口令的写出操作按缺口令报错，不静默生成无保护产物。

提示语固定英文，与 openssl 的肌肉记忆对齐：读取用 `Enter pass phrase for <path>: `，写入且需二次确认时用 `Verifying - Enter pass phrase for <path>: `。`<path>` 取命令行原始路径，从 `stdin` 读口令时写 `stdin`。二次输入不一致直接报 `两次输入的口令不一致。`，不做三次重试。

空口令与错误口令分开处理：

| 场景 | 处理 |
| --- | --- |
| PFX 加解密 | 空口令合法，等同无口令保护 |
| 加密 PKCS#8 | 空口令非法，报 `PKCS#8 加密口令不能为空。` |
| 带保护容器但口令错误 | 统一报 `口令错误或文件损坏。`，不区分二者以免成为口令探测手段；不拼路径，让 PFX 与 PKCS#8 两条链路文案一致 |

安全约束：口令只在内存中传递，不得回显、不得拼进异常消息、不得写日志、不得进入 `-info` 摘要。`pass:<明文>` 会出现在进程命令行与 shell history，这一点只在 README 提示，工具内不解决。

兼容说明：`-passin stdin` 与 openssl 一致；`env:` 的变量名在 openssl 中区分大小写，本工具同；`-passin fd:<n>` 不做，遇到即报错。

## 11. 输出保真度与已知差异

### 11.1 PEM 与 DER

| 项 | 规则 |
| --- | --- |
| 行宽 | base64 每行 64 列，硬换行，不做自适应 |
| 换行 | 一律 `LF`，不随平台变化；文件末尾恰好一个换行 |
| BOM | 不写 BOM；读取时容忍 BOM，但不保留 |
| 块头 | 严格按对象类型：`CERTIFICATE`、`CERTIFICATE REQUEST`、`PRIVATE KEY`、`ENCRYPTED PRIVATE KEY`、`RSA PRIVATE KEY`、`PKCS7`、`PUBLIC KEY` |
| 多对象 | 证书链与 `pkcs7 -print_certs` 按原顺序连续写多个块，块间不插说明文字 |
| DER | 原始二进制，无换行、无块头、不带任何文本 |

`RSA PRIVATE KEY` 只用于 PKCS#1；PKCS#8 明文一律 `PRIVATE KEY`，加密一律 `ENCRYPTED PRIVATE KEY`。库内旧式加密 PKCS#1（`Proc-Type: 4,ENCRYPTED`）只读不写。

### 11.2 文本输出

| 项 | 格式 |
| --- | --- |
| 姓名 | 固定 oneline：`CN = localhost, O = SAEA`，分隔符 `, `，`=` 两侧各一个空格 |
| 日期 | `notBefore=Nov 13 10:00:00 2026 GMT`，英文月份缩写、固定 GMT，不随时区偏移 |
| 指纹 | 大写十六进制、冒号分隔，如 `SHA1 Fingerprint=AB:CD:…` |
| 序列号 | 十六进制大写，不加 `0x`，不做补零对齐 |
| `-info` | 保留「算法 / 证书数 / friendlyName / 是否含私钥」的信息量，不逐字复刻 |

### 11.3 已知差异

| 项目 | openssl 行为 | 本工具 | 影响 |
| --- | --- | --- | --- |
| `Bag Attributes` | `pkcs12` 输出时带 friendlyName 等属性行 | 不输出 | 靠该行判断 friendlyName 的脚本失效 |
| `pkcs7 -print_certs` 摘要行 | 每张证书前打 `subject=` / `issuer=` | 只输出 PEM 块 | `-noout` 成为空操作；按行切分的脚本需改 |
| `pkcs7 -print_certs -outform DER` | 接受组合 | 报错（`-print_certs` 只输出 PEM 文本） | 组合无实际意义，脚本需拆开 |
| `crl2pkcs7 -nocrl` 无 `-certfile` | 生成不含证书的 certs-only 包 | 报错 | 只跑空包的脚本失效，该产物无实际用途 |
| `pkcs12` 读取时的私钥加密 | 用 `-passout` 重新加密，缺省交互索要 | 用 `-passin` 的口令重新加密；读取模式不接受 `-passout`，`-nodes` 输出明文 | 读写口令不同的脚本需加 `-nodes` 或拆成两步 |
| `pkcs12 -nodes` 的 MAC | 只关加密，仍用口令写 MAC | `-nodes`/`-nomac` 都不写 MAC，与 `-passout` 互斥 | 无口令 PFX 无法做完整性校验，只能直接读回 |
| `x509 -text` | 完整 ASN.1 dump | 不支持，报错并给替代参数 | 需要 dump 时只能用外部工具 |
| `-nameopt` | 可切 RFC2253 / multiline 等 | 不支持，固定 oneline | 需要 RFC2253 的脚本要自行转换 |
| `-config` | 读配置文件生成证书 | 不支持，仅 `-extfile` / `-addext` | 既有 `openssl.cnf` 流程需改写成命令行 |
| `req -newkey` 未给 `-nodes`/`-passout` | 交互式索要口令并加密私钥，非交互时读不到口令即失败 | 明文写出并在 stderr 提示 | 明文私钥不得进版本库；脚本需要加密必须显式给 `-passout` |
| `version` | `OpenSSL 3.x.y …` | `<工具名> <版本>（openssl 兼容子集）` | 版本探测脚本可能误判 |
| PFX 默认参数 | 证书与私钥均 AES-256-CBC，PBKDF2 迭代 2048，MAC 为 HMAC-SHA-256、迭代 2048，PBE 与 MAC 的 salt 各 8 字节 | 同上，常量写死；`-legacy` 走 RC2-40 / 3DES / SHA-1 | 加密参数与 openssl 3 一致，`openssl pkcs12 -info` 显示的算法、迭代数、盐长相同；文件字节仍不同，因为 salt 与 IV 随机 |
| `-info` 文本 | 逐字固定格式 | 信息量对齐、措辞不同 | 匹配 `MAC:` 等字段的正则可能失效 |

结论：只读 PEM / DER / PFX / P7B 的程序（.NET、Java、nginx、IIS）不受影响；靠正则解析文本输出的脚本可能受影响，README 必须写明并给出替代参数。

关于逐字节一致：salt 与 IV 随机，任何两次导出（openssl 自己两次导出也一样）都不可能字节相同；本期的口径是参数、结构、条目顺序、friendlyName 规则逐项一致。`Pkcs12Codec` 内部保留一个可注入 PBE salt 与 MAC salt 的构造函数（块加密 IV 由 salt 派生），供有 openssl 的环境做结构级对拍；CLI 不暴露固定随机源的参数。

## 12. 实现结构

落点 `Src/SAEA.Sockets.SslTlsTool/OpenSsl/`，命名空间 `SAEA.Sockets.SslTlsTool.OpenSsl`。`Program.Main` 只增加一处分派：按 §7 的规则识别首 token，命中子命令则交给 `OpenSslCommand.Run(string[] args)` 并原样返回其退出码；遗留生成与校验路径零改动，`SslTlsToolOptions`、`CertificateVerifier` 不动。

| 文件 | 职责 |
| --- | --- |
| `OpenSslCommand.cs` | 子命令分派、`-help` 短路、异常收敛为退出码（成功 0，`OpenSslException` 与未预期异常 1） |
| `OpenSslUsage.cs` | 每个子命令的支持参数清单与 `-help` 文本 |
| `OptionReader.cs` | 单横线参数读取：取值型、开关型、可重复聚合、值缺失检测、未知参数报错 |
| `PasswordResolver.cs` | §10 的口令来源解析与交互提示，`static string? Resolve(string? spec, string target, bool confirm, bool interactive)` |
| `PemCodec.cs` | PEM 块切分与写出：按块头收集、保序、统一 64 列与 LF |
| `KeyCodec.cs` | PKCS#1 / PKCS#8 的明文与加密读写、PEM 与 DER 互转、旧式加密 PKCS#1 只读 |
| `CertificateCodec.cs` | 证书与链的 PEM/DER 读写、证书到文本块（姓名/日期/序列号/指纹/公钥） |
| `Pkcs12Codec.cs` | `Pkcs12Store` / `Pkcs12StoreBuilder` 的装载与保存、friendlyName、`-legacy` 算法族、`-nocerts`/`-clcerts`/`-cacerts` 筛选 |
| `Pkcs7Codec.cs` | `Pkcs7SignedData` certs-only 的生成与拆解 |
| `CsrCodec.cs` | PKCS#10 的生成与读取、`-copy_extensions` 的四类扩展搬运 |
| `SubjectNameParser.cs` | `-subj` 斜杠格式解析，分隔符 `/` 与 `,`，支持 `\/`、`\,` 与 `\\`，拒绝 `+` 多值 |
| `ExtensionSpec.cs` | §8.1 白名单到 BouncyCastle 扩展对象的映射，含 `critical` 前缀与 SAN 的 `DNS:`/`IP:`/`email:`/`URI:` 校验 |
| `MiniConfigReader.cs` | `-extfile` / `-extensions` 的最小 INI 解析：只认 `[section]`、`key=value`、`;` 与 `#` 注释 |
| `OpenSslException.cs` | 统一错误文案载体，`Message` 即可直接写 stderr 的句子 |

上述类型同处一个命名空间，不再分子命名空间，避免与库内 `SAEA.Sockets.Ssl.Internal` 混淆。依赖的 BouncyCastle 类型族全部由 `SAEA.Sockets` 的 `BouncyCastle.Cryptography 2.6.2` 传递提供，不新增 `PackageReference`：

| 命名空间 | 用途 |
| --- | --- |
| `Org.BouncyCastle.X509` | 证书解析、`X509V3CertificateGenerator` 签发路径 |
| `Org.BouncyCastle.OpenSsl` | `PemReader` / `PemWriter`、`Pkcs8Generator`、旧式加密 PEM |
| `Org.BouncyCastle.Pkcs` | `Pkcs12Store`、`Pkcs12StoreBuilder`、`PrivateKeyInfoFactory` |
| `Org.BouncyCastle.Asn1` | `Pkcs`、`X509` 结构，P7B 拆装与扩展装配 |
| `Org.BouncyCastle.Crypto` | 密钥对生成、PBE 算法与摘要 |
| `Org.BouncyCastle.Security` | `SecureRandom`、签名器与算法标识 |
| `Org.BouncyCastle.Utilities.IO.Pem` | 需要控制块头时手工拼 `PemObject` |

文件头 banner：`OpenSsl/` 下每个新建 `.cs` 必须带仓库统一 banner。工具 csproj 无 `<Version>`，故 `CLR版本` 与 `版本号` 两栏都写 `net10.0`，`修改标记` 段版本号同；`命名空间` 写 `SAEA.Sockets.SslTlsTool.OpenSsl`；`唯一标识` 每文件新生成 GUID，禁止复用。

与库内 `SaeaCertificateWriter` 的重复边界：

| 逻辑 | 处理 | 理由 |
| --- | --- | --- |
| 密钥对生成、自签与签发证书的构建 | 刻意重复，在 `OpenSsl` 下自建 | 该类型为 `internal` 且签名绑定 `SaeaCertificateSpec` 与用途枚举，复用会把 CLI 语义塞进库用途模型 |
| PFX 与 PEM 写出 | 刻意重复，但走同一套 BouncyCastle API | 库只写不加密 PKCS#8，覆盖面不足 |
| `SaeaCertificateValidator` | 必须复用，不改一行 | 校验规则是安全边界，两套实现必然漂移 |
| `SaeaCertificateLoader` | 不复用其 PEM 读取 | 它只取首个 `CERTIFICATE` 且带进程内缓存，语义与转换命令冲突 |

## 13. 测试策略

结论：不新增测试工程、不新增 `PackageReference`，用一个 PowerShell 7 脚本做端到端取证。落点 `Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1`，与工具源码同目录但独立运行。理由是工具规模小，脚本足以承担验收取证，新增 csproj 与依赖的长期维护成本反而更高。

不用 Windows PowerShell 5.1：脚本要用 `X509Certificate2.ImportFromPem` / `X509CertificateLoader` / `SignedCms`（均属 .NET 5+），5.1 只有 .NET Framework，缺这些 API。脚本首行检测 `$PSVersionTable.PSVersion.Major -lt 7` 即报错退出。

脚本结构：每条用例 = 一行 `Invoke-Case` 调用，断言四件事——退出码、产物文件存在、PEM 块头与 64 列 base64、用 .NET API 读回关键属性（主题 / 有效期 / SAN / 公钥指纹 / 证书数量）。私钥用 `X509Certificate2.CreateFromPemFile` 验证能加载；PKCS#7 用 `System.Security.Cryptography.Pkcs.SignedCms.Decode` 验证证书数量与顺序；PKCS#12 用 `X509CertificateLoader.LoadPkcs12FromFile`（带口令）验证叶子 / 链 / 私钥。

| 组 | 覆盖内容 |
| --- | --- |
| 往返矩阵 | PEM↔DER 证书、PKCS#8↔PKCS#1、明文↔加密 PKCS#8、PFX 打包↔拆包、P7B 生成↔拆解、CSR 生成与 `x509 -req` 签发；每项断言产物可再次解析且关键属性一致 |
| 双路径读回 | .NET 读回（`ImportFromPem`、`X509CertificateLoader`、`CreateFromPemFile`、`SignedCms`）与工具自身再解析一次产物，确认写路径与读路径互认 |
| 参数与错误 | 未知参数、缺值、互斥（`-newkey`/`-key`、`-nodes`/`-passout`、`-traditional`/cipher）、扩展不在白名单、`-subj` 缺失；断言 stderr 文案与退出码 1 |
| 口令来源 | `pass:` / `env:` / `file:` / `stdin` 四种，含 `env:` 变量缺失、`file:` 首行含尾随空格 |
| 输出流 | `-outform DER` 无 `-out` 时重定向捕获 stdout，断言首字节 `0x30` 且无文本混入；有 `-out` 时 stdout 为空 |
| 遗留回归 | 工具 README 里的两条生成命令与 `--verify` 仍退出 0，产物文件名与格式不变 |

脚本约定：

- 临时目录取 `$env:TEMP` 下的随机子目录，用例结束后删除。
- 每条用例打印 `PASS 用例名` 或 `FAIL 用例名：原因`；末尾打印 `N passed, M failed`；有失败时 `exit 1`。
- 默认用 `dotnet run --project Src/SAEA.Sockets.SslTlsTool -c Release --` 调用工具；`-ToolPath` 参数可指向已发布的可执行文件，跳过 `dotnet run`。

可选差分：环境变量 `SAEA_OPENSSL` 指向 openssl 可执行文件时，额外比对本工具与 openssl 的 `-subject` / `-dates` / `-serial` / `-fingerprint` 文本与 PFX 结构；未设置则跳过并打印 `SKIP openssl 差分（未找到 SAEA_OPENSSL）`。**本机未安装 openssl，差分本轮未实测。**

脚本是验收证据的一部分，但不进 sln、不进 CI（仓库无 CI），需在提交说明里附上运行输出。

## 14. 文档更新

| 文件 | 改动 |
| --- | --- |
| `Src/SAEA.Sockets.SslTlsTool/README.md` | 新增「openssl 兼容子命令」章节 |
| `Src/SAEA.Sockets.SslTlsTool/README.en.md` | 同上，与中文版逐节对应 |
| 根 `README.md` / `README.en.md` | 各加一行指向该工具，不重复参数细节 |
| `Src/SAEA.Sockets/README.md` / `README.en.md` | 各加一行指向该工具，说明证书生成与格式转换在工具侧 |
| `docs/superpowers/specs/2026-11-13-ssl-tls-tool-openssl-compat-design.md` | 本文件 |

工具 README 新增章节的结构：命令→参数映射表（每个子命令一张小表，与 §8 一致，只列常用项）；格式矩阵摘要（压缩 §9 主干，不复制全表）；5–8 条最常用示例（DER→PEM、PKCS#8→PKCS#1、PFX→PEM、PEM+私钥→PFX、`-legacy` 导出、CSR 生成与签发、P7B 打包与拆解）；§11.3 差异全表与结论句；退出码说明（兼容分支 0/1 与遗留分支 0/1/2/3 并存，需写清如何判断走的是哪条分支）；`-passin`/`-passout` 四种来源与 `pass:` 进 shell history 的提醒。

需要改写的既有句子（命令本身不动）：把「用外部 openssl 转换」改为「用本工具的兼容子命令，或外部 openssl」；把「本工具只生成证书」改为「生成与格式转换」；PFX 默认算法描述补上 `-legacy` 分支。中英两版同批改动，示例命令两侧一致。

两份 README 各补一小节「怎么验证」：给出 `pwsh Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1` 的调用方式、`-ToolPath` 的用途、预期末行 `N passed, 0 failed`，并说明 `SAEA_OPENSSL` 只影响可选的差分用例。

## 15. 我替你做过的决定（可逐条否决）

1. 兼容层全放工具工程：不动库公开面与包版本，风险最小。
2. 兼容分支退出码只用 0 / 1：复用 2 / 3 会与遗留「全部跳过未写入」的语义撞车。
3. `-text` / `-nameopt` / `-config` 不支持：完整 ASN.1 dump 与配置文件解析的成本远超本次收益。
4. `pkcs7 -print_certs` 不输出 `subject=` / `issuer=` 摘要行：摘要行与 PEM 混排会逼管道消费者先过滤。
5. PFX 默认参数逐项对齐 openssl 3：证书与私钥加密都是 AES-256-CBC，PBKDF2 迭代 2048，MAC 用 HMAC-SHA-256、迭代 2048，PBE salt 8 字节；`-legacy` 改走 3DES（私钥）、RC2-40（证书）、SHA-1 MAC。理由：用户要求与 openssl 3 一致，旧算法只作显式降级。
6. 默认不写 friendlyName，`-name` 给了才写。理由：openssl 3 未给 `-name` 时不设置别名（仅当证书自带 alias 才沿用）。
7. `-nodes` 与 `-passout` 互斥：两者语义冲突，静默取一等于骗用户。
8. 只做 RSA：库内生成路径只有 RSA，加 EC / Ed25519 要引入参数族与新密钥格式。
9. `version` 用自定义格式：明确声明不是 openssl，避免脚本误判能力。
10. 交互提示语用英文：贴合 openssl 的肌肉记忆，也便于复制搜索。
11. 不新建测试工程，用 `tests/verify-openssl-compat.ps1` 取证。理由：工具规模小，新增 csproj 与 `PackageReference` 的长期维护成本高于脚本。
12. 不新增 NuGet 依赖：BouncyCastle 已传递可用，加包要用户批准。
13. 不改 `SAEA.Sockets` 公开 API：兼容层只服务 CLI，公开面定型后难回收。
14. `x509` 只支持 `-extfile`，不从命令行加扩展：`x509 -addext` 语义含糊，白名单外的扩展更不可控。
15. `-subj` 不支持 `+` 多值：多值 DN 在 oneline 输出里无法无歧义表达。
16. `-passin` 不支持 `fd:`：文件描述符在 Windows 上无对应物，跨平台行为会分叉。
17. 扩展白名单只留四类：白名单外的扩展没有校验手段，宁可报错也不写错。
18. 口令错误与文件损坏合并文案：避免把报错差异变成口令探测手段。
19. `-help` 写 stdout 并退出 0，`-info` 摘要写 stderr：与 openssl 行为一致，也避免摘要文本污染 `-out`/stdout 的数据管道。
20. `-out` 目标已存在时直接覆盖，兼容分支永不返回 3：「跳过已有文件」是遗留生成分支的语义，转换命令静默跳过会让脚本以为转换成功。
21. 交互模式下禁用 `-passin stdin`、且 `-outform DER` 必须给 `-out`：交互循环本身占用 stdin 和 stdout，否则会吃掉下一条命令、或把提示符混进二进制。

## 16. 风险与缓解

| 风险 | 触发场景 | 缓解 |
| --- | --- | --- |
| 文本输出保真度不足 | 脚本用正则抓 `subject=`、`Bag Attributes` 或 `MAC:` | §11.3 逐条登记，README 给替代参数；必要时补 `-nameopt` 而非改默认 |
| PFX 旧算法兼容失败 | 产物交给 Windows 旧组件或 openssl 1.1.1 | 默认 AES-256-CBC + PBKDF2，保留 `-legacy` 走 RC2-40 / 3DES，并在示例中成对给出 |
| BouncyCastle 在 net10.0 的 API 差异 | 2.6.2 与旧文档示例签名不一致 | 只依赖稳定类型，编译即暴露；差异写进实现笔记，不靠运行时反射兜底 |
| 口令泄露 | `pass:` 进命令行 / 异常消息带口令 | 口令只走内存，异常统一用 `OpenSslException` 包装且不带口令；README 提醒 `pass:` 的风险 |
| 能力膨胀带来维护成本 | 子命令越加越多，参数逐渐对齐 openssl | 参数矩阵按白名单冻结，未列参数一律报错；新增能力须另提请求 |
| 两套证书构建逻辑漂移 | 库内 `SaeaCertificateWriter` 与 `OpenSsl` 各自演进 | 校验复用库、构建刻意重复但共用 BouncyCastle API，靠遗留回归用例守行为 |
| 本机无 openssl 无法差分 | 无对照基准，格式细节靠推断 | 以 .NET 与工具自身双向读回作为主证据；PFX 参数对齐以 openssl 3.0 源码默认值为准（`crypto/pkcs12/p12_crt.c` 的 `PKCS12_create_ex` 与 `include/openssl/pkcs12.h.in`），找到有 openssl 的环境后补一次实测 |
| 脚本取证覆盖不足 | 脚本只断言可读回，不断言字节级细节（如 PEM 空行、P7B 顺序细节） | 脚本按 §11 的保真度规则逐项断言块头/行宽/换行；新增规则时同步加断言 |

## 17. 验收标准

1. 在未安装 openssl 的 Windows 上按工具 README 完成六类转换（DER→PEM、PKCS#8↔PKCS#1、PFX→PEM、PEM+私钥→PFX、P7B 打包、P7B 拆解），全部退出码 0，产物能被 .NET 读回。
2. `req -x509 -newkey rsa:2048 -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"` 生成的证书只含一个 SAN 扩展，`DNS:localhost` 进 `dNSName`、`IP:127.0.0.1` 进 `iPAddress`，未给 `-addext` 时无 SAN。
3. `pkcs12 -export` 后用 `pkcs12 -in … -nodes` 拆包，叶子证书、链顺序、私钥与口令语义不变；`-legacy` 产物能被只支持 3DES / RC2 的旧算法读回。
4. 加密 PKCS#8 的加解密往返、PKCS#1 的读写往返均无损，且密文块头为 `ENCRYPTED PRIVATE KEY`、明文为 `PRIVATE KEY`、PKCS#1 为 `RSA PRIVATE KEY`。
5. `crl2pkcs7 -nocrl -certfile chain.pem` 后用 `pkcs7 -print_certs` 拆解，证书数量与顺序与输入一致。
6. 遗留 `--self-signed`、`--ca`、`--verify` 与交互模式的输出、文件布局、退出码（含 2 / 3 语义）完全不变。
7. 任意未知参数、缺值、互斥组合均报错退出 1，且 stdout 无任何文本污染；`-outform DER` 无 `-out` 时 stdout 只有原始二进制。
8. `dotnet build Src/SAEA.Sockets.sln -c Debug` 0 errors；`Src/SAEA.Sockets.SslTlsTool/tests/verify-openssl-compat.ps1` 在 pwsh 7 下全流程 `N passed, 0 failed`、退出码 0；`Src/SAEA.Common.Tests`、`Src/SAEA.MQTT.Tests`、`Src/SAEA.FTP.Tests`、`Src/SAEA.Sockets.Pipeline.Tests` 四个基线工程全绿。仓库内其它 MSTest 工程不在本次范围。

## 18. 未决问题

本节两项已在计划中定稿，不再阻塞评审：

1. `-nameopt RFC2253` 本期不做。姓名固定 oneline 风格（`CN = localhost, O = SAEA`），`x509 -nameopt` 显式报错并指向 `-subject`/`-issuer`；后续确有 RFC2253 需求时再补独立格式化路径，同时扩 §11.2 表与 README。
2. `version` 保留。输出 `<工具名> <版本>（openssl 兼容子集）`，只用于探测本工具版本；README 明确它不等于 `openssl version`，版本探测脚本需按前缀自行判断。

## 19. 实现回填

1. §12 的 `Pkcs12Codec.Save` 未用 `Pkcs12Store`：BouncyCastle 2.6.2 的 `Pkcs12Store` 不能指定迭代次数、salt 与 MAC 摘要算法，无法满足 §8.3 的「迭代 2048 / MAC HMAC-SHA-256」要求（`-legacy` 才走 SHA-1），改为按 PKCS#12 ASN.1 手工构造 `Pfx`（`Pkcs12Codec.cs` 的 `Save` 内联构造 `AuthenticatedSafe`，私有方法 `EncryptKey`、`EncryptedCerts`、`Mac` 分别产出加密私钥、加密证书与 `MacData`）。
2. §12 的目录细化：每个子命令一个文件，统一放 `OpenSsl/Commands/`（`X509Command`、`Pkcs12Command`、`Pkcs7Command` 等）；`OptionReader`、`OpenSslUsage`、`OpenSslException`、`OpenSslCommand` 留在 `OpenSsl/` 根。
3. `pkcs12` 在 §8.3 原表之外增加两个参数：`-nokeys`（读取时不输出私钥）、`-nomac`（导出时不写 MAC，与 `-passout` 互斥）；`-CAfile` 不再列出（openssl 的 `pkcs12` 本身没有该参数，本工具不实现，出现时报不支持参数）。
4. 退出码分两条分支并存：首参以 `-` 开头或无参数时整段交给遗留解析器（0/1/2/3）；其余首参交给兼容分支（0/1），兼容分支内再与 `req`/`x509`/`pkcs8`/`pkey`/`pkcs12`/`crl2pkcs7`/`pkcs7`/`version` 精确匹配（区分大小写），未命中报不支持子命令并退出 1。与 §7 的分派口径一致。
5. 兼容分支的用法/错误文本统一走 `OpenSslUsage`，stdout 只写数据，提示一律走 stderr，落实 §11.1。
6. `x509 -CAserial` 与 §8.2「遇到报专用文案」的差异：该参数不进白名单，由 `OptionReader` 统一报 `error: 不支持的参数 "-CAserial"；支持的参数见 -help。`，比专用文案更早拦截，语义（不支持该参数、序列号文件固定为 `<CA 文件名>.srl`）一致。
7. `OpenSsl/Commands/` 子目录使用独立命名空间 `SAEA.Sockets.SslTlsTool.OpenSsl.Commands`；`OpenSsl/` 根下的类型仍在 `SAEA.Sockets.SslTlsTool.OpenSsl`，与 §12「不再分子命名空间」按目录分层落地。
8. `PasswordResolver.Resolve` 去掉 `option` 参数（错误文案不拼参数名），签名实为 `Resolve(string? spec, string target, bool confirm, bool interactive)`；`req -newkey` 在既无 `-nodes` 也无 `-passout` 时按明文写私钥并在 stderr 提示，不隐式索要口令，避免非交互脚本挂起（已记入 §11.3）。

状态：已实现，待验收