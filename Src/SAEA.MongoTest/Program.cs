/****************************************************************************
*项目名称：SAEA.MongoTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MongoTest
*类 名 称：Program
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/24 15:30:21
*描述：
*=====================================================================
*修改时间：2019/5/24 15:30:21
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Mongo.Driver;
using SAEA.MongoTest.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.MongoTest
{
    static class Program
    {

        static void Main(string[] args)
        {
            Console.Title = "SAEA.MongoTest";

            var mongoUrl = new MongoUrl("mongodb://admin:admin@localhost:27017/MongoTests?authSource=admin");

            MongoClient mongoClient = new MongoClient(mongoUrl);

            var collection = mongoClient.GetDatabase("MongoTests").GetCollection<UserInfo>("UserInfo");

            var userInfo = collection.Find(b => b.ID > 0).FirstOrDefault();

            if (userInfo == null)
            {
                var ui = new UserInfo()
                {
                    ID = 1,
                    UserName = "yswenli",
                    Sex = true,
                    Birthday = DateTime.Now,
                    Score = 99.20M
                };
                collection.InsertOne(ui);

                Console.WriteLine("SAEA.Mongo.InsertOne");
            }

            var query = collection.AsQueryable();

            userInfo = query.Where(b => b.ID > 0).FirstOrDefault();

            if (userInfo != null)
            {
                Console.WriteLine($"SAEA.Mongo.Linq userInfo.UserName:{userInfo.UserName}");
            }

            Console.ReadLine();

        }
    }
}
