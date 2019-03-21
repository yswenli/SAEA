/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： SAEAMvcApplicationConfig
*版本号： v4.3.1.2
*唯一标识：1ed5d381-d7ce-4ea3-b8b5-c32f581ad49f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 10:55:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 10:55:31
*修改人： yswenli
*版本号： v4.3.1.2
*描述：
*
*****************************************************************************/

namespace SAEA.MVC
{
    public class SAEAMvcApplicationConfig
    {
        public string Root
        {
            get; set;
        } = "wwwroot";

        public int Port
        {
            get; set;
        } = 39654;

        public bool IsStaticsCached
        {
            get; set;
        } = true;

        public bool IsZiped
        {
            get; set;
        } = false;

        public int BufferSize
        {
            get; set;
        } = 10240;

        public int Count
        {
            get; set;
        } = 10000;


        public string DefaultPage
        {
            get; set;
        } = "index.html";

        public bool IsDebug
        {
            get; set;
        } = false;

        public SAEAMvcApplicationConfig()
        {

        }

        public static SAEAMvcApplicationConfig Default
        {
            get
            {
                return new SAEAMvcApplicationConfig();
            }
        }
    }
}
