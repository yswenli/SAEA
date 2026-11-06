using System.Runtime.CompilerServices;

// 单元测试需要直接构造 internal 类型（PooledMessage、TransportFrame 等）并观测内部计数。
[assembly: InternalsVisibleTo("SAEA.Sockets.Pipeline.Tests")]