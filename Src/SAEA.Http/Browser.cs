/****************************************************************************
*项目名称：SAEA.Http
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Http
*类 名 称：Browser
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/26 16:40:11
*描述：
*=====================================================================
*修改时间：2019/8/26 16:40:11
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/

namespace SAEA.Http
{
    public class Browser
    {
        public string OSName
        {
            get; private set;
        }


        public string BrowserName
        {
            get; private set;
        }


        public static Browser Parse(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return null;

            var browser = new Browser();

            browser.OSName = GetOSName(userAgent);

            browser.BrowserName = GetBrowser(userAgent);

            return browser;
        }

        static string GetOSName(string userAgent)
        {
            string osName = "未知";
            if (userAgent.Contains("NT 6.4"))
            {
                osName = "Windows 10";
            }
            else if (userAgent.Contains("NT 6.3"))
            {
                osName = "Windows 8.1";
            }
            else if (userAgent.Contains("NT 6.2"))
            {
                osName = "Windows 8";
            }
            else if (userAgent.Contains("NT 6.1"))
            {
                osName = "Windows 7";
            }
            else if (userAgent.Contains("NT 6.0"))
            {
                osName = "Windows Vista/Server 2008";
            }
            else if (userAgent.Contains("NT 5.2"))
            {
                osName = "Windows Server 2003";
            }
            else if (userAgent.Contains("NT 5.1"))
            {
                osName = "Windows XP";
            }
            else if (userAgent.Contains("NT 5"))
            {
                osName = "Windows 2000";
            }
            else if (userAgent.Contains("NT 4"))
            {
                osName = "Windows NT4";
            }
            else if (userAgent.Contains("Me"))
            {
                osName = "Windows Me";
            }
            else if (userAgent.Contains("98"))
            {
                osName = "Windows 98";
            }
            else if (userAgent.Contains("95"))
            {
                osName = "Windows 95";
            }
            else if (userAgent.Contains("Mac"))
            {
                osName = "Mac";
            }
            else if (userAgent.Contains("Unix"))
            {
                osName = "UNIX";
            }
            else if (userAgent.Contains("Linux"))
            {
                osName = "Linux";
            }
            else if (userAgent.Contains("SunOS"))
            {
                osName = "SunOS";
            }
            return osName;
        }


        static string GetBrowser(string userAgent)
        {
            string browserName = "未知";

            if (userAgent.Contains("QQBrowser"))
            {
                return "QQ浏览器";
            }

            if (userAgent.Contains("MetaSr"))
            {
                return "搜狗浏览器";
            }

            if (userAgent.Contains("Maxthon"))
            {
                return "傲游浏览器";
            }

            if (userAgent.Contains("MSIE 9.0"))
            {
                if (userAgent.Contains("Tablet PC 2.0"))
                {
                    return "IE9浏览器";
                }
                else
                {
                    return "其它国产";
                }
            }
            if (userAgent.Contains("MSIE 8.0"))
            {
                return "IE8浏览器";
            }
            if (userAgent.Contains("MSIE 7.0"))
            {
                return "IE7浏览器";
            }
            if (userAgent.Contains("MSIE 6.0"))
            {
                return "IE6浏览器";
            }
            if (userAgent.Contains("Opera"))
            {
                return "Opera浏览器";
            }
            if (userAgent.Contains("Chrome"))
            {
                return "Chrome浏览器";
            }
            return browserName;
        }
    }

}
