/****************************************************************************
*项目名称：SAEA.MVCTest.Entities
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Entities
*类 名 称：ServerConfigLog
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/12 13:49:15
*描述：
*=====================================================================
*修改时间：2021/3/12 13:49:15
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using WEF;
using WEF.Common;
using WEF.MvcPager;
using WEF.Section;

namespace SAEA.MVCTest.Entities
{
    /// <summary>
    /// 实体类ServerConfigLog
    /// </summary>
    [Serializable, DataContract, Table("ServerConfigLog")]
	public partial class ServerConfigLog : Entity
	{
		private static string m_tableName;
		public ServerConfigLog() : base("ServerConfigLog") { m_tableName = "ServerConfigLog"; }
		public ServerConfigLog(string tableName) : base(tableName) { m_tableName = tableName; }

		#region Model
		private int _ID;
		private int? _EnvironmentalIdent;
		private string _ServerName;
		private string _ClientIP;
		private string _ConfigName;
		private string _ConfigValue;
		private int? _State;
		private DateTime? _CreateTime;
		private int? _UserID;
		private string _UserName;
		private DateTime? _SortTime;
		private string _ConfigType;
		private string _Category;
		private string _Describe;
		private string _DataCenter;
		/// <summary>
		/// ID 
		/// </summary>
		[DataMember]
		public int ID
		{
			get { return _ID; }
			set
			{
				this.OnPropertyValueChange(_.ID, _ID, value);
				this._ID = value;
			}
		}
		/// <summary>
		/// EnvironmentalIdent 
		/// </summary>
		[DataMember]
		public int? EnvironmentalIdent
		{
			get { return _EnvironmentalIdent; }
			set
			{
				this.OnPropertyValueChange(_.EnvironmentalIdent, _EnvironmentalIdent, value);
				this._EnvironmentalIdent = value;
			}
		}
		/// <summary>
		/// ServerName 
		/// </summary>
		[DataMember]
		public string ServerName
		{
			get { return _ServerName; }
			set
			{
				this.OnPropertyValueChange(_.ServerName, _ServerName, value);
				this._ServerName = value;
			}
		}
		/// <summary>
		/// ClientIP 
		/// </summary>
		[DataMember]
		public string ClientIP
		{
			get { return _ClientIP; }
			set
			{
				this.OnPropertyValueChange(_.ClientIP, _ClientIP, value);
				this._ClientIP = value;
			}
		}
		/// <summary>
		/// ConfigName 
		/// </summary>
		[DataMember]
		public string ConfigName
		{
			get { return _ConfigName; }
			set
			{
				this.OnPropertyValueChange(_.ConfigName, _ConfigName, value);
				this._ConfigName = value;
			}
		}
		/// <summary>
		/// ConfigValue 
		/// </summary>
		[DataMember]
		public string ConfigValue
		{
			get { return _ConfigValue; }
			set
			{
				this.OnPropertyValueChange(_.ConfigValue, _ConfigValue, value);
				this._ConfigValue = value;
			}
		}
		/// <summary>
		/// State 
		/// </summary>
		[DataMember]
		public int? State
		{
			get { return _State; }
			set
			{
				this.OnPropertyValueChange(_.State, _State, value);
				this._State = value;
			}
		}
		/// <summary>
		/// CreateTime 
		/// </summary>
		[DataMember]
		public DateTime? CreateTime
		{
			get { return _CreateTime; }
			set
			{
				this.OnPropertyValueChange(_.CreateTime, _CreateTime, value);
				this._CreateTime = value;
			}
		}
		/// <summary>
		/// UserID 
		/// </summary>
		[DataMember]
		public int? UserID
		{
			get { return _UserID; }
			set
			{
				this.OnPropertyValueChange(_.UserID, _UserID, value);
				this._UserID = value;
			}
		}
		/// <summary>
		/// UserName 
		/// </summary>
		[DataMember]
		public string UserName
		{
			get { return _UserName; }
			set
			{
				this.OnPropertyValueChange(_.UserName, _UserName, value);
				this._UserName = value;
			}
		}
		/// <summary>
		/// SortTime 
		/// </summary>
		[DataMember]
		public DateTime? SortTime
		{
			get { return _SortTime; }
			set
			{
				this.OnPropertyValueChange(_.SortTime, _SortTime, value);
				this._SortTime = value;
			}
		}
		/// <summary>
		/// ConfigType 
		/// </summary>
		[DataMember]
		public string ConfigType
		{
			get { return _ConfigType; }
			set
			{
				this.OnPropertyValueChange(_.ConfigType, _ConfigType, value);
				this._ConfigType = value;
			}
		}
		/// <summary>
		/// Category 
		/// </summary>
		[DataMember]
		public string Category
		{
			get { return _Category; }
			set
			{
				this.OnPropertyValueChange(_.Category, _Category, value);
				this._Category = value;
			}
		}
		/// <summary>
		/// Describe 
		/// </summary>
		[DataMember]
		public string Describe
		{
			get { return _Describe; }
			set
			{
				this.OnPropertyValueChange(_.Describe, _Describe, value);
				this._Describe = value;
			}
		}
		/// <summary>
		/// DataCenter 
		/// </summary>
		[DataMember]
		public string DataCenter
		{
			get { return _DataCenter; }
			set
			{
				this.OnPropertyValueChange(_.DataCenter, _DataCenter, value);
				this._DataCenter = value;
			}
		}
		#endregion

		#region Method
		/// <summary>
		/// 获取实体中的标识列
		/// </summary>
		public override Field GetIdentityField()
		{
			return _.ID;
		}
		/// <summary>
		/// 获取实体中的主键列
		/// </summary>
		public override Field[] GetPrimaryKeyFields()
		{
			return new Field[] {
				_.ID};
		}
		/// <summary>
		/// 获取列信息
		/// </summary>
		public override Field[] GetFields()
		{
			return new Field[] {
				_.ID,
				_.EnvironmentalIdent,
				_.ServerName,
				_.ClientIP,
				_.ConfigName,
				_.ConfigValue,
				_.State,
				_.CreateTime,
				_.UserID,
				_.UserName,
				_.SortTime,
				_.ConfigType,
				_.Category,
				_.Describe,
				_.DataCenter};
		}
		/// <summary>
		/// 获取值信息
		/// </summary>
		public override object[] GetValues()
		{
			return new object[] {
				this._ID,
				this._EnvironmentalIdent,
				this._ServerName,
				this._ClientIP,
				this._ConfigName,
				this._ConfigValue,
				this._State,
				this._CreateTime,
				this._UserID,
				this._UserName,
				this._SortTime,
				this._ConfigType,
				this._Category,
				this._Describe,
				this._DataCenter};
		}
		#endregion

		#region _Field
		/// <summary>
		/// 字段信息
		/// </summary>
		public class _
		{
			/// <summary>
			/// ServerConfigLog 
			/// </summary>
			public readonly static Field All = new Field("*", m_tableName);
			/// <summary>
			/// ID 
			/// </summary>
			public readonly static Field ID = new Field("ID", m_tableName, "ID");
			/// <summary>
			/// EnvironmentalIdent 
			/// </summary>
			public readonly static Field EnvironmentalIdent = new Field("EnvironmentalIdent", m_tableName, "EnvironmentalIdent");
			/// <summary>
			/// ServerName 
			/// </summary>
			public readonly static Field ServerName = new Field("ServerName", m_tableName, "ServerName");
			/// <summary>
			/// ClientIP 
			/// </summary>
			public readonly static Field ClientIP = new Field("ClientIP", m_tableName, "ClientIP");
			/// <summary>
			/// ConfigName 
			/// </summary>
			public readonly static Field ConfigName = new Field("ConfigName", m_tableName, "ConfigName");
			/// <summary>
			/// ConfigValue 
			/// </summary>
			public readonly static Field ConfigValue = new Field("ConfigValue", m_tableName, "ConfigValue");
			/// <summary>
			/// State 
			/// </summary>
			public readonly static Field State = new Field("State", m_tableName, "State");
			/// <summary>
			/// CreateTime 
			/// </summary>
			public readonly static Field CreateTime = new Field("CreateTime", m_tableName, "CreateTime");
			/// <summary>
			/// UserID 
			/// </summary>
			public readonly static Field UserID = new Field("UserID", m_tableName, "UserID");
			/// <summary>
			/// UserName 
			/// </summary>
			public readonly static Field UserName = new Field("UserName", m_tableName, "UserName");
			/// <summary>
			/// SortTime 
			/// </summary>
			public readonly static Field SortTime = new Field("SortTime", m_tableName, "SortTime");
			/// <summary>
			/// ConfigType 
			/// </summary>
			public readonly static Field ConfigType = new Field("ConfigType", m_tableName, "ConfigType");
			/// <summary>
			/// Category 
			/// </summary>
			public readonly static Field Category = new Field("Category", m_tableName, "Category");
			/// <summary>
			/// Describe 
			/// </summary>
			public readonly static Field Describe = new Field("Describe", m_tableName, "Describe");
			/// <summary>
			/// DataCenter 
			/// </summary>
			public readonly static Field DataCenter = new Field("DataCenter", m_tableName, "DataCenter");
		}
		#endregion


	}
	/// <summary>
	/// 实体类ServerConfigLog操作类
	/// </summary>
	public partial class ServerConfigLogRepository : IRepository<ServerConfigLog>
	{
		DBContext db;
		/// <summary>
		/// 构造方法
		/// </summary>
		public ServerConfigLogRepository()
		{
			db = new DBContext();
		}
		/// <summary>
		/// 构造方法
		/// </summary>
		public ServerConfigLogRepository(DBContext dbContext)
		{
			db = dbContext;
		}
		/// <summary>
		/// 构造方法
		/// <param name="connStrName">连接字符串中的名称</param>
		/// </summary>
		public ServerConfigLogRepository(string connStrName)
		{
			db = new DBContext(connStrName);
		}
		/// <summary>
		/// 构造方法
		/// <param name="dbType">数据库类型</param>
		/// <param name="connStr">连接字符串</param>
		/// </summary>
		public ServerConfigLogRepository(DatabaseType dbType, string connStr)
		{
			db = new DBContext(dbType, connStr);
		}
		/// <summary>
		/// 当前db操作上下文
		/// </summary>
		public DBContext DBContext
		{
			get
			{
				return db;
			}
		}
		/// <summary>
		/// 总数
		/// </summary>
		/// <returns></returns>
		public int Total
		{
			get
			{
				return Search().Count();
			}
		}
		/// <summary>
		/// 当前实体查询上下文
		/// </summary>
		public ISearch<ServerConfigLog> Search(string tableName = "")
		{
			if (string.IsNullOrEmpty(tableName))
			{
				tableName = "ServerConfigLog";
			}
			return db.Search<ServerConfigLog>(tableName);
		}
		/// <summary>
		/// 当前实体查询上下文
		/// </summary>
		public ISearch<ServerConfigLog> Search(ServerConfigLog entity)
		{
			return db.Search<ServerConfigLog>(entity);
		}
		/// <summary>
		/// 获取实体
		/// <param name="ID">ID</param>
		/// <param name="tableName">表名</param>
		/// </summary>
		/// <returns></returns>
		public ServerConfigLog GetServerConfigLog(int ID, string tableName = "")
		{
			return Search(tableName).Where(b => b.ID == ID).First();
		}
		/// <summary>
		/// 获取列表
		/// <param name="pageIndex">分页第几页</param>
		/// <param name="pageSize">分页一页取值</param>
		/// </summary>
		/// <returns></returns>
		public List<ServerConfigLog> GetList(int pageIndex, int pageSize)
		{
			return this.Search().Page(pageIndex, pageSize).ToList();
		}
		/// <summary>
		/// 获取列表
		/// <param name="tableName">表名</param>
		/// <param name="pageIndex">分页第几页</param>
		/// <param name="pageSize">分页一页取值</param>
		/// </summary>
		/// <returns></returns>
		public List<ServerConfigLog> GetList(string tableName, int pageIndex = 1, int pageSize = 12)
		{
			return this.Search(tableName).Page(pageIndex, pageSize).ToList();
		}
		/// <summary>
		/// 分页查询
		/// <param name="lambdaWhere">查询表达式</param>
		/// <param name="pageIndex">分页第几页</param>
		/// <param name="pageSize">分页一页取值</param>
		/// <param name="orderBy">排序</param>
		/// <param name="asc">升降</param>
		/// </summary>
		/// <returns></returns>
		public PagedList<ServerConfigLog> GetPagedList(Expression<Func<ServerConfigLog, bool>> lambdaWhere, string tableName = "", int pageIndex = 1, int pageSize = 12, string orderBy = "ID", bool asc = true)
		{
			return this.Search(tableName).GetPagedList(lambdaWhere, pageIndex, pageSize, orderBy, asc);
		}
		/// <summary>
		/// 添加实体
		/// <param name="entity">传进的实体</param>
		/// </summary>
		public int Insert(ServerConfigLog entity)
		{
			return db.Insert(entity);
		}
		/// <summary>
		/// 更新实体
		/// <param name="entity">传进的实体</param>
		/// </summary>
		public int Update(ServerConfigLog entity)
		{
			return db.Update(entity);
		}
		/// <summary>
		/// 删除实体
		/// <param name="entity">传进的实体</param>
		/// </summary>
		public int Delete(ServerConfigLog entity)
		{
			return db.Delete(entity);
		}
		/// <summary>
		/// 删除实体
		/// <param name="ID">ID</param>
		/// <param name="tableName">tableName</param>
		/// </summary>
		public int Delete(int ID, string tableName = "")
		{
			var entity = Search(tableName).Where(b => b.ID == ID).First();
			if (entity == null) return -1;
			entity.Attach(EntityState.Deleted);
			return db.Save(entity);
		}
		/// <summary>
		/// 批量删除实体
		/// <param name="obj">传进的实体列表</param>
		/// </summary>
		public int Deletes(List<ServerConfigLog> entities)
		{
			return db.Delete<ServerConfigLog>(entities);
		}
		/// <summary>
		/// 持久化实体
		/// <param name="entity">传进的实体</param>
		/// </summary>
		public int Save(ServerConfigLog entity)
		{
			return db.Save<ServerConfigLog>(entity);
		}
		/// <summary>
		/// 批量持久化实体
		/// <param name="entities">传进的实体列表</param>
		/// </summary>
		public int Save(List<ServerConfigLog> entities)
		{
			return db.Save<ServerConfigLog>(entities);
		}
		/// <summary>
		/// 持久化实体
		/// <param name="tran">事务</param>
		/// <param name="entity">传进的实体</param>
		/// </summary>
		public int Save(DbTransaction tran, ServerConfigLog entity)
		{
			return db.Save<ServerConfigLog>(tran, entity);
		}
		/// <summary>
		/// 批量持久化实体
		/// <param name="tran">事务</param>
		/// <param name="entity">传进的实体列表</param>
		/// </summary>
		public int Save(DbTransaction tran, List<ServerConfigLog> entities)
		{
			return db.Save<ServerConfigLog>(tran, entities);
		}
		/// <summary>
		/// 执行sql语句
		/// <param name="sql"></param>
		/// </summary>
		public SqlSection ExecuteSQL(string sql)
		{
			return db.FromSql(sql);
		}
		/// <summary>
		/// 执行存储过程
		/// <param name="sql"></param>
		/// </summary>
		public ProcSection ExcuteProc(string procName)
		{
			return db.FromProc(procName);
		}
	}
}
