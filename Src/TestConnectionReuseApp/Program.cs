using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TestConnectionReuseApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("测试HTTP连接复用功能...");

            try
            {
                // 创建TCP连接
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", 28080);
                    Console.WriteLine("成功连接到服务器");

                    NetworkStream stream = client.GetStream();

                    // 发送第一个请求
                    string request1 = "GET /Home/Index HTTP/1.1\r\nHost: 127.0.0.1:28080\r\nConnection: keep-alive\r\n\r\n";
                    byte[] request1Bytes = Encoding.UTF8.GetBytes(request1);
                    Console.WriteLine($"发送第一个请求:\n{request1}");
                    await stream.WriteAsync(request1Bytes, 0, request1Bytes.Length);

                    // 读取第一个响应
                    string response1 = await ReadResponse(stream);
                    Console.WriteLine($"收到第一个响应:\n{response1}");

                    // 发送第二个请求
                    string request2 = "GET /Home/About HTTP/1.1\r\nHost: 127.0.0.1:28080\r\nConnection: keep-alive\r\n\r\n";
                    byte[] request2Bytes = Encoding.UTF8.GetBytes(request2);
                    Console.WriteLine($"发送第二个请求:\n{request2}");
                    await stream.WriteAsync(request2Bytes, 0, request2Bytes.Length);

                    // 读取第二个响应
                    string response2 = await ReadResponse(stream);
                    Console.WriteLine($"收到第二个响应:\n{response2}");

                    Console.WriteLine("\n测试完成: 成功复用连接!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试失败: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }

        static async Task<string> ReadResponse(NetworkStream stream)
        {
            Console.WriteLine("开始读取响应...");
            var headerBuilder = new StringBuilder();
            var buffer = new byte[1024];
            int bytesRead;

            try
            {
                // 读取响应头
                Console.WriteLine("读取响应头...");
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    Console.WriteLine($"读取到 {bytesRead} 字节的数据");
                    var responseData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    headerBuilder.Append(responseData);
                    Console.WriteLine($"当前读取到的数据:\n{responseData}");

                    // 检查是否读取到响应头的结束标记（两个连续的换行符）
                    if (headerBuilder.ToString().Contains("\r\n\r\n"))
                    {
                        Console.WriteLine("读取到响应头结束标记");
                        break;
                    }
                }

                var header = headerBuilder.ToString();
                Console.WriteLine($"完整响应头:\n{header}");
                var contentLength = 0;
                var contentLengthStart = header.IndexOf("Content-Length:");

                if (contentLengthStart != -1)
                {
                    var contentLengthEnd = header.IndexOf("\r\n", contentLengthStart);
                    var contentLengthValue = header.Substring(contentLengthStart + 15, contentLengthEnd - (contentLengthStart + 15)).Trim();
                    contentLength = int.Parse(contentLengthValue);
                    Console.WriteLine($"Content-Length: {contentLength}");
                }
                else
                {
                    Console.WriteLine("未找到Content-Length头");
                }

                // 读取响应体
                var bodyBuilder = new StringBuilder();
                var totalBytesRead = 0;

                Console.WriteLine("开始读取响应体...");
                while (totalBytesRead < contentLength)
                {
                    var bytesToRead = Math.Min(buffer.Length, contentLength - totalBytesRead);
                    Console.WriteLine($"准备读取 {bytesToRead} 字节");
                    
                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);
                        Console.WriteLine($"读取到 {bytesRead} 字节的响应体数据");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"读取响应体时出错: {ex.GetType().Name} - {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                        throw;
                    }
                    
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("连接关闭，没有更多数据");
                        break;
                    }
                    
                    totalBytesRead += bytesRead;
                    bodyBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    Console.WriteLine($"累计读取响应体 {totalBytesRead}/{contentLength} 字节");
                }

                var body = bodyBuilder.ToString();
                Console.WriteLine($"完整响应体:\n{body}");
                return header + body;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取响应时出错: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }
    }
}