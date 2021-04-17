
using SAEA.Common;
using SAEA.MVC;
using SAEA.MVC.Tool;

namespace SAEA.MVCTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.MVCTest";

            var mvcConfig = SAEAMvcApplicationConfigBuilder.Read();

            mvcConfig.Count = 2;

            mvcConfig.Port = 28080;

            mvcConfig.IsStaticsCached = true;

            //mvcConfig.ControllerNameSpace = "SAEA.MVCTest";

            SAEAMvcApplicationConfigBuilder.Write(mvcConfig);

            SAEAMvcApplication mvcApplication = new SAEAMvcApplication(mvcConfig);

            mvcApplication.OnException += MvcApplication_OnException;

            //设置默认控制器

            //mvcApplication.SetDefault("home", "index");

            //mvcApplication.SetDefault("index.html");

            //限制

            //mvcApplication.SetForbiddenAccessList("/content/");

            //mvcApplication.SetForbiddenAccessList(".jpg");

            mvcApplication.Start();

            mvcApplication.SetCrossDomainHeaders("token", "auth", "Authorization");

            mvcApplication.Restart();

            ConsoleHelper.WriteLine($"SAEA.MVCApplication 已启动！\t\r\n访问请输入http://127.0.0.1:{mvcConfig.Port}/{{controller}}/{{action}}");

            //生成sdk测试
            TestCodeGenerate1();
            TestCodeGenerate2();

            ConsoleHelper.WriteLine("回车结束！");
            ConsoleHelper.ReadLine();
        }

        private static Http.Model.IHttpResult MvcApplication_OnException(Http.Model.IHttpContext httpContext, System.Exception ex)
        {
            return new ContentResult($"已通过事件捕获发生异常，url：{httpContext?.Request.Url},ex:{ex.Message}");
        }

        /// <summary>
        /// 生成sdk代码测试
        /// </summary>
        static void TestCodeGenerate1()
        {
            ConsoleHelper.WriteLine("回车测试sdk生成");
            ConsoleHelper.ReadLine();
            APISdkCodeGenerator.Save(@"C:\Users\yswenli\Desktop", CodeType.Js);
            APISdkCodeGenerator.Save(@"C:\Users\yswenli\Desktop", CodeType.CSharp);
        }

        /// <summary>
        /// csharp sdk 测试
        /// </summary>
        static void TestCodeGenerate2()
        {
            ConsoleHelper.WriteLine("回车测试sdk的功能测试");
            ConsoleHelper.ReadLine();

            var sdk = new MVC.Tool.CodeGenerte.SaeaApiSdk("http://127.0.0.1:28080/");

            sdk.HomeGetGet("1", (data) =>
            {
                ConsoleHelper.WriteLine("TestCodeGenerate2.sdk.HomeGetGet:" + data);
            }, (e) =>
            {
                ConsoleHelper.WriteLine("TestCodeGenerate2.sdk.HomeGetGet.Error:" + e.Message);
            });


            sdk.HomeUpdatePost("true", 1, "yswenli", "yswenli", (data) =>
            {
                ConsoleHelper.WriteLine("TestCodeGenerate2.sdk.HomeUpdatePost:" + data);
            }, (e) =>
            {
                ConsoleHelper.WriteLine("TestCodeGenerate2.sdk.HomeUpdatePost.Error:" + e.Message);
            });
        }
    }
}
