using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Newtonsoft;
using IEMWorks.Log;
using IEMWorks.Util;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace RedisClient
{
   
    public static class Logger
    {
        public static void Log(this Exception ex, string method)
        {     
               
            LogCom.WriteErrLog("Method:"+method ,ex);
           
        }

      

        public static void LogOperation(string operationName, object detail = null)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (detail != null)
                        LogCom.WriteInfoLog(string.Format("Operation Method: {0}: ", operationName), Newtonsoft.Json.JsonConvert.SerializeObject( detail));
                    else
                        LogCom.WriteInfoLog(string.Format("Operation Method: {0}: ", operationName));

                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex); 
                }
            });

        
        }

       
       
         

     
 
       
    }
}
