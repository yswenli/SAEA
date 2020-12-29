using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Text;

namespace SAEA.Sockets.UdpTest
{
    class Program
    {

        static IServerSokcet _udpServer;

        static BaseUnpacker _baseUnpacker;

        static void Main(string[] args)
        {
            Console.Title = "SAEA.Sockets.UdpTest";

            //udpserver
            _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIP("127.0.0.1")
                .SetPort(39656)
                .UseIocp<BaseContext>()
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .Build());

            _udpServer.OnAccepted += UdpServer_OnAccepted;
            _udpServer.OnDisconnected += UdpServer_OnDisconnected;
            _udpServer.OnError += UdpServer_OnError;
            _udpServer.OnReceive += UdpServer_OnReceive;
            _udpServer.Start();


            //udpclient
            var bContext = new BaseContext();

            var udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIP("127.0.0.1")
                .SetPort(39656)
                .UseIocp(bContext)
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .Build());

            udpClient.OnDisconnected += UdpClient_OnDisconnected;
            udpClient.OnReceive += UdpClient_OnReceive;
            udpClient.OnError += UdpClient_OnError;
            udpClient.Connect();

            _baseUnpacker = (BaseUnpacker)bContext.Unpacker;

            var msg1 = BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes("hello udpserver"), SocketProtocalType.RequestSend);

            udpClient.SendAsync(msg1.ToBytes());

            Console.ReadLine();


            StringBuilder sb = new StringBuilder();
            var str = "UDP 是User Datagram Protocol的简称， 中文名是用户数据报协议，是OSI（Open System Interconnection，开放式系统互联） 参考模型中一种无连接的传输层协议，提供面向事务的简单不可靠信息传送服务，IETF RFC 768 [1]  是UDP的正式规范。UDP在IP报文的协议号是17。,UDP协议与TCP协议一样用于处理数据包，在OSI模型中，两者都位于传输层，处于IP协议的上一层。UDP有不提供数据包分组、组装和不能对数据包进行排序的缺点，也就是说，当报文发送之后，是无法得知其是否安全完整到达的。UDP用来支持那些需要在计算机之间传输数据的网络应用。包括网络视频会议系统在内的众多的客户/服务器模式的网络应用都需要使用UDP协议。UDP协议从问世至今已经被使用了很多年，虽然其最初的光彩已经被一些类似协议所掩盖，但即使在今天UDP仍然不失为一项非常实用和可行的网络传输层协议。,许多应用只支持UDP，如：多媒体数据流，不产生任何额外的数据，即使知道有破坏的包也不进行重发。当强调传输性能而不是传输的完整性时，如：音频和多媒体应用，UDP是最好的选择。在数据传输时间很短，以至于此前的连接过程成为整个流量主体的情况下，UDP也是一个好的选择。 [3] ,内容,UDP是OSI参考模型中一种无连接的传输层协议，它主要用于不要求分组顺序到达的传输中，分组传输顺序的检查与排序由应用层完成 [4]  ，提供面向事务的简单不可靠信息传送服务。UDP 协议基本上是IP协议与上层协议的接口。UDP协议适用端口分别运行在同一台设备上的多个应用程序。,UDP提供了无连接通信，且不对传送数据包进行可靠性保证，适合于一次传输少量数据，UDP传输的可靠性由应用层负责。常用的UDP端口号有：53（DNS）、69（TFTP）、161（SNMP），使用UDP协议包括：TFTP、SNMP、NFS、DNS、BOOTP。,UDP报文没有可靠性保证、顺序保证和流量控制字段等，可靠性较差。但是正因为UDP协议的控制选项较少，在数据传输过程中延迟小、数据传输效率高，适合对可靠性要求不高的应用程序，或者可以保障可靠性的应用程序，如DNS、TFTP、SNMP等。,功能,为了在给定的主机上能识别多个目的地址，同时允许多个应用程序在同一台主机上工作并能独立地进行数据包的发送和接收，设计用户数据报协议UDP。,UDP使用底层的互联网协议来传送报文，同IP一样提供不可靠的无连接数据包传输服务。它不提供报文到达确认、排序、及流量控制等功能。,UDP Helper可以实现对指定UDP端口广播报文的中继转发，即将指定UDP端口的广播报文转换为单播报文发送给指定的服务器，起到中继的作用。,报文格式编辑,在UDP协议层次模型中，UDP位于IP层之上。应用程序访问UDP层然后使用IP层传送数据报。IP数据包的数据部分即为UDP数据报。IP层的报头指明了源主机和目的主机地址，而UDP层的报头指明了主机上的源端口和目的端口。UDP传输的段（segment）有8个字节的报头和有效载荷字段构成。,UDP报头由4个域组成，其中每个域各占用2个字节，具体包括源端口号、目标端口号、数据报长度、校验值。,以下将对UDP数据报格式进行简要介绍，具体内容请参照RFC 768 [1]  。,端口号,UDP协议使用端口号为不同的应用保留其各自的数据传输通道。UDP和TCP协议正是采用这一机制实现对同一时刻内多项应用同时发送和接收数据的支持。数据发送一方（可以是客户端或服务器端）将UDP数据包通过源端口发送出去，而数据接收一方则通过目标端口接收数据。有的网络应用只能使用预先为其预留或注册的静态端口；而另外一些网络应用则可以使用未被注册的动态端口。因为UDP报头使用两个字节存放端口号，所以端口号的有效范围是从0到65535。一般来说，大于49151的端口号都代表动态端口。UDP端口号指定有两种方式：由管理机构指定端口和动态绑定的方式。,长度,数据报的长度是指包括报头和数据部分在内的总字节数。因为报头的长度是固定的，所以该域主要被用来计算可变长度的数据部分（又称为数据负载）。数据报的最大长度根据操作环境的不同而各异。从理论上说，包含报头在内的数据报的最大长度为65535字节。不过，一些实际应用往往会限制数据报的大小，有时会降低到8192字节。,校验值,UDP协议使用报头中的校验值来保证数据的安全。校验值首先在数据发送方通过特殊的算法计算得出，在传递到接收方之后，还需要再重新计算。如果某个数据报在传输过程中被第三方篡改或者由于线路噪音等原因受到损坏，发送和接收方的校验计算值将不会相符，由此UDP协议可以检测是否出错。这与TCP协议是不同的，后者要求必须具有校验值。 [5] ,许多链路层协议都提供错误检查，包括流行的以太网协议，也许你想知道为什么UDP也要提供检查和校验。其原因是链路层以下的协议在源端和终端之间的某些通道可能不提供错误检测。虽然UDP提供有错误检测，但检测到错误时，UDP不做错误校正，只是简单地把损坏的消息段扔掉，或者给应用程序提供警告信息。,主要特点编辑,UDP是一个无连接协议，传输数据之前源端和终端不建立连接，当它想传送时就简单地去抓取来自应用程序的数据，并尽可能快地把它扔到网络上。在发送端，UDP传送数据的速度仅仅是受应用程序生成数据的速度、计算机的能力和传输带宽的限制；在接收端，UDP把每个消息段放在队列中，应用程序每次从队列中读一个消息段。,由于传输数据不建立连接，因此也就不需要维护连接状态，包括收发状态等，因此一台服务机可同时向多个客户机传输相同的消息。,UDP信息包的标题很短，只有8个字节，相对于TCP的20个字节信息包而言UDP的额外开销很小。,吞吐量不受拥挤控制算法的调节，只受应用软件生成数据的速率、传输带宽、源端和终端主机性能的限制。,UDP是面向报文的。发送方的UDP对应用程序交下来的报文，在添加首部后就向下交付给IP层。既不拆分，也不合并，而是保留这些报文的边界，因此，应用程序需要选择合适的报文大小。,虽然UDP是一个不可靠的协议，但它是分发信息的一个理想协议。例如，在屏幕上报告股票市场、显示航空信息等等。UDP也用在路由信息协议RIP（Routing Information Protocol）中修改路由表。在这些应用场合下，如果有一个消息丢失，在几秒之后另一个新的消息就会替换它。UDP广泛用在多媒体应用中。,协议对比编辑,UDP和TCP协议的主要区别是两者在如何实现信息的可靠传递方面不同。TCP协议中包含了专门的传递保证机制，当数据接收方收到发送方传来的信息时，会自动向发送方发出确认消息；发送方只有在接收到该确认消息之后才继续传送其它信息，否则将一直等待直到收到确认信息为止。与TCP不同，UDP协议并不提供数据传送的保证机制。如果在从发送方到接收方的传递过程中出现数据包的丢失，协议本身并不能做出任何检测或提示。因此，通常人们把UDP协议称为不可靠的传输协议。,TCP 是面向连接的传输控制协议，而UDP 提供了无连接的数据报服务；TCP 具有高可靠性，确保传输数据的正确性，不出现丢失或乱序；UDP 在传输数据前不建立连接，不对数据报进行检查与修改，无须等待对方的应答，所以会出现分组丢失、重复、乱序，应用程序需要负责传输可靠性方面的所有工作；UDP 具有较好的实时性，工作效率较 TCP 协议高；UDP 段结构比 TCP 的段结构简单，因此网络开销也小。TCP 协议可以保证接收端毫无差错地接收到发送端发出的字节流，为应用程序提供可靠的通信服务。对可靠性要求高的通信系统往往使用 TCP 传输数据。";
            for (int i = 0; i < 100; i++)
            {
                sb.Append(str);
            }
            var msg2 = BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes(sb.ToString()), Model.SocketProtocalType.RequestSend);
            udpClient.SendAsync(msg2.ToBytes());

            Console.ReadLine();
        }

        private static void UdpServer_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            var userToken = (IUserToken)currentSession;
            userToken.Unpacker.Unpack(data, (msg) =>
            {
                Console.WriteLine($"udp服务器收到消息：{Encoding.UTF8.GetString(msg.Content)}");

                var msg2 = BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes("hello udpclient"), Model.SocketProtocalType.RequestSend);

                _udpServer.SendAsync(userToken.ID, msg2.ToBytes());
            });
        }

        private static void UdpServer_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"UdpServer_OnError: {ID} :" + ex.Message);
        }

        private static void UdpServer_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"UdpServer_OnDisconnected:{ID}");
        }

        private static void UdpServer_OnAccepted(object obj)
        {
            Console.WriteLine($"UdpServer_OnAccepted:{((IUserToken)obj).ID}");
        }

        private static void UdpClient_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"UdpClient_OnError {ID} :" + ex.Message);
        }

        private static void UdpClient_OnReceive(byte[] data)
        {
            _baseUnpacker.Unpack(data, (msg) =>
            {
                Console.WriteLine($"udp客户端收到消息：{Encoding.UTF8.GetString(msg.Content)}");
            });
        }

        private static void UdpClient_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"UdpClient_OnDisconnected {ID} :" + ex.Message);
        }


    }
}
