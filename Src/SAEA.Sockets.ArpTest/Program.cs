using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

class ARPTool
{
    // 导入SendARP函数，用于发送ARP请求
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    public static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref int physicalAddrLen);

    static void Main(string[] args)
    {
        
        // 获取局域网内的IP地址列表
        List<string> ipList = GetLanIPAddressList();
        Dictionary<string, string> ipMacDict = new Dictionary<string, string>();

        // 遍历IP地址列表，获取每个IP地址对应的MAC地址
        foreach (var ipAddress in ipList)
        {
            IPAddress ip;
            if (!IPAddress.TryParse(ipAddress, out ip))
            {
                Console.WriteLine("Invalid IP Address: " + ipAddress);
                continue;
            }

            byte[] macAddr = new byte[6];
            int macAddrLen = macAddr.Length;

            // 发送ARP请求，获取MAC地址
            int result = SendARP((int)ip.Address, 0, macAddr, ref macAddrLen);

            if (result != 0)
            {
                Console.WriteLine("Error: " + result + " for IP: " + ipAddress);
                continue;
            }

            // 将MAC地址转换为字符串格式
            string macAddress = BitConverter.ToString(macAddr, 0, macAddrLen);
            ipMacDict[ipAddress] = macAddress;
        }

        // 输出IP地址和MAC地址的对应关系
        foreach (var entry in ipMacDict)
        {
            Console.WriteLine("IP Address: " + entry.Key + " - MAC Address: " + entry.Value);
        }

        // 发送ARP通知
        // 将MAC地址字符串转换成字节数组
        string myMacAddress = "D8-80-83-CC-6A-FB";
        byte[] myMacBytes = new byte[6];
        string[] macAddressParts = null;
        if (myMacAddress.IndexOf(":") > -1)
        {
            macAddressParts = myMacAddress.Split(':');
        }
        if (myMacAddress.IndexOf("-") > -1)
        {
            macAddressParts = myMacAddress.Split('-');
        }
        if (macAddressParts == null) throw new Exception("mac address format error");
        for (int i = 0; i < myMacBytes.Length; i++)
        {
            myMacBytes[i] = byte.Parse(macAddressParts[i], System.Globalization.NumberStyles.HexNumber);
        }
        IPAddress gatewayIp = IPAddress.Parse("192.168.3.215");
        byte[] gatewayMacAddr = myMacBytes;
        int gatewayMacAddrLen = gatewayMacAddr.Length;

        var targetIpAddress = "192.168.3.80";

        int notifyResult = SendARP((int)IPAddress.Parse(targetIpAddress).Address, (int)gatewayIp.Address, gatewayMacAddr, ref gatewayMacAddrLen);

        if (notifyResult != 0)
        {
            Console.WriteLine("Error: " + notifyResult + " when notifying gateway for IP: " + targetIpAddress);
        }
        else
        {
            Console.WriteLine("Successfully notified gateway for IP: " + targetIpAddress);
        }

        Console.ReadLine();
    }

    // 获取局域网内的IP地址列表
    static List<string> GetLanIPAddressList()
    {
        List<string> ipList = new List<string>();
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus == OperationalStatus.Up)
            {
                var ipProperties = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation ip in ipProperties.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        // 判断当前地址是否是网关地址
                        if (string.IsNullOrEmpty(ipProperties.GatewayAddresses.FirstOrDefault()?.Address.ToString()))
                        {
                            continue;
                        }
                        // 获取子网掩码
                        var subnetMask = ip.IPv4Mask;
                        if (subnetMask == null)
                        {
                            continue;
                        }

                        // 计算子网中的所有可能的IP地址
                        var networkAddress = GetNetworkAddress(ip.Address, subnetMask);
                        var broadcastAddress = GetBroadcastAddress(ip.Address, subnetMask);

                        for (IPAddress current = networkAddress; current.Address <= broadcastAddress.Address; current = IncrementIPAddress(current))
                        {
                            ipList.Add(current.ToString());
                        }
                    }
                }
            }
        }
        return ipList;
    }

    // 获取网络地址
    static IPAddress GetNetworkAddress(IPAddress address, IPAddress subnetMask)
    {
        byte[] ipBytes = address.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();
        byte[] networkBytes = new byte[ipBytes.Length];

        for (int i = 0; i < ipBytes.Length; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }

        return new IPAddress(networkBytes);
    }

    // 获取广播地址
    static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        byte[] ipBytes = address.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();
        byte[] broadcastBytes = new byte[ipBytes.Length];

        for (int i = 0; i < ipBytes.Length; i++)
        {
            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(broadcastBytes);
    }

    // 增加IP地址
    static IPAddress IncrementIPAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            if (bytes[i] < 255)
            {
                bytes[i]++;
                return new IPAddress(bytes);
            }
            bytes[i] = 0;
        }
        throw new InvalidOperationException("Cannot increment the broadcast address.");
    }
}
