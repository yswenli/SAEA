# SAEA

SAEA.Socket is a high-performance IOCP framework based on dotnet standard 2.0; Src contains application test scenarios such as websocket, high-performance message queue, rpc, redis driver, Mvc WebApi, single-machine million-connection high-performance message server, large file transfer, etc.. <br/>
Reference components that search for saea in nuget, or enter the command Install-Package SAEA. Sockets-Version 2.1.0 directly

SAEA.Socket是一个高性能IOCP框架，基于dotnet standard 2.0；Src中含有其应用测试场景，例如websocket、高性能消息队列、rpc、redis驱动、Mvc WebApi、单机百万连接高性能消息服务器、大文件传输等<br/>

- [x] IOCP
- [x] FileTransfer
- [x] MessageSocket
- [x] QueueSocket
- [x] MVC
- [x] RPC
- [x] Websocket
- [x] RedisDrive

引用组件，可以在nuget中搜索saea，或者直接输入命令Install-Package SAEA.Sockets -Version 2.1.0 等方式

nuget url: https://www.nuget.org/packages?q=saea

WebRedisManager is also a redis management tool based on this. See https://www.cnblogs.com/yswenli/p/9460527.html git source: https://github.com/yswenli/WebRedisManager.

WebRedisManager也是基于此的一款redis管理工具，具体可参见：https://www.cnblogs.com/yswenli/p/9460527.html git源码：https://github.com/yswenli/WebRedisManager


##Detailes

Below are screenshots of various scenario test cases.

下面是各种场景测试实例截图

------

## FileTransfer
<img src="https://github.com/yswenli/SAEA/blob/master/FileSocketTest.png?raw=true" alt="SAEA.FileSocket"/>

## QueueTest
<img src="https://github.com/yswenli/SAEA/blob/master/QueueSocketTest.png?raw=true" alt="SAEA.QueueSocket" />

## WebSocket
<img src="https://github.com/yswenli/SAEA/blob/master/WebsocketTest.png?raw=true" alt="SAEA.Websocket"/>

## RedisTest
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.RedisTest.png?raw=true" alt="SAEA.RedisSocket"/>

## redis cluster test

<img src="https://github.com/yswenli/SAEA/blob/master/redis%20cluster%20test.png?raw=true" alt="SAEA.RedisSocket.Cluster">

## SAEA.MVC WebAPI

<img src="https://raw.githubusercontent.com/yswenli/SAEA/master/SAEA.MVC.png" alt="SAEA.MVC WebAPI ">

## SAEA.MVC WebAPI Pressure test

![SAEA.MVC](https://raw.githubusercontent.com/yswenli/SAEA/master/SAEA.MVCTest.png)

![SAEA.MVC](https://raw.githubusercontent.com/yswenli/SAEA/master/SAEA.WebAPITest.png)

## SAEA.RPC Pressure test

<img src="https://github.com/yswenli/SAEA/blob/master/rpc.png?raw=true" alt="SAEA.MVC RPC Pressure test">
