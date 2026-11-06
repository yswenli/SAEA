# SAEA.Sockets.Pipeline 实施计划

上游设计：`docs/superpowers/specs/2026-11-05-socket-pipeline-design.md`（下称"设计文档"）。
本计划只覆盖设计文档已定项，不新增架构决策；实现中发现设计缺陷，先改设计文档再改代码。

## 目标与交付边界

- 新增独立核心包 `Src/SAEA.Sockets.Pipeline/`：`netstandard2.0` + `LangVersion 8.0`，不引用 `SAEA.Sockets`，只引用 `SAEA.Common`。
- 新增控制台演示 `Src/SAEA.Sockets.PipelineTest/`（入 sln）。
- 新增 MSTest 工程 `Src/SAEA.Sockets.Pipeline.Tests/`（**不入 sln**，与另外三个单测工程一致）。
- 改造 `SAEA.Sockets` 的三处接入点：`SocketOptionBuilder`、`SaeaServerSocket`、`SaeaConnection`（双来源闸门）。
- 既有 `ICoder` / `BaseCoder` / `FrameDecoder` / `OnServerReceiveSpan` 不改语义。

## 通用约定（每个 Task 都适用）

1. **TDD**：先写失败的测试，再写实现，最后重构。测试工程用 MSTest（`Microsoft.NET.Test.Sdk` 17.8.0 / `MSTest.*` 3.1.1）。
2. **C# 8 上限**：禁止 `record`、`init`、file-scoped namespace、`is not`、`??=`、可空引用类型标注；公开异步方法返回 `Task`，不用 `ValueTask`。
3. **库代码不写溯源注释**：不写"见设计 §X"，规则本身写完整；跨文件引用写"文件名 + 主题"。
4. 每个 Task 结束时必须跑该 Task 指定的验证命令，并把**退出码 + 关键输出**贴回。未运行不得声称通过。
5. 每个子代理只领一个 Task；输出限制在 100 行以内（改动文件清单 + 测试结果），避免长输出被截断。
6. 不改与本 Task 无关的文件。发现范围外问题，记录在报告里，不顺手修。

## 环境与基线

```bash
dotnet --version                                  # 10.0.403
dotnet build Src/SAEA.Sockets.sln -c Debug        # 0 errors，基线 93 warnings
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release   # 59
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release       # 153
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release         # 13
```

新包版本 `26.11.5.1`；`GeneratePackageOnBuild=true`。

---

## 里程碑 0 — 工程骨架

### Task 0.1 创建核心库工程

**文件**

- 新增 `Src/SAEA.Sockets.Pipeline/SAEA.Sockets.Pipeline.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>8.0</LangVersion>
    <Version>26.11.5.1</Version>
    <Authors>yswenli</Authors>
    <Company>yswenli</Company>
    <Description>SAEA.Sockets.Pipeline is a bidirectional channel pipeline with pluggable frame splitting and codecs for SAEA.
SAEA.Sockets.Pipeline 是 SAEA 的双向通道链，带可插拔分帧器与两级编解码器</Description>
    <Copyright>yswenli</Copyright>
    <PackageProjectUrl>https://github.com/yswenli/SAEA</PackageProjectUrl>
    <RepositoryUrl>https://github.com/yswenli/SAEA</RepositoryUrl>
    <PackageTags>socket,pipeline,codec,dotnetty</PackageTags>
    <PackageRequireLicenseAcceptance>true</PackageRequireLicenseAcceptance>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageLicenseFile>LICENSE.txt</PackageLicenseFile>
    <PackageIcon>favicon.png</PackageIcon>
    <Title>SAEA.Sockets.Pipeline</Title>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\SAEA.Common\SAEA.Common.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\..\favicon.png">
      <Pack>True</Pack>
      <PackagePath></PackagePath>
    </None>
    <None Include="..\..\LICENSE.txt">
      <Pack>True</Pack>
      <PackagePath></PackagePath>
    </None>
    <None Update="README.md">
      <Pack>True</Pack>
      <PackagePath>\</PackagePath>
    </None>
  </ItemGroup>

</Project>
```

- 新增空 `Src/SAEA.Sockets.Pipeline/README.md`（中文）与 `README.en.md`（英文）。里程碑 5 补内容，先建占位避免打包失败。
- 执行 `dotnet sln Src/SAEA.Sockets.sln add Src/SAEA.Sockets.Pipeline/SAEA.Sockets.Pipeline.csproj`。

**验证**

```bash
dotnet build Src/SAEA.Sockets.Pipeline/SAEA.Sockets.Pipeline.csproj -c Debug
```

预期：0 errors。此时 `SAEA.Common` 会带来 `System.IO.Pipelines 10.0.14`、`System.Threading.Channels 10.0.14`、`System.Memory 4.6.3`、`protobuf-net 3.4.40` 的传递引用，**不要**重复添加同版本 PackageReference。

### Task 0.2 创建测试工程与演示工程

**文件**

- `Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <LangVersion>10.0</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.1.1" />
    <PackageReference Include="MSTest.TestFramework" Version="3.1.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SAEA.Sockets.Pipeline\SAEA.Sockets.Pipeline.csproj" />
    <ProjectReference Include="..\SAEA.Sockets\SAEA.Sockets.csproj" />
  </ItemGroup>

</Project>
```

> 测试工程引用 `SAEA.Sockets` 仅用于 parity（`BaseCoder` / `BaseSocketProtocal`）。核心库本身不得引用它。

- `Src/SAEA.Sockets.PipelineTest/SAEA.Sockets.PipelineTest.csproj`：`net10.0`，`OutputType=Exe`，引用核心包 + `SAEA.Sockets`；用 `dotnet sln add` 加入 sln。
- `Src/SAEA.Sockets.PipelineTest/Program.cs`：暂时只打印用法并返回 0，`--all` 也返回 0。

**验证**

```bash
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Debug
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Debug -- --all
```

预期：0 个测试（或 1 个占位测试）通过；演示退出码 0。

---

## 里程碑 1 — 核心抽象、分帧、编码器

对应设计文档 §5.1–§5.3 的接口、§5.2 的分帧器与编码器、§5.4 的 `PooledMessage`、§6.2 的 `FrameAccumulator`。

### Task 1.1 抽象接口与基础类型

**文件**（`Src/SAEA.Sockets.Pipeline/Abstractions/`）

- `IChannelHandler.cs`：`IChannelHandler` / `IChannelInboundHandler` / `IChannelOutboundHandler`
- `IChannelPipeline.cs`
- `IChannelHandlerContext.cs`
- `IChannel.cs`
- `IRawByteStream.cs`
- `ICodec.cs`、`IMessageCodec.cs`
- `IFrameSplitter.cs`、`IFrameEncoder.cs`
- `IMessageHandler.cs`
- `ITypeRegistry.cs`、`TypeRegistry.cs`
- `IChannelGuard.cs`、`NullChannelGuard.cs`、`ChannelTraffic.cs`
- `IPipelineHost.cs`

`Src/SAEA.Sockets.Pipeline/FrameConstants.cs`：

```csharp
namespace SAEA.Sockets.Pipeline
{
    /// <summary>线格式常量：8B 小端 body 长度 + 1B type + body。</summary>
    public static class FrameConstants
    {
        public const int P_LEN = 8;
        public const int P_Type = 1;
        public const int P_Head = P_LEN + P_Type;
    }
}
```

`Src/SAEA.Sockets.Pipeline/Exceptions/`：

- `FrameProtocolException.cs`：`public sealed class FrameProtocolException : Exception`，两个构造（message / message + inner）。
- `MessageTimeoutException.cs`：`public sealed class MessageTimeoutException : Exception`，属性 `ChannelId`、`Timeout`。
- `ChannelClosedException.cs`：`public sealed class ChannelClosedException : Exception`。

`TypeRegistry` 实现（两个 `ConcurrentDictionary`，注册期加一把锁保证双向原子）：

```csharp
public sealed class TypeRegistry : ITypeRegistry
{
    private readonly object _gate = new object();
    private readonly ConcurrentDictionary<int, Type> _codeToType = new ConcurrentDictionary<int, Type>();
    private readonly ConcurrentDictionary<Type, int> _typeToCode = new ConcurrentDictionary<Type, int>();

    public void Register(int typeCode, Type clrType)
    {
        if (clrType == null) throw new ArgumentNullException(nameof(clrType));
        if (typeCode < 0 || typeCode > 255)
            throw new ArgumentOutOfRangeException(nameof(typeCode), "type code 必须落在 0..255（线上 1 字节）。");

        lock (_gate)
        {
            if (_codeToType.ContainsKey(typeCode))
                throw new InvalidOperationException($"type code {typeCode} 已注册。");
            if (_typeToCode.ContainsKey(clrType))
                throw new InvalidOperationException($"CLR 类型 {clrType.FullName} 已注册。");

            _codeToType[typeCode] = clrType;
            _typeToCode[clrType] = typeCode;
        }
    }

    public Type Resolve(int typeCode) { Type t; return _codeToType.TryGetValue(typeCode, out t) ? t : null; }
    public int ResolveCode(Type clrType) { int c; return clrType != null && _typeToCode.TryGetValue(clrType, out c) ? c : -1; }
}
```

`ChannelTraffic` 用只读属性结构（字段类型与设计文档 §6.2 一致，`BufferedBytes` 是 `long`）：

```csharp
public readonly struct ChannelTraffic
{
    public long TotalReceivedBytes { get; }
    public long BufferedBytes { get; }
    public int QueuedMessages { get; }
    public ChannelTraffic(long totalReceivedBytes, long bufferedBytes, int queuedMessages) { ... }
}

public interface IChannelGuard
{
    bool Allow(in ChannelTraffic traffic);
}

public sealed class NullChannelGuard : IChannelGuard
{
    public static readonly NullChannelGuard Instance = new NullChannelGuard();
    public bool Allow(in ChannelTraffic traffic) { return true; }
}
```

`IChannelGuard.Allow` 的异常处理口径由 executor 负责：钩子抛异常按"超限"处理（记录后走 `guard-rejected` 收口），不放行。

**验证**

```bash
dotnet build Src/SAEA.Sockets.Pipeline/SAEA.Sockets.Pipeline.csproj -c Debug
```

### Task 1.2 `PooledMessage`

**文件**：`Src/SAEA.Sockets.Pipeline/Runtime/PooledMessage.cs`

要求（设计文档 §5.4）：

- 字段：`ReadOnlyMemory<byte> _frame`、`byte[] _buffer`、`int _originalSize`、`bool _isPooled`、`int _refCount`、`volatile bool _released`。
- 构造函数 `internal PooledMessage(ReadOnlyMemory<byte> frame, byte[] buffer, int originalSize, bool isPooled)`，引用计数初始 1。
- `Frame` getter：`if (_released) throw new ObjectDisposedException(nameof(PooledMessage)); return _frame;`
- `Retain()`：`if (_released) throw new ObjectDisposedException(...); Interlocked.Increment(ref _refCount);`
- `Release()`：

```csharp
public void Release()
{
    if (Interlocked.Decrement(ref _refCount) != 0) return;
    if (_released) return;
    _released = true;
    var buffer = _buffer;
    _buffer = null;
    _frame = ReadOnlyMemory<byte>.Empty;
    if (_isPooled && buffer != null)
        MemoryPoolManager.Return(buffer, _originalSize);
}
```

- `Dispose()` 等价 `Release()`。`IsPooled` / `IsReleased` 只读属性。
- **`Release` 对已归零实例是 no-op**：第二次 `Release` 会 `Interlocked.Decrement` 成负数——必须先判断 `_refCount <= 0` 再直接返回：

```csharp
public void Release()
{
    if (Volatile.Read(ref _refCount) <= 0) return;
    if (Interlocked.Decrement(ref _refCount) != 0) return;
    ...
}
```

> 注意：`Retain` 与 `Release` 并发时依赖引用计数正确；本栈的所有权模型保证同一实例的 `Retain` 只在入队前/业务显式持有前发生，不要求无锁下的 ABA 安全。

**测试** `Tests/Runtime/PooledMessageTests.cs`

- 池化背衬 `Release` 后 `MemoryPoolManager.GetStatistics()` 的 `SmallPoolReturned` 增加。
- 非池化背衬 `Release` 后不增加归还计数。
- `Release` 两次不抛、不重复归还（归还计数只加 1）。
- `Release` 后访问 `Frame` 抛 `ObjectDisposedException`。
- `Retain` 后 `Release` 一次仍未归还，第二次才归还。

**验证**

```bash
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Debug --filter "FullyQualifiedName~PooledMessageTests"
```

### Task 1.3 `FrameAccumulator`

**文件**：`Src/SAEA.Sockets.Pipeline/Runtime/FrameAccumulator.cs`

```csharp
/// <summary>增量分帧累加器。非线程安全，只能由泵协程独占使用。</summary>
public sealed class FrameAccumulator : IDisposable
{
    private byte[] _buffer;
    private int _start;
    private int _end;

    public int BufferedLength { get { return _end - _start; } }

    public ReadOnlyMemory<byte> Buffered
    {
        get { return new ReadOnlyMemory<byte>(_buffer, _start, _end - _start); }
    }

    public void Append(ReadOnlyMemory<byte> data)
    {
        if (data.Length == 0) return;
        EnsureCapacity(data.Length);
        data.Span.CopyTo(_buffer.AsSpan(_end));
        _end += data.Length;
    }

    /// <summary>消费已拆分的前 count 字节；count 必须在 [0, BufferedLength]。</summary>
    public void Consume(int count)
    {
        if (count < 0 || count > BufferedLength) throw new ArgumentOutOfRangeException(nameof(count));
        _start += count;
        if (_start == _end) { _start = 0; _end = 0; }
    }

    private void EnsureCapacity(int extra)
    {
        var required = _end + extra;
        if (required <= _buffer.Length) return;

        // 先尝试原地紧凑，避免无谓扩容。
        if (_start > 0)
        {
            Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
            _end -= _start;
            _start = 0;
            if (_end + extra <= _buffer.Length) return;
        }

        var newSize = _buffer.Length;
        while (newSize < _end + extra) newSize = checked(newSize * 2);
        var next = MemoryPoolManager.Rent(newSize);
        Buffer.BlockCopy(_buffer, 0, next, 0, _end);
        MemoryPoolManager.Return(_buffer, _buffer.Length);
        _buffer = next;
    }

    public void Reset() { if (_buffer != null) { MemoryPoolManager.Return(_buffer, _buffer.Length); _buffer = null; } _start = 0; _end = 0; }
    public void Dispose() { Reset(); }
}
```

- 构造函数里 `_buffer = MemoryPoolManager.Rent(1024)`。
- **不要**设 `maxBufferedBytes + readBufferSize` 的物理上限（设计文档 §6.2 已明确按需扩容）。
- `Reset()` 必须幂等（`_buffer == null` 时直接返回）。

**测试** `Tests/Runtime/FrameAccumulatorTests.cs`

- 追加 → `BufferedLength` 正确、`Buffered` 内容正确。
- `Consume` 部分消费后剩余内容正确；全部消费后 `_start/_end` 归零。
- `Consume` 越界抛 `ArgumentOutOfRangeException`。
- 反复「追加 1 字节 → 消费 1 字节」10 万次，`MemoryPoolManager` 租借增量不超过 2（紧凑逻辑生效）。
- 一次追加 1MB，内容不丢、长度正确。
- `Reset` 后 `BufferedLength == 0`；`Reset` 两次不抛；`Dispose` 后归还计数增加。

### Task 1.4 `LengthFieldSplitter`

**文件**：`Src/SAEA.Sockets.Pipeline/Splitters/LengthFieldSplitter.cs`

```csharp
public sealed class LengthFieldSplitter : IFrameSplitter
{
    private readonly int _maxFrameLength;
    private readonly int _lengthFieldOffset;
    private readonly int _lengthFieldLength;
    private readonly int _lengthAdjustment;
    private readonly int _initialBytesToStrip;
    private readonly bool _isLittleEndian;

    public int MaxFrameLength { get { return _maxFrameLength; } }

    public static LengthFieldSplitter Default(int maxFrameLength)
        => new LengthFieldSplitter(maxFrameLength, 0, FrameConstants.P_LEN,
                                   FrameConstants.P_Head - FrameConstants.P_LEN,
                                   FrameConstants.P_LEN, true);

    public static LengthFieldSplitter LegacyCompatible(int maxFrameLength)
        => new LengthFieldSplitter(maxFrameLength, 0, FrameConstants.P_LEN,
                                   FrameConstants.P_Head - FrameConstants.P_LEN, 0, true);

    public bool TrySplit(ReadOnlyMemory<byte> buffered, out int consumed, out ReadOnlyMemory<byte> frame)
    {
        consumed = 0;
        frame = ReadOnlyMemory<byte>.Empty;

        var head = _lengthFieldOffset + _lengthFieldLength;
        if (buffered.Length < head) return false;

        long bodyLen = ReadLength(buffered.Span);
        if (bodyLen < 0 || bodyLen > _maxFrameLength)
            throw new FrameProtocolException($"非法的数据帧长度: {bodyLen}");

        long total = (long)head + bodyLen + _lengthAdjustment;
        if (total < head || total > int.MaxValue)
            throw new FrameProtocolException($"帧总长度越界: {total}");
        if (buffered.Length < total) return false;

        consumed = (int)total;
        frame = buffered.Slice(_initialBytesToStrip, (int)total - _initialBytesToStrip);
        return true;
    }

    public void Reset() { }
}
```

要点：

- `lengthAdjustment = P_Head - P_LEN = 1`，则 `total = 8 + bodyLen + 1 = P_Head + bodyLen`。
- `initialBytesToStrip=8` 时 frame 是 `[1B type][body]`；`=0` 时是完整帧。
- 构造函数做参数校验：`maxFrameLength >= 0`、`lengthFieldOffset >= 0`、`lengthFieldLength` ∈ {1,2,3,4,8}、`initialBytesToStrip >= 0 && initialBytesToStrip <= head`。
- `ReadLength` 支持 1/2/3/4/8 字节与小端/大端；3 字节手工拼。
- 不做 `FrameDecoder` 的 `maxFrameLength < P_Head` 夹逼（设计文档 §5.2）。

**测试** `Tests/Splitters/LengthFieldSplitterTests.cs`

- 0/1/8/9 字节输入（不足头）返回 false。
- 完整帧返回 `consumed = P_Head + bodyLen`，`Default` 的 frame 首字节 == `LegacyCompatible` frame 下标 8。
- 帧体跨两次 `Append`（1/2/3/7 字节切分）能正确产出。
- `bodyLen < 0`、`bodyLen > MaxFrameLength` 抛 `FrameProtocolException`。
- 三帧粘连一次输入，连续三次 `TrySplit` 后 `BufferedLength == 0`。
- 空 body 合法。

### Task 1.5 其余三个分帧器

**文件**

- `Splitters/DelimiterSplitter.cs`：构造 `(byte[] delimiter, int maxFrameLength, bool stripDelimiter = true)`。找不到分隔符且 `buffered.Length > maxFrameLength` → `FrameProtocolException`；找到则 `consumed = index + delimiter.Length`，`frame = stripDelimiter ? buffered.Slice(0, index) : buffered.Slice(0, index + delimiter.Length)`。空分隔符抛 `ArgumentException`。
- `Splitters/LineSplitter.cs`：`DelimiterSplitter` 的包装，分隔符 `\n`，剥掉分隔符并去掉尾部 `\r`。
- `Splitters/FixedLengthSplitter.cs`：`frameLength > 0`；`BufferedLength < frameLength` 返回 false；`consumed = frameLength`，`frame = buffered.Slice(0, frameLength)`；`MaxFrameLength == frameLength`。

三者都无状态，`Reset()` 空实现。

**测试** `Tests/Splitters/OtherSplittersTests.cs`

- 分隔符跨段、一次多帧、超长无分隔符抛错、`stripDelimiter=false` 保留分隔符。
- `LineSplitter` 处理 `\r\n` 与 `\n`。
- 定长分帧：不足返回 false、正好一帧、多帧连续。

### Task 1.6 `LengthFieldFrameEncoder`

**文件**：`Src/SAEA.Sockets.Pipeline/Splitters/LengthFieldFrameEncoder.cs`

```csharp
public sealed class LengthFieldFrameEncoder : IFrameEncoder
{
    public void Encode(ReadOnlyMemory<byte> body, int typeCode, IBufferWriter<byte> writer)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        if (typeCode < 0 || typeCode > 255)
            throw new ArgumentOutOfRangeException(nameof(typeCode), "type code 必须落在 0..255。");

        var head = writer.GetSpan(FrameConstants.P_Head);
        BinaryPrimitives.WriteInt64LittleEndian(head, body.Length);   // body 长度，不是总长
        head[FrameConstants.P_LEN] = (byte)typeCode;
        writer.Advance(FrameConstants.P_Head);
        writer.Write(body.Span);
    }
}
```

> `body.Length` 是 `int`，写入 `long` 自动提升。

**测试** `Tests/Splitters/LengthFieldFrameEncoderTests.cs`

- 输出首 8 字节小端解出的整数 == `body.Length`（不是 `body.Length + 9`）。
- 输出第 9 字节 == typeCode。
- 与 `BaseSocketProtocal`（`WriteTo`）对同一 body/type 的输出逐字节相等。
- `typeCode = -1` / `256` 抛 `ArgumentOutOfRangeException`。
- 空 body：输出恰好 9 字节，长度字段为 0。

> `BaseSocketProtocal` 的构造签名以 `Src/SAEA.Sockets/Base/BaseSocketProtocal.cs` 为准；测试里用它公开的构造函数（`Type` / `Content`）构造后调 `WriteTo(PooledBufferWriter)`。若构造不可用，改用 `BaseCoder` 的 `Encode(ISocketProtocal, IBufferWriter<byte>)` 路径。

### Task 1.7 parity 验收

**文件**：`Tests/Parity/SplitterParityTests.cs`

- `LengthFieldSplitter.LegacyCompatible(maxFrameLength)` vs `BaseCoder.DecodeStream`：
  - 同一批输入分段喂入（1/2/3/7/13 字节随机切分，固定种子）。
  - 每个产出帧断言：`consumed == P_Head + bodyLen`、`frame[0..8]` 小端 == `BodyLength`、`frame[8] == Type`、`frame.Slice(9)` == `Content`。
- `Default` 与 `LegacyCompatible` 的 `consumed` 相同；`Default.frame` 长度 = Legacy 帧长 − 8；`Default.frame[0] == Legacy.frame[8]`。
- 覆盖：含 `P_Head` 边界、type=0/255、空 body、多段序列。

**验证（里程碑 1 收口）**

```bash
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Debug
```

预期：全绿；`SAEA.Sockets.Pipeline.Tests` 仍然不在 sln 中。

---

## 里程碑 2 — Pipeline 运行时（链 + 执行器）

对应设计文档 §5.1 / §5.1.1 / §5.5 的上下文与链、§6.2 泵、§6.3 队列与收口。

### Task 2.1 `DefaultChannel` 与流量防护

**文件**

- `Runtime/DefaultChannel.cs`：`ConcurrentDictionary<string, object>` 背衬；`IsActive` 用 `volatile bool`；`CloseAsync` 调构造注入的 `Func<string, Task>`；`MarkInactive()` 为 `internal`，置 `IsActive=false` 并 `Clear()`。
- `NullChannelGuard.cs` 已存在；`ChannelTraffic` 已存在。

**测试** `Tests/Runtime/DefaultChannelTests.cs`：并发写 16 个 key 互不覆盖；关闭后 `IsActive == false`、`Attributes.Count == 0`。

### Task 2.2 上下文与链骨架

**文件**

- `Runtime/DefaultChannelHandlerContext.cs`
- `Runtime/ChannelPipeline.cs`（sealed）

链结构：双向链表 + 头/尾哨兵。

```csharp
sealed class Node
{
    public IChannelHandler Handler;
    public Node Prev;
    public Node Next;
    public DefaultChannelHandlerContext Context;
}
```

`DefaultChannelHandlerContext` 字段：`private readonly Node _node;`、`internal ChannelPipeline Pipeline { get; }`（构造函数同时接收两者）；其余对外成员按设计文档 §5.1 的 `IChannelHandlerContext` 实现。

关键方法：

```csharp
public Task FireChannelReadAsync(object msg)
{
    var next = FindNextInbound(_node.Next);
    if (next == null) return Task.CompletedTask;          // 透传到尾，由 ExceptionTerminalHandler 兜底
    return InvokeInboundAsync(next, msg);
}

private async Task InvokeInboundAsync(Node node, object msg)
{
    try { await node.Handler.AsInbound().ChannelReadAsync(node.Context, msg).ConfigureAwait(false); }
    catch (OperationCanceledException) { throw; }         // 超时必须原样冒泡给 executor
    catch (Exception ex) { await Pipeline.FireExceptionCaughtFromAsync(node.Next, ex).ConfigureAwait(false); }
}

public Task WriteAsync(object msg) { ... 找到 Prev 方向第一个 outbound；没有则走 head sink ... }

public Task FireExceptionCaughtAsync(Exception ex) => Pipeline.FireExceptionCaughtFromAsync(_node.Next, ex);
```

- 异常上报必须从**当前节点的下一个节点**开始（`node.Next`），不能包含自己，否则 handler 会收到自己刚抛出的异常。
- `CancellationToken` 读 `Pipeline.CurrentCancellationToken`（executor 的实例 `AsyncLocal`，未设置时回落到连接级 ct）。
- `Channel` 读 `Pipeline.Channel`（Attach 后可用；Attach 前抛 `InvalidOperationException`）。
- `Handler` 返回节点 handler（哨兵返回 null）。
- 入站异常契约：`OperationCanceledException` 原样抛；其它异常走 `FireExceptionCaughtFromAsync(node.Next, ex)` 并**吞掉**，不再抛给调用方（设计文档 §5.1）。
- 出站 `WriteAsync` **不吞异常**，直接冒泡。

`ChannelPipeline` 结构：

```csharp
public sealed class ChannelPipeline : IChannelPipeline
{
    private readonly ChannelPipelineOptions _options;
    private readonly Node _head;              // 哨兵：入站起点、出站终点（head sink）
    private readonly Node _tail;              // 哨兵：入站终点、出站起点
    private readonly object _gate = new object();
    private readonly List<Node> _nodes = new List<Node>();
    private bool _outboundPrefixClosed;
    private IRawByteStream _stream;
    private ChannelPipelineExecutor _executor;
    private volatile bool _closed;
    private int _disposed;

    public static ChannelPipeline Create(ChannelPipelineOptions options) { ... }
    public IChannelPipeline AddLast(IChannelHandler handler) { ... }
    public IChannelPipeline AddFirst(IChannelHandler handler) { ... }
    public IChannelPipeline Remove(IChannelHandler handler) { ... }
    public IReadOnlyList<IChannelHandler> Handlers { get { lock (_gate) return snapshot; } }

    internal CancellationToken CurrentCancellationToken
    {
        get
        {
            var executor = _executor;
            return executor == null ? CancellationToken.None : executor.CurrentToken;
        }
    }

    internal IChannel Channel
    {
        get
        {
            var executor = _executor;
            if (executor == null) throw new InvalidOperationException("pipeline 尚未 Attach 到传输层。");
            return executor.Channel;
        }
    }

    public Task FireChannelReadAsync(object msg) => FireChannelReadFromAsync(_head.Next, msg);
    public async Task FireChannelReadCompleteAsync()
    {
        foreach (var node in InboundNodesSnapshot())    // 快照，避免遍历中被修改
            await node.Handler.AsInbound().ChannelReadCompleteAsync(node.Context).ConfigureAwait(false);
    }
    public Task FireExceptionCaughtAsync(Exception ex) => FireExceptionCaughtFromAsync(_head.Next, ex);

    // Attach 前只把链标记为关闭；Attach 后交给 executor 走完整收口。
    public Task CloseAsync(string reason)
    {
        _closed = true;
        var executor = _executor;
        if (executor != null) return executor.BeginShutdown(reason);
        return Task.CompletedTask;
    }

    internal void Attach(IRawByteStream stream, ChannelPipelineExecutor executor) { ... }
    public void Dispose() { ... }
}
```

- `FireChannelReadAsync` 包一层：链首 handler 抛的异常也要到链尾；`OperationCanceledException` 原样抛。
- `FireReadCompleteAsync` 的异常由**泵**记录并吞（链本身不吞），因为链只知道 handler 抛了；运行时由 `SafeFireReadCompleteAsync` 统一处理。实现上让 `ChannelPipeline.FireChannelReadCompleteAsync` 不捕获，泵包 `try/catch` 记 `Warn`。
- 头哨兵的 `WriteAsync` 是 head sink：

```csharp
// 链头出站终点
if (!(msg is TransportFrame frame))
    throw new InvalidOperationException("出站链缺少 FrameEncoderHandler：链头只接受 TransportFrame。");
if (_stream == null || _executor == null)
    throw new InvalidOperationException("pipeline 尚未 Attach 到传输层。");
if (_closed || !_executor.IsActive)
    throw new ChannelClosedException("连接已进入收口，不能继续发送。");
await _stream.SendAsync(frame.Data, frame.Owner).ConfigureAwait(false);
// 正常返回 = 所有权已交接；此处不 Dispose owner
```

> `TransportFrame` / `EncodedBody` 在 Task 3.2 建立；本 Task 若先实现 head sink，可先引用类型占位（同一 `Runtime` 命名空间），或把 head sink 的类型检查留到 Task 3.2 的失败测试驱动。二者选一，但**不能**跳过类型检查。

**IsSharable 注册表**（`Runtime/HandlerRegistrationTable.cs`，静态）：

- `ConditionalWeakTable<IChannelHandler, Registration>`，`Registration { int Count; }`。
- `Acquire(handler)`：`Count++`；若 `Count > 1 && !handler.IsSharable` → `Count--` 并抛 `InvalidOperationException`。
- `Release(handler)`：`Count--`（不小于 0）。
- 一把 `lock` 足够；`ConditionalWeakTable` 条目在计数归零后可被回收。

### Task 2.3 `ChannelPipeline.Create` 校验与结构约束

**文件**：`Runtime/ChannelPipelineOptions.cs`（**本 Task 新建**；Task 2.1 只做 `DefaultChannel`）、`Runtime/DisconnectDrainPolicy.cs`、`ChannelPipeline.cs`

选项与校验按设计文档 §6.3 原文实现：

- `Splitter != null`；`QueueCapacity >= 2`；`MaxConcurrentMessages >= 1`。
- `ResumeWatermark == 0` → 归一化为 `max(1, QueueCapacity / 2)`；否则必须 `0 < ResumeWatermark < QueueCapacity`。
- `MessageTimeout >= 0`、`DrainTimeout >= 0`。
- `SmallFrameInlineThreshold >= 0`、`EnvelopeCopyThreshold >= 0`、`ReadBufferSize > 0`。
- `MaxBufferedBytes`：`0` → `Splitter.MaxFrameLength + P_Head + ReadBufferSize`（用 `long` 计算后校验不超过 `int.MaxValue`）；非 0 且 `< Splitter.MaxFrameLength + P_Head` → 抛。
- `Guard == null` → `NullChannelGuard.Instance`。
- `ChannelPipelineOptions` 在 `Create` 内**拷贝/归一化**到 pipeline 私有字段，之后修改 options 不影响运行中的 pipeline。

结构约束：

- `AddLast(outbound)`：若 `_outboundPrefixClosed` → 抛（消息给出标准装配顺序）。
- `AddLast(inbound)`：`_outboundPrefixClosed = true`。
- `AddFirst(outbound)`：合法（前缀向左扩展）。
- `AddFirst(inbound)`：若链首已有 outbound（即 `_head.Next` 是 outbound）→ 抛；否则合法。
- `Remove` 后重新计算前缀状态：从 `_head.Next` 起统计连续 outbound 数量，`_outboundPrefixClosed = 存在 outbound 之后的 inbound`。

**测试** `Tests/Runtime/PipelineStructureTests.cs`

- 只装入站 handler，链可建、`FireChannelReadAsync` 不抛。
- outbound 夹在 inbound 之后 → `AddLast` 抛。
- `AddFirst(inbound)` 在链首有 outbound 时抛；没有时合法。
- `Remove` 后重新 `AddLast` 结构状态正确。
- 选项校验逐条：null splitter、`QueueCapacity=1`、`ResumeWatermark>=QueueCapacity`、`MaxBufferedBytes` 过小、`ReadBufferSize=0` 均抛，且**在 `Create` 返回前**抛。
- `MaxBufferedBytes=0` 推导值等于 `MaxFrameLength + 9 + ReadBufferSize`。

### Task 2.4 `ChannelPipelineExecutor`：队列、泵、背压

**文件**：`Runtime/ChannelPipelineExecutor.cs`

公开面：

```csharp
public sealed class ChannelPipelineExecutor : IDisposable
{
    public ChannelPipelineExecutor(IRawByteStream stream, IChannel channel,
                                   ChannelPipelineOptions options,
                                   CancellationToken transportToken,
                                   IPipelineHost host = null);
    public IChannel Channel { get; }
    public bool IsActive { get; }          // !_closing，供 head sink 做快速拒绝
    public Task Completion { get; }        // 收口第 2–6 步
    public void Attach(ChannelPipeline pipeline);   // 内部调 pipeline.Attach(stream, this)
    public void Start();                   // 启动泵 + 消费者循环
    public Task BeginShutdown(string reason);   // 公开入口，等价 fromPump:false；只做第 1 步并返回
    internal Task BeginShutdown(string reason, bool fromPump);  // 泵自身触发用
    public void Dispose();
    internal CancellationToken CurrentToken { get; }   // 供 ctx.CancellationToken 回落
}
```

- 构造函数把 `transportToken` 与 `_pumpCts.Token` 做 `CreateLinkedTokenSource`，并注册一次回调：`transportToken` 取消 → `BeginShutdown("transport-cancelled")`，保证外部取消也一定进入收口（不能只让泵循环 break）。
- `IChannel` 的 `CloseAsync` 由使用方在创建 `DefaultChannel` 时注入；标准做法是传入 `reason => { BeginShutdown(reason); return Task.CompletedTask; }`，即只做第 1 步、绝不 `await` `Completion`。
- `Attach` 必须在 `Start` 之前调用；未 `Attach` 就 `Start` 抛 `InvalidOperationException`。

字段：

- `Channel<PooledMessage> _queue`（`BoundedChannelOptions(QueueCapacity) { FullMode = Wait, SingleReader = true, SingleWriter = true }`）。
- `int _queued`（`Interlocked` 自维护）、`int _queuePaused`（0/1）、`SemaphoreSlim _resumeSignal = new SemaphoreSlim(0, 1)`。
- `SemaphoreSlim _concurrency = new SemaphoreSlim(MaxConcurrentMessages, MaxConcurrentMessages)`。
- `CancellationTokenSource _pumpCts`、`CancellationTokenSource _connectionToken`、`CancellationTokenSource _linked`（`CreateLinkedTokenSource(_pumpCts.Token, transportToken)`）。
- `FrameAccumulator _accumulator`、`bool _pendingComplete`、`long _totalReceived`。
- `Task _pumpTask`、`Task _consumerTask`。
- `int _closing`、`Task _shutdownTask`、`object _shutdownGate`。
- `AsyncLocal<CancellationToken> _ambientToken`（**每 executor 一个实例**）。

`DrainToQueue` 按设计文档 §6.2 的代码实现，注意：

- 先 `TrySplit` → `AllocateFrame`（拷贝）→ `TryWrite`。
- `TryWrite` 失败：`msg.Release()`、`SetQueuePausedOnce()`，**不 Consume**，返回 false。
- 成功：`_queued++` 必须在 `TryWrite` **之前**（`Interlocked.Increment`），失败立即 `Decrement`；`Consume(consumed)`。
- 全部拆完返回 true。

`AllocateFrame`：

```csharp
private PooledMessage AllocateFrame(ReadOnlyMemory<byte> frame)
{
    if (frame.Length >= _options.SmallFrameInlineThreshold)
    {
        var buffer = MemoryPoolManager.Rent(frame.Length);
        frame.Span.CopyTo(buffer.AsSpan());
        return new PooledMessage(new ReadOnlyMemory<byte>(buffer, 0, frame.Length), buffer, frame.Length, true);
    }
    var exact = frame.ToArray();
    return new PooledMessage(exact, exact, exact.Length, false);
}
```

`SetQueuePausedOnce()`：

```csharp
if (Interlocked.CompareExchange(ref _queuePaused, 1, 0) == 0)
{
    _stream.Pause();
    // 空窗竞态：Pause 之后消费侧可能已经腾空，立即复核一次
    if (Volatile.Read(ref _queued) <= _options.ResumeWatermark)
        TryResumeFromPause();
}
```

`TryResumeFromPause()`：`Interlocked.CompareExchange(ref _queuePaused, 0, 1) == 0` → `_stream.Resume()` + `_resumeSignal.Release()`（用 `try/catch (SemaphoreFullException)` 保证只放行一个等待者）。

`WaitQueueSpaceAsync(ct)`：`await _resumeSignal.WaitAsync(ct)`；catch `OperationCanceledException` 返回 false。

泵循环**逐行对照设计文档 §6.2**（收口一律用 `BeginShutdown` 单步入口，泵**不 await** 收口完成，避免"外部收口等泵、泵等收口"的死锁）：

1. 检查 ct。
2. `var drained = DrainToQueue(out var anyQueued)`；**无论成功失败都先 `pendingComplete |= anyQueued`**。`drained == false`（队列满）→ `WaitQueueSpaceAsync(ct)`，返回 false 则 break，否则 `continue`（**绝不在此刻调 `ReadAsync`**）。
3. 缓冲上限检查（`_accumulator.BufferedLength > effectiveMaxBufferedBytes`）→ `BeginShutdown("buffered-bytes-limit", fromPump: true)` 后 break。
4. `pendingComplete` → `SafeFireReadCompleteAsync()` 并清零（与第 8 步共用同一个私有方法，避免两处写法漂移）。
5. `ReadAsync(ct)`；`OperationCanceledException` → break；其它异常 → `RecordPumpError` + `BeginShutdown("read-error", fromPump: true)` + break。
6. `IsCanceled` → `SafeAdvanceTo(buffer.End)` + `BeginShutdown("transport-closed", fromPump: true)` + break。
7. try 之前先声明 `bool complete = false; bool anyQueued = false;`；`try { Append; complete = DrainToQueue(out anyQueued); } catch { RecordPumpError; BeginShutdown("split-error", fromPump: true); break } finally { SafeAdvanceTo(buffer.End) }`。
8. `pendingComplete |= anyQueued`；`complete && pendingComplete` → 通知并清零。
9. `IsCompleted` → `BeginShutdown("remote-eof", fromPump: true)` + break（**允许丢弃**未入队残余）。
10. `complete && !guard.Allow(Traffic())` → `BeginShutdown("guard-rejected", fromPump: true)` + break；`guard.Allow` 抛异常按"超限"处理（`RecordPumpError` 后同样走 `guard-rejected`）。

> `BeginShutdown` 只做第 1 步并启动后台收口；泵 break 后走 `finally`，因此第 2 步"外部触发时等泵返回"不会与泵互等。测试与传输层需要"彻底回收"时等 `executor.Completion`。

`SafeAdvanceTo` / `SafeFireReadCompleteAsync` / `RecordPumpError` 均为私有方法，异常只记 `Debug` / `Warn`，绝不逃出泵。泵的 `finally`：

```csharp
try { _accumulator.Reset(); } catch { }
try { _reader.Complete(); } catch (InvalidOperationException) { } catch (ObjectDisposedException) { }
```

`Start()`：`_pumpTask = Task.Run(PumpAsync)`、`_consumerTask = Task.Run(ConsumeAsync)`。

### Task 2.5 消费、并发、超时、收口

**消费循环**

```csharp
try
{
    while (await _queue.Reader.WaitToReadAsync(ct))
    {
        PooledMessage msg;
        while (_queue.Reader.TryRead(out msg))
        {
            Interlocked.Decrement(ref _queued);
            if (_queuePaused == 1 && Volatile.Read(ref _queued) <= _options.ResumeWatermark)
                TryResumeFromPause();

            if (_closing == 1) { msg.Release(); continue; }
            await _concurrency.WaitAsync(ct);
            if (_closing == 1) { _concurrency.Release(); msg.Release(); continue; }
            Task.Run(() => ExecuteMessageAsync(msg));       // 不等待
        }
    }
}
catch (OperationCanceledException) { }   // 收口取消：退出消费者，收口流程负责剩余释放
catch (ChannelClosedException) { }
finally { while (_queue.Reader.TryRead(out var leftover)) leftover.Release(); }
```

> `finally` 里的排空必须保留：`WaitToReadAsync` 抛取消时队列可能还有条目；收口第 3 步会再排一次，`Release` 幂等，不会重复归还。

`ExecuteMessageAsync`：

```csharp
var cts = CancellationTokenSource.CreateLinkedTokenSource(_connectionToken.Token);
if (_options.MessageTimeout > TimeSpan.Zero) cts.CancelAfter(_options.MessageTimeout);
var previous = _ambientToken.Value;
_ambientToken.Value = cts.Token;
try
{
    await _pipeline.FireChannelReadAsync(msg);
}
catch (OperationCanceledException) when (_closing == 0 && cts.IsCancellationRequested
                                         && !_connectionToken.IsCancellationRequested)
{
    var timeout = _options.MessageTimeout;
    await _pipeline.FireExceptionCaughtAsync(new MessageTimeoutException(_stream.ChannelId, timeout));
}
catch (OperationCanceledException) { LogHelper.Debug($"通道链消息已取消:{_stream.ChannelId}"); }
catch (Exception ex)
{
    // 兜底：链内已把普通异常转成 ExceptionCaught；能到这里说明链本身出错，仍上报链尾。
    try { await _pipeline.FireExceptionCaughtAsync(ex); } catch { }
}
finally
{
    _ambientToken.Value = previous;
    cts.Dispose();
    _concurrency.Release();
    msg.Release();
}
```

- `_closing` 用 `Volatile.Read` 读取。
- `msg.Release()` 必须在 `finally`；`CodecDecoderHandler` 已 `Release` 时这里是幂等 no-op。

**收口**：

- `BeginShutdown(reason, fromPump)` 同步且幂等：`Interlocked.CompareExchange(ref _closing, 1, 0) == 0` 时置 `_channel.MarkInactive()` 并创建 `_shutdownTask = Task.Run(() => ShutdownCoreAsync(fromPump))`；并发调用者拿到同一个 Task。公开的 `BeginShutdown(reason)` 等价 `fromPump: false`；pipeline/executor 委托它给 `IChannel.CloseAsync`、`IChannelPipeline.CloseAsync`。
- `ShutdownCoreAsync(bool fromPump)`：
  1. 取消 `_pumpCts`；**若是外部触发**（`fromPump == false`）`await _pumpTask`（泵的 finally 负责归还 accumulator/reader）；若是泵自身触发，跳过等待。
  2. `_queue.Writer.TryComplete()`。
  3. 排空队列：`TryRead` → `Release()` + `Decrement`，直到空。
  4. `CancelPending`：取消 `_connectionToken`；`DrainInFlight`：不取消，等所有在途消息结束，最多 `DrainTimeout`（用 `Task.WhenAll` 收集在途任务 + `Task.WhenAny(timeout)`）。
  5. 等 `_concurrency` 回到满额（`CancelPending` 下即取消落地）；`DrainInFlight` 超时后放弃。
  6. `_channel.MarkInactive()`；`_host?.TryRemoveChannel(_stream.ChannelId, out _)`；`_pipeline.Dispose()`（逐个 `Count--`）；`await _stream.CloseAsync(reason)`，异常吞掉。
- `_pipeline.CloseAsync` / `_channel.CloseAsync` 只调 `BeginShutdown` 并立即返回（不 await `_shutdownTask`），防自等待。
- `Dispose()`：`BeginShutdown("disposed")` 后同步等 `Completion` 最多 5s（轮询 `Task.WhenAny`，不阻塞线程池），不抛。

> 在途任务集合用 `ConcurrentDictionary<int, Task>` 记录，`ExecuteMessageAsync` 的包装任务在 `finally` 里移除。

**测试** `Tests/Runtime/`：见 Task 2.6。

### Task 2.6 运行时测试（内存传输 + 验收 3/4/5/8/13/14/15/16/17/21/22/24/25）

**测试替身** `Tests/Infrastructure/TestRawByteStream.cs`：

```csharp
public sealed class TestRawByteStream : IRawByteStream
{
    public string ChannelId { get; }
    public PipeReader Input { get { return _pipe.Reader; } }
    public readonly Pipe Pipe;                       // 测试侧写端
    public readonly BlockingCollection<SentFrame> Sent = new BlockingCollection<SentFrame>();
    public int PauseCount;
    public int ResumeCount;
    public readonly TaskCompletionSource<string> Closed =
        new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

    public TestRawByteStream(string id)
    {
        ChannelId = id;
        Pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
    {
        Sent.Add(new SentFrame(data.ToArray(), owner));
        return Task.CompletedTask;
    }
    public void Pause() { Interlocked.Increment(ref PauseCount); }
    public void Resume() { Interlocked.Increment(ref ResumeCount); }
    public Task CloseAsync(string reason) { Closed.TrySetResult(reason); return Task.CompletedTask; }
}
```

`TestChannel : IChannel`（`ConcurrentDictionary` 背衬）、`TestGuard : IChannelGuard`（阈值/恒 false）一并提供。

**用例（每个用例单独 `[TestMethod]`）**

- **背压与低水位（验收 4）**：`QueueCapacity=4`、`MaxConcurrentMessages=1`；第一条 handler 阻塞；写 10 帧；断言 `PauseCount == 1`、`_queued` 停在 4（用 `Traffic` 或内部计数暴露 `internal` 给测试，或通过 `IChannelGuard` 收到的 `QueuedMessages` 观测）；消费到 2 时 `ResumeCount == 1`；恢复后 10 帧全部处理且顺序内容正确；断言暂停期间没有新的 `ReadAsync`（用 `TestRawByteStream` 的读计数——`PipeReader` 无法计数，改为断言写端未继续被消费：写端只写一次，`Sent` 数量按已入队帧计）。
- **截断帧不丢（验收 4 后半）**：队列满时被挡下的帧恢复后必须出现。
- **慢鉴权不阻塞（验收 3）**：5 条消息延迟 200/160/120/80/40ms，断言完成顺序、峰值并发 > 1、总耗时 < 1000ms。
- **异常（验收 5）**：handler 抛错 → `ExceptionTerminalHandler` 记录 + `CloseAsync`；队列条目全部 `Release`（引用计数配平）。
- **`ChannelReadComplete`（验收 13）**：一轮 3 帧只通知 1 次、在全部入队后、3 条消息未完成时；通知 handler 延迟 50ms 不串行化消息。
- **断开策略（验收 14）**：`CancelPending` / `DrainInFlight` 两组；卡死 handler 用 `DrainTimeout=100ms`。
- **半包耗尽（验收 15）**：每次 1 字节永不成帧，`MaxBufferedBytes=1024` 到点断开；`IChannelGuard` 恒 false 同路径。
- **`IsSharable`（验收 16）**：非 sharable `AddLast` → `Remove` → 新 pipeline `AddLast` 成功；未移除复用抛。
- **`Attributes` 并发（验收 17）**：16 条并发写不同 key；关闭后清空。
- **中止/取消传播（验收 21）**：取消 transport token，500ms 内泵退出、队列与 accumulator 归零。
- **小帧分配（验收 22）**：预热后 10 万条 20B 帧 `Rent` 增量 0；`IsPooled == false`；8KB 帧走池并归还。
- **单消息超时（验收 24）**：`MessageTimeout=100ms`、`MaxConcurrentMessages=2`；一条等 token、一条 20ms 完成；断言超时消息取消、`ExceptionCaught` 到链尾、连接 `IsActive`、后一条完成；忽略 token 忙等 300ms 不强杀。
- **断开后回包（验收 20）**：进入收口后，在途 handler 再调 `ctx.WriteAsync` 必须抛 `ChannelClosedException`；该异常走 `ExceptionCaught` 到链尾，只记 `Debug`，不延长收口、不影响其它在途消息；测试断言 `Sent` 没有新增条目。
- **外部断开（验收 25）**：空闲时 `stream.CloseAsync`（模拟 `CancelPendingRead`）+ 取消 ct，500ms 内收口、引用配平；200ms 内无新 `ReadAsync`（用 `IActivityProbe` 包装 `PipeReader` 或断言泵任务已结束）。
- **内存（验收 8）**：10 万帧后 `MemoryPoolManager` 租借/归还配平。

**验证**

```bash
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Debug --filter "FullyQualifiedName~Runtime"
```

---

## 里程碑 3 — 两级编解码与标准 Handler

对应设计文档 §5.2 出站镜像、§5.3 / §5.3.1、§5.4 中间载体、§5.5 路由。

### Task 3.1 编解码实现

**文件**

- `Codecs/Envelope.cs`
- `Codecs/EnvelopeCodec.cs`

```csharp
public object Decode(ReadOnlyMemory<byte> frame)
{
    if (frame.Length < FrameConstants.P_Type)
        throw new FrameProtocolException("帧长度不足以读取 type 字节。");

    var payload = new byte[frame.Length - FrameConstants.P_Type];
    frame.Slice(FrameConstants.P_Type).CopyTo(payload);   // ReadOnlyMemory.CopyTo(Memory) 不可用，用 Span
    return new Envelope { Type = frame.Span[0], Payload = payload };
}

public void Encode(object message, IBufferWriter<byte> writer)
{
    var env = message as Envelope;
    if (env == null) throw new InvalidOperationException("EnvelopeCodec 只接受 Envelope。");
    if (env.Payload != null) writer.Write(env.Payload);
}
```

> `frame.Slice(1).CopyTo(payload)` 需要 `ReadOnlyMemory<byte>.CopyTo(Memory<byte>)`；ns2.0 的 `System.Memory` 提供该重载。若编译失败改用 `frame.Slice(1).Span.CopyTo(payload)`。

- `Codecs/JsonMessageCodec.cs`：`Decode(payload, targetType)` → `Encoding.UTF8.GetString(payload)` + `JsonConvert.DeserializeObject(text, targetType)`；`Encode(entity)` → `Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity))`。空 payload 返回 `Activator.CreateInstance(targetType)`（值类型用 default）。`targetType == null` 抛 `ArgumentNullException`。
- `Codecs/ProtobufMessageCodec.cs`：`RuntimeTypeModel.Default.Deserialize` / `Serialize`；同样返回独立 `byte[]`。
- `Codecs/RawCopyCodec.cs`：`Decode` 返回 `byte[]` 拷贝（首字节仍当 type 处理，返回 `Envelope` 更一致——按设计文档，`RawCopyCodec` 是 `ICodec`，返回拷贝后的 `Envelope { Type, Payload }` 更符合"帧 ↔ Envelope"契约）。实现为 Envelope 的拷贝版；`Encode` 写 `Payload`。

**测试** `Tests/Codecs/CodecTests.cs`：往返、type 边界 0/255、空 payload、未知 type 由上层处理、`Envelope.Payload` 与原帧不共享内存（改写原帧后 payload 不变）。

### Task 3.2 出站载体与编解码 handler

**文件**

- `Runtime/EncodedBody.cs`、`Runtime/TransportFrame.cs`（`internal`）
- `Handlers/CodecDecoderHandler.cs`
- `Handlers/CodecEncoderHandler.cs`
- `Handlers/EnvelopeUnwrapHandler.cs`
- `Handlers/EnvelopeWrapHandler.cs`
- `Handlers/FrameEncoderHandler.cs`

`CodecDecoderHandler`：

```csharp
public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
{
    var pooled = msg as PooledMessage;
    if (pooled == null) throw new InvalidOperationException("CodecDecoderHandler 的入参必须是 PooledMessage。");
    object decoded;
    try { decoded = _codec.Decode(pooled.Frame); }
    finally { pooled.Release(); }
    await ctx.FireChannelReadAsync(decoded);
}
```

`CodecEncoderHandler`：

```csharp
public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
{
    var env = msg as Envelope;
    if (env == null) throw new InvalidOperationException("CodecEncoderHandler 只接受 Envelope。");

    var writer = new PooledBufferWriter(Math.Max(256, env.Payload == null ? 0 : env.Payload.Length));
    EncodedBody encoded = null;
    try
    {
        _codec.Encode(env, writer);
        encoded = new EncodedBody(env.Type, writer.WrittenMemory, writer);
        await ctx.WriteAsync(encoded);
    }
    finally { if (encoded != null) encoded.Dispose(); }
}
```

`FrameEncoderHandler`：

```csharp
public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
{
    var encoded = msg as EncodedBody;
    if (encoded == null) throw new InvalidOperationException("FrameEncoderHandler 之前必须经过 CodecEncoderHandler。");

    var writer = new PooledBufferWriter(encoded.Body.Length + FrameConstants.P_Head);
    TransportFrame frame = null;
    try
    {
        _encoder.Encode(encoded.Body, encoded.TypeCode, writer);
        frame = new TransportFrame(writer.WrittenMemory, writer);
        await ctx.WriteAsync(frame);
    }
    catch
    {
        if (frame != null) frame.Dispose(); else writer.Dispose();
        throw;
    }
}
```

`EnvelopeWrapHandler`（出站）：实体 → `Envelope`。

```csharp
var code = _registry.ResolveCode(msg.GetType());
if (code < 0) throw new InvalidOperationException($"类型 {msg.GetType().FullName} 未在 ITypeRegistry 注册。");
var payload = _codec.Encode(msg);
await ctx.WriteAsync(new Envelope { Type = code, Payload = payload });
```

`EnvelopeUnwrapHandler`（入站）：

```csharp
var env = msg as Envelope;
if (env == null) throw new InvalidOperationException("EnvelopeUnwrapHandler 的入参必须是 Envelope。");
var target = _registry.Resolve(env.Type);
if (target == null) throw new InvalidOperationException($"type code {env.Type} 未在 ITypeRegistry 注册。");
if (env.Payload != null && env.Payload.Length > _copyThreshold)
    LogHelper.Warn($"Envelope payload 超过阈值:{ctx.ChannelId},{env.Payload.Length},{_copyThreshold}");
var entity = _codec.Decode(env.Payload ?? Array.Empty<byte>(), target);
await ctx.FireChannelReadAsync(entity);
```

**测试**：出站契约（验收 7）——`ctx.WriteAsync(entity)` 经整套 handler 后落到 `TestRawByteStream.Sent`，字节与手工 `ICodec.Encode` + `BaseSocketProtocal.WriteTo` 逐字节一致；owner 在测试里手动 `Dispose` 并断言只释放一次。

### Task 3.3 路由、鉴权、日志、链尾

**文件**

- `Handlers/AuthHandler.cs`（`IsSharable => true`，白名单 `HashSet<int>`）
- `Handlers/TokenAuthHandler.cs`（默认实现校验 `IChannel.GetAttribute("token")`，可 override；作为可替换示例）
- `Handlers/MessageDispatcher.cs`
- `Handlers/InboundLoggingHandler.cs`
- `Handlers/OutboundLoggingHandler.cs`
- `Handlers/ExceptionTerminalHandler.cs`
- `Handlers/FramePassthroughHandler.cs`：入站收到 `PooledMessage` 后 `Retain()`，把原对象下传给旁路分支，由旁路自己的尾部 handler `Release()` 这份 Retain；它**不**与 `CodecDecoderHandler` / `EnvelopeUnwrapHandler` 同时装配（两套所有权不能并存）。executor 的 `finally` 释放的是队列条目那一份，两者互不替代。
- `Handlers/FrameSplitterHandler.cs`（仅纯链式场景）

`MessageDispatcher`：

- `Register<TMessage>(IMessageHandler)` → 内部 `Dictionary<Type, IMessageHandler>`。
- `RegisterFallback(IMessageHandler)`。
- `ChannelReadAsync`：精确查 `msg.GetType()`；未命中 → fallback；无 fallback → `Warn` + `ctx.FireChannelReadAsync(msg)`；若 `ctx.Pipeline.Handlers` 快照里**最后一个入站 handler 就是 `this`**（没有链尾）→ `Error` 并就地丢弃（消息实现 `IDisposable` 时 `Dispose`），不再下传。
- 调用 `IMessageHandler.HandleAsync(ctx, message)` 时**必须传本节点的 `ctx`**：业务在 handler 内 `ctx.WriteAsync` 时，出站要沿本节点向 head 方向找到链首出站前缀；另建一个指向链头的 context 会跳过错位。
- 返回 `this` 便于示例链式（注意接口签名，`Register` 在具体类上返回 `MessageDispatcher`）。

`ExceptionTerminalHandler`：

- `ChannelExceptionCaughtAsync`：`MessageTimeoutException` → `Warn` 返回；`ChannelClosedException` → `Debug` 返回；其它 → `Error` + `ctx.Channel.CloseAsync("handler-exception")`（吞 `ChannelClosedException`）。
- `ChannelReadAsync`：`Debug` + `IDisposable.Dispose()`，不关连接。
- 不 override `ChannelReadCompleteAsync`。

`InboundLoggingHandler`：`Stopwatch` 包住 `await ctx.FireChannelReadAsync(msg)`；`Debug` 记录 `ChannelId`、`msg.GetType().Name`、耗时。
`OutboundLoggingHandler`：在 `FrameEncoderHandler` 的 tail 一侧；包住下游出站；记录 `EncodedBody.TypeCode`、`Body.Length`、耗时。

**标准装配（写进演示工程与 README，顺序即约束）**：

```csharp
var pipeline = ChannelPipeline.Create(options);              // 具体类型，供后续 Attach
pipeline.AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()));
pipeline.AddLast(new OutboundLoggingHandler());
pipeline.AddLast(new CodecEncoderHandler(new EnvelopeCodec()));
pipeline.AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()));
// 以下为入站：分帧由泵完成，链上没有 FrameSplitterHandler
pipeline.AddLast(new CodecDecoderHandler(new EnvelopeCodec()));
pipeline.AddLast(new AuthHandler(new[] { 1, 2 }));
pipeline.AddLast(new EnvelopeUnwrapHandler(registry, new JsonMessageCodec()));
pipeline.AddLast(new TokenAuthHandler());
pipeline.AddLast(new InboundLoggingHandler());
pipeline.AddLast(new MessageDispatcher()
    .Register<LoginRequest>(new LoginHandler())
    .Register<Heartbeat>(new HeartbeatHandler()));
pipeline.AddLast(new ExceptionTerminalHandler());
```

- 前 4 个必须是连续的出站前缀；任何出站 handler 落在入站 handler 之后都会让出站事件收不到消息，由 `AddLast` 的结构校验直接抛错。
- `.Register<...>()` 返回 `MessageDispatcher`，所以它能和其它 `AddLast` 混写；`AddLast` 自身返回 `IChannelPipeline`，不要在同一条表达式里链到最后还指望拿到 `ChannelPipeline`。

**测试** `Tests/Handlers/`：

- 路由命中/未命中/fallback（验收 23）。
- 鉴权在反序列化之前（验收 19）：未认证 + 非白名单 type → `EnvelopeUnwrapHandler` 计数 0、连接关闭；白名单 → 放行。
- 未知 type code → `InvalidOperationException` → 链尾记录 + 断开。
- `Envelope.Payload` 不持有池内存（验收 18）：解码后强制归还全部 `PooledMessage`（池被覆写）后 payload 内容仍正确。
- 出站未注册 CLR 类型 → `InvalidOperationException`。
- 双重 `ctx.WriteAsync` 的坏 handler 在 Debug 下被检出（验收 12①）。

---

## 里程碑 4 — `SAEA.Sockets` 接入与演示

### Task 4.1 钩子类型与 `SocketOptionBuilder`

**文件**

- 新增 `Src/SAEA.Sockets/IReceiveFlowControl.cs`（设计文档 §6.1 原文）。
- 新增 `Src/SAEA.Sockets/IPipelineSocketOption.cs`：

```csharp
public interface IPipelineSocketOption
{
    Action<ChannelInfo> ConnectionOpened { get; }
    Func<IUserToken, System.IO.Pipelines.PipeReader, IReceiveFlowControl, CancellationToken, Task> InputHandler { get; }
}
```

- `Model/SocketOption.cs` 实现 `IPipelineSocketOption`，新增两个可空属性。
- `SocketOptionBuilder`：

```csharp
public SocketOptionBuilder UseConnectionOpened(Action<ChannelInfo> callback)
{
    _socketOption.ConnectionOpened = callback;
    return this;
}

public SocketOptionBuilder UseInputHandler(
    Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task> handler)
{
    _socketOption.InputHandler = handler;
    return this;
}
```

- `Build()` 校验（在返回前抛）：
  - `InputHandler != null && !UseStream` → `InvalidOperationException`。
  - `ConnectionOpened != null && !UseStream` → 同样抛。
  - `InputHandler != null && ConnectionOpened == null` → `InvalidOperationException`（两者必须成对）。
  - 只配 `ConnectionOpened` 合法。
- 客户端钩子拒绝：在 `SaeaClientSocket` 构造函数里，`socketOption is IPipelineSocketOption opt && (opt.InputHandler != null || opt.ConnectionOpened != null)` → `NotSupportedException`。

**测试**（在 `SAEA.Sockets.Pipeline.Tests` 里，引用 `SAEA.Sockets`），覆盖验收 10：

- ① 配 `InputHandler` 但未 `UseStream()` → `Build()` 抛 `InvalidOperationException`。
- ① 只配 `ConnectionOpened` 但未 `UseStream()` → `Build()` 抛。
- ② 只配 `ConnectionOpened` + `UseStream()` → `Build()` 成功（合法组合）。
- ③ 配 `InputHandler` 但没配 `ConnectionOpened` → `Build()` 抛（必须成对）。
- 客户端配任一钩子 → `new SaeaClientSocket(...)` 抛 `NotSupportedException`。

### Task 4.2 `SaeaServerSocket` 挂接点

**文件**：`Src/SAEA.Sockets/Core/Tcp/SaeaServerSocket.cs`

1. 字段：`private readonly Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task> _inputHandler;`、`private readonly Action<ChannelInfo> _connectionOpened;`、`private readonly ConcurrentDictionary<string, CancellationTokenSource> _pipelineCts = new ...`。
2. 构造函数从 `socketOption as IPipelineSocketOption` 取值（`SocketOption` 已被 builder 填入）。
3. `RaiseAccepted` 改造（保持既有语义）：

```csharp
private void RaiseAccepted(IUserToken userToken, SaeaConnection connection)
{
    var handler = OnAccepted;

    if (_useStream && connection != null)
    {
        if (_connectionOpened == null && handler == null) return;

        var channel = _channelManager.Set(userToken.ID, userToken.Socket, connection.Stream, connection.Tls);
        channel.UserToken = userToken;

        _connectionOpened?.Invoke(channel);   // 即使没有 OnAccepted 也要执行
        handler?.Invoke(channel);
    }
    else
    {
        handler?.Invoke(userToken);
    }
}
```

4. `ProcessAccepted` 两处调用点加显式 `try/catch` + 启动泵：

```csharp
// 普通路径
try
{
    RaiseAccepted(userToken, connection);
    StartPipelineIfNeeded(userToken, connection);
}
catch (Exception ex)
{
    OnError?.Invoke(userToken.ID, ex);
    Disconnect(userToken, ex);
    return;
}

// TLS 续体
RaiseAccepted(userToken, connection);
StartPipelineIfNeeded(userToken, connection);
// 同样包 try/catch
```

5. `StartPipelineIfNeeded`：

```csharp
private void StartPipelineIfNeeded(IUserToken userToken, SaeaConnection connection)
{
    if (_inputHandler == null || connection == null) return;

    var cts = new CancellationTokenSource();
    _pipelineCts[userToken.ID] = cts;
    var flow = new ReceiveFlowControl(connection, () => Disconnect(userToken));
    var handler = _inputHandler;

    _ = Task.Run(async () =>
    {
        try { await handler(userToken, connection.Input, flow, cts.Token).ConfigureAwait(false); }
        catch (Exception ex) { OnError?.Invoke(userToken.ID, ex); }
        finally
        {
            CancellationTokenSource removed;
            _pipelineCts.TryRemove(userToken.ID, out removed);
            removed?.Dispose();
        }
    });
}
```

6. `StartSpanPump` 启动条件加 `&& _inputHandler == null`。
7. `Disconnect(IUserToken)` / `Stop()` 取消 `_pipelineCts` 里的 token（`TryRemove` + `Cancel`），**在 `connection.Dispose()` 之前**取消，保证泵先收到取消并走收口。

`ReceiveFlowControl`（`Src/SAEA.Sockets/Core/ReceiveFlowControl.cs`）：`Pause` → `connection.PauseApplication()`；`Resume` → `connection.ResumeApplication()`；`CloseAsync` → `onClose()` + `Task.CompletedTask`；`IsPaused` 读 `connection.IsApplicationPaused`。

### Task 4.3 双来源闸门

**文件**：`Src/SAEA.Sockets/Core/SaeaConnection.cs`

```csharp
private volatile bool _receivePaused;   // Pipe 写端背压（既有）
private volatile bool _appPaused;       // 应用级暂停（新增）

public bool IsReceivePaused { get { return _receivePaused || _appPaused; } }
public bool IsApplicationPaused { get { return _appPaused; } }

public void PauseApplication() { _appPaused = true; }

public void ResumeApplication()
{
    _appPaused = false;
    if (!_receivePaused)
    {
        try { _receiveResume?.Invoke(); } catch { }
    }
}
```

- `PauseReceive` 的续体保持原样（只清 `_receivePaused`）；`ResumeReceive` 的 `if (connection.IsReceivePaused) return;` 自动覆盖两个来源。

**回归**：普通流、TLS、Pipe 背压三条路径（验收 9 + §8.3）。

### Task 4.4 演示工程端到端

**文件**（`Src/SAEA.Sockets.PipelineTest/`）

- `SocketStreamAdapter.cs`：`IRawByteStream` 实现，构造 `(string channelId, PipeReader input, IReceiveFlowControl flow, IServerSocket server)`。
  - `SendAsync`：`try { _server.SendAsync(_channelId, data, owner); return Task.CompletedTask; } catch (Exception ex) { throw new ChannelClosedException(...); }`。**必须把 owner 传下去**，不能在这里 `Dispose`。
  - `Pause/Resume` → `_flow`；`CloseAsync` → `_flow.CloseAsync(reason)`。
- `PipelineHost.cs`：`IPipelineHost` 实现（`ConcurrentDictionary<string, IChannel>`）。
- `EchoHandlers.cs` + `Messages.cs`：`LoginRequest` / `LoginResponse` / `Heartbeat`，`LoginHandler` / `HeartbeatHandler`。
- `Program.cs`：
  - `--all`：依次跑内存回环、半包/粘包、慢鉴权、背压、出站回包、超时、断开策略。
  - `--socket`：真实 `SaeaServerSocket` + `.UseStream().UseConnectionOpened(...).UseInputHandler(...)`，客户端用 `Socket` 直连发帧、校验回包。
  - 不带参数打印用法并退出 0（自动化场景永远显式传参）。

验收覆盖：验收 1、2、11 由演示 + 单测共同覆盖；验收 10①③ 在单测。

### Task 4.5 里程碑 4 验证

```bash
dotnet build Src/SAEA.Sockets.sln -c Debug
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Debug
dotnet run --project Src/SAEA.Sockets.PipelineTest -c Debug -- --all
dotnet run --project Src/SAEA.P2PTest -c Release -- --all
dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --functional
```

---

## 里程碑 5 — 文档、版本、全量回归

### Task 5.1 README 双语

`Src/SAEA.Sockets.Pipeline/README.md` + `README.en.md` 必须包含：

1. 线格式不变量（8B 小端 **body 长度** + 1B type + body）。
2. 标准装配顺序（出站前缀在链首、入站在后）。
3. `IRawByteStream` 适配器全文（可直接复制到使用方）。
4. `PooledMessage` / `Envelope.Payload` 所有权规则。
5. `Attach` 前不能 `WriteAsync`；`CloseAsync` 只表示"进入收口"。
6. 禁止 `GetStream()` / `ChannelInfo.Stream.ReadAsync` 与泵抢 reader。
7. `UseInputHandler` + `UseConnectionOpened` 成对、必须 `.UseStream()`。
8. 每帧 2 次入站拷贝 / 3 次出站拷贝的已知成本。
9. `MaxBufferedBytes` / `MaxFrameLength` 的推导与校验。
10. 调试断言只覆盖"同一条消息写两次"，不覆盖"忘记转发"。

### Task 5.2 版本与全量回归

- 核心包与演示工程的 `Version` 统一 `26.11.5.1`。
- 文档中引用的行号重新核对（`Src/SAEA.Sockets/Base/BaseCoder.cs`、`MemoryPoolManager.cs` 等）。
- 全量回归：

```bash
dotnet build Src/SAEA.Sockets.sln -c Debug
dotnet test Src/SAEA.Common.Tests/SAEA.Common.Tests.csproj -c Release
dotnet test Src/SAEA.MQTT.Tests/SAEA.MQTT.Tests.csproj -c Release
dotnet test Src/SAEA.FTP.Tests/SAEA.FTP.Tests.csproj -c Release
dotnet test Src/SAEA.Sockets.Pipeline.Tests/SAEA.Sockets.Pipeline.Tests.csproj -c Release
dotnet run --project Src/SAEA.P2PTest -c Release -- --tls
dotnet run --project Src/SAEA.QueueSocketTest -c Release -- --functional
```

预期：sln 0 errors、warnings 不高于 93 基线；59 / 153 / 13 全绿；演示 `--all` 退出码 0。

---

## 验收用例 → 里程碑映射

| 验收 | 归属 | 主要落点 |
|---|---|---|
| 1 回环 | M3 + M4 | `EncoderHandlerTests` + 演示 |
| 2 半包/粘包 | M1 + M2 | `LengthFieldSplitterTests` + 泵测试 + 演示 |
| 3 慢鉴权不阻塞 | M2 | `ExecutorConcurrencyTests` |
| 4 背压与低水位 | M2 | `ExecutorBackpressureTests` |
| 5 异常 | M2 + M3 | `ExecutorShutdownTests` + `ExceptionTerminalTests` |
| 6 超长帧 | M1 | `LengthFieldSplitterTests` |
| 7 出站 | M3 | `OutboundPipelineTests` |
| 8 内存配平 | M2 | `ExecutorAllocationTests` |
| 9 暂停来源隔离 | M4 | `DualSourceGateTests`（需真实 `SaeaConnection` 或等价替身） |
| 10 配置校验 | M2 + M4 | `PipelineOptionsTests` + `SocketOptionBuilderTests` |
| 11 启动时序 | M4 | 演示 `--socket` + `SaeaServerSocketTests` |
| 12 出站契约 | M3 | `OutboundContractTests` |
| 13 `ChannelReadComplete` | M2 | `ReadCompleteTests` |
| 14 断开策略 | M2 | `ExecutorShutdownTests` |
| 15 半包耗尽 | M2 | `BufferedLimitTests` |
| 16 `IsSharable` | M2 | `HandlerRegistrationTests` |
| 17 `Attributes` 并发 | M2 | `DefaultChannelTests` |
| 18 Envelope 不持池内存 | M3 | `EnvelopeOwnershipTests` |
| 19 鉴权先于反序列化 | M3 | `AuthOrderTests` |
| 20 断开后回包 | M2 | `ExecutorShutdownTests` |
| 21 取消传播 | M2 | `ExecutorCancellationTests` |
| 22 小帧分配 | M2 | `ExecutorAllocationTests` |
| 23 未命中路由 | M3 | `MessageDispatcherTests` |
| 24 单消息超时 | M2 | `MessageTimeoutTests` |
| 25 外部断开 | M2 | `ExternalDisconnectTests` |

---

## 子代理分派顺序（每个 Task 一个子代理）

1. Task 0.1 → 0.2（可合并给一个子代理）
2. Task 1.1 → 1.2 → 1.3 → 1.4 → 1.5 → 1.6 → 1.7
3. Task 2.1 → 2.2 → 2.3 → 2.4 → 2.5 → 2.6
4. Task 3.1 → 3.2 → 3.3
5. Task 4.1 → 4.2 → 4.3 → 4.4 → 4.5
6. Task 5.1 → 5.2

依赖关系：Task 1.1 必须先于其它 1.x；Task 2.2 依赖 1.1 与 2.1；Task 2.4 依赖 2.2/2.3；Task 3.2 依赖 2.2 与 3.1；Task 4.x 依赖 2.x/3.x 全部完成。

每个子代理的交付报告模板：

```
Task: <编号与名称>
改动文件: <列表>
验证命令: <命令>
结果: <退出码 + 关键输出>
未完成/风险: <无 或 具体项>
```