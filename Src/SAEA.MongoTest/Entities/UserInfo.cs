using SAEA.Mongo;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MongoTest.Entities
{
    public class UserInfo : MongoEntity
    {
        public int ID
        {
            get; set;
        }

        public string UserName
        {
            get; set;
        }

        public bool Sex
        {
            get; set;
        }

        public DateTime Birthday
        {
            get; set;
        }

        public decimal Score
        {
            get; set;
        }

    }
}
