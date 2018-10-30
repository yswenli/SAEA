# SAEA

SAEA.Socket is a high-performance IOCP framework based on dotnet standard 2.0; Src contains application test scenarios such as websocket, high-performance message queue, rpc, redis driver, Mvc WebApi, single-machine million-connection high-performance message server, large file transfer, etc.. <br/>
Reference components that search for saea in nuget, or enter the command Install-Package SAEA.Sockets-Version 3.2.1.1 directly

SAEA.Socket是一个高性能IOCP框架，基于dotnet standard 2.0；Src中含有其应用测试场景，例如websocket、高性能消息队列、rpc、redis驱动、Mvc WebApi、单机百万连接高性能消息服务器、大文件传输等<br/>

- [x] IOCP
- [x] FileTransfer
- [x] MessageSocket
- [x] QueueSocket
- [x] MVC
- [x] RPC
- [x] Websocket
- [x] RedisDrive

引用组件，可以在nuget中搜索saea，或者直接输入命令 
```
Install-Package SAEA.Sockets -Version 3.2.1.1
```
nuget url: https://www.nuget.org/packages?q=saea

WebRedisManager is also a redis management tool based on this. See https://www.cnblogs.com/yswenli/p/9460527.html git source: https://github.com/yswenli/WebRedisManager.

WebRedisManager也是基于此的一款redis管理工具，具体可参见：https://www.cnblogs.com/yswenli/p/9460527.html git源码：https://github.com/yswenli/WebRedisManager

GFF一款仿QQ通信程序同样基于此，具体可参见:https://github.com/yswenli/GFF 

------

## FileTransfer

### saea.filesocket usage

```csharp
var fileTransfer = new FileTransfer(filePath);
fileTransfer.OnReceiveEnd += _fileTransfer_OnReceiveEnd;
fileTransfer.OnDisplay += _fileTransfer_OnDisplay;
//send file
fileTransfer.SendFile(string fileName, string ip)
```
## QueueTest

### saea.queue server usage

```csharp
var server = new QServer();
server.Start();
```
### saea.queue producer usage

```csharp
var ipPort = "127.0.0.1:39654";
QClient producer = new QClient("productor_" + Guid.NewGuid().ToString("N"), ipPort);
producer.OnError += Producer_OnError;
producer.OnDisconnected += Client_OnDisconnected;
producer.Connect();
producer.Publish(topic, msg);
```

### saea.queue consumer usage

```csharp
var ipPort = "127.0.0.1:39654";
QClient consumer = new QClient("subscriber_" + Guid.NewGuid().ToString("N"), ipPort);
consumer.OnMessage += Subscriber_OnMessage;
consumer.OnDisconnected += Client_OnDisconnected;
consumer.Connect();
consumer.Subscribe(topic);
```

## WebSocket
### wsserver usage

```csharp
WSServer server = new WSServer();
server.OnMessage += Server_OnMessage;
server.Start();

private static void Server_OnMessage(string id, WSProtocal data)
{
    Console.WriteLine("WSServer 收到{0}的消息：{1}", ConsoleColor.Green, id, Encoding.UTF8.GetString(data.Content));
    server.Reply(id, data);
}
```
### wsclient usage

```csharp
WSClient client = new WSClient();
client.OnPong += Client_OnPong;
client.OnMessage += Client_OnMessage;
client.OnError += Client_OnError;
client.OnDisconnected += Client_OnDisconnected;
client.Connect();
client.Send("hello world!");
client.Ping();
client.Close();
```

## RedisTest

<a href="https://github.com/yswenli/WebRedisManager" target="_blank">https://github.com/yswenli/WebRedisManager</a>

### saea.redis usage

```csharp
 var cnnStr = "server=127.0.0.1:6379;passwords=yswenli";
 RedisClient redisClient = new RedisClient(cnnStr);
 redisClient.Connect();
 redisClient.GetDataBase(1).Set("key", "val");
 var val = redisClient.GetDataBase().Get("key");
```

## SAEA.MVC

<a href="https://github.com/yswenli/WebRedisManager" target="_blank">https://github.com/yswenli/WebRedisManager</a>

### saea.mvc init usage

```csharp
SAEAMvcApplication mvcApplication = new SAEAMvcApplication(root: "/html/");
//设置默认控制器
mvcApplication.SetDefault("home", "index");
mvcApplication.SetDefault("index.html");
//限制
mvcApplication.SetForbiddenAccessList("/content/");
mvcApplication.SetForbiddenAccessList(".jpg");

mvcApplication.Start();
```

### saea.mvc controller usage

```csharp
[LogAtrribute]
public class HomeController : Controller
{          
	[Log2Atrribute]
	[HttpGet]
	[HttpPost]
	public ActionResult Index()
	{
		return Content("Hello,I'm SAEA.MVC！你好！");
	}
	
	public ActionResult Show()
	{
		var response = HttpContext.Response;
		response.ContentType = "text/html; charset=utf-8";
		response.Write("<h3>测试一下那个response对象使用情况！</h3>参考消息网4月12日报道外媒称，法国一架“幻影-2000”战机意外地对本国一家工厂投下了炸弹。据俄罗斯卫星网4月12日援引法国蓝色电视台报道，事故于当地时间10日发生在卢瓦尔省，当时两架法国空军的飞机飞过韦尔尼松河畔诺让市镇上空，一枚炸弹从其中一架飞机上掉了下来，直接掉在了佛吉亚公司的工厂里。与此同时，有两人受伤。一名目击者称，“起初是两架战机飞过，然后我们都听到了物体撞击的声音，声音相当响，甚至盖过了飞过的飞机的噪音。”法国空军代表称，掉在工厂里的炸弹是演习用的，里面没有装炸药，本来是要将它投到离兰斯市不远的靶场。这名代表称事件“非常非常罕见”，目前正进行调查。");
		response.End();
		return Empty();
	}
	
	public ActionResult GetModels(string version, BasePamars basePamars, PagedPamars pagedPamars)
	{
		return Content($"version:{version}  basePamars:{Serialize(basePamars)}  pagedPamars:{Serialize(pagedPamars)}");
	}	
	
	public ActionResult Download()
	{
		return File(HttpContext.Server.MapPath("/Content/Image/c984b2fb80aeca7b15eda8c004f2e0d4.jpg"));
	}

	[HttpPost]
	public ActionResult Upload(string name)
	{
		var postFiles = HttpContext.Request.PostFiles;
		return Content($"ok！name：{name}");
	}
}
```

## SAEA.RPC
### saea.rpc service usage

```csharp
var sp = new ServiceProvider();
sp.OnErr += Sp_OnErr;
sp.Start();

[RPCService]
public class HelloService
{
	public string Hello()
	{
		return "saea.rpc hello!"
	}
}
```

### saea.rpc client usage

```csharp
var url = "rpc://127.0.0.1:39654";
RPCServiceProxy cp = new RPCServiceProxy(url);
cp.OnErr += Cp_OnErr;
cp.HelloService.Hello();
```

## SAEA.Message

### saea.message server usage

```csharp
MessageServer server = new MessageServer(1024, 1000 * 1000, 30 * 60 * 1000);

server.OnDisconnected += Server_OnDisconnected;

server.Start();
```

### saea.message client usage

```csharp
var cc1 = new MessageClient();
cc1.OnPrivateMessage += Client_OnPrivateMessage;
cc1.Connect();

//私信
cc1.SendPrivateMsg(cc2.UserToken.ID, "你好呀,cc2！");

//订阅
cc1.Subscribe(channelName);

//发送频道消息
cc1.SendChannelMsg(channelName, "hello!");

//创建群组
cc1.SendCreateGroup(groupName);

//加入群组
cc2.SendAddMember(groupName);


//发送群消息
cc1.SendGroupMessage(groupName, "群主广播了！");

//退群
cc2.SendRemoveGroup(groupName);

```

## Instance screenshot

<img src="https://github.com/yswenli/SAEA/blob/master/FileSocketTest.png?raw=true" /><br/>
<img src="https://github.com/yswenli/SAEA/blob/master/QueueSocketTest.png?raw=true" /><br/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.MVC.png?raw=true" /><br/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.MVCTest.png?raw=true" /><br/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.RedisTest.png?raw=true" /><br/>
<img src="https://github.com/yswenli/SAEA/blob/master/SAEA.WebAPITest.png?raw=true" /><br/>
<img src="https://github.com/yswenli/SAEA/blob/master/WebsocketTest.png?raw=true" /><br/>
<img src="https://github.com/yswenli/SAEA/blob/master/redis%20cluster%20test.png?raw=true" /><br/>
<img src="https://github.com/yswenli/SAEA/blob/master/rpc.png?raw=true" /><br/>
