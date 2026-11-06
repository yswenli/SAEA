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
		/// <summary>
		/// 以默认表名 ServerConfigLog 初始化实体
		/// </summary>
		public ServerConfigLog() : base("ServerConfigLog") { m_tableName = "ServerConfigLog"; }
		/// <summary>
		/// 以指定表名初始化实体，并同步字段表达式的表前缀
		/// </summary>
		/// <param name="tableName">表名</param>
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
		/// 主键 ID
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
		/// 环境标识
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
		/// 服务器名称
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
		/// 客户端 IP
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
		/// 配置项名称
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
		/// 配置项取值
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
		/// 状态
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
		/// 创建时间
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
		/// 用户 ID
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
		/// 用户名
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
		/// 排序时间
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
		/// 配置类型
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
		/// 分类
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
		/// 描述
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
		/// 数据中心
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
		/// <returns>自增标识列 ID</returns>
		public override Field GetIdentityField()
		{
			return _.ID;
		}
		/// <summary>
		/// 获取实体中的主键列
		/// </summary>
		/// <returns>主键列集合，当前仅含 ID</returns>
		public override Field[] GetPrimaryKeyFields()
		{
			return new Field[] {
				_.ID};
		}
		/// <summary>
		/// 获取列信息
		/// </summary>
		/// <returns>实体映射的全部列，顺序与 GetValues 一致</returns>
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
		/// <returns>与 GetFields 顺序一一对应的当前列取值</returns>
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
			/// 全部列的通配字段
			/// </summary>
			public readonly static Field All = new Field("*", m_tableName);
			/// <summary>
			/// 主键 ID 列
			/// </summary>
			public readonly static Field ID = new Field("ID", m_tableName, "ID");
			/// <summary>
			/// 环境标识列
			/// </summary>
			public readonly static Field EnvironmentalIdent = new Field("EnvironmentalIdent", m_tableName, "EnvironmentalIdent");
			/// <summary>
			/// 服务器名称列
			/// </summary>
			public readonly static Field ServerName = new Field("ServerName", m_tableName, "ServerName");
			/// <summary>
			/// 客户端 IP 列
			/// </summary>
			public readonly static Field ClientIP = new Field("ClientIP", m_tableName, "ClientIP");
			/// <summary>
			/// 配置项名称列
			/// </summary>
			public readonly static Field ConfigName = new Field("ConfigName", m_tableName, "ConfigName");
			/// <summary>
			/// 配置项取值列
			/// </summary>
			public readonly static Field ConfigValue = new Field("ConfigValue", m_tableName, "ConfigValue");
			/// <summary>
			/// 状态列
			/// </summary>
			public readonly static Field State = new Field("State", m_tableName, "State");
			/// <summary>
			/// 创建时间列
			/// </summary>
			public readonly static Field CreateTime = new Field("CreateTime", m_tableName, "CreateTime");
			/// <summary>
			/// 用户 ID 列
			/// </summary>
			public readonly static Field UserID = new Field("UserID", m_tableName, "UserID");
			/// <summary>
			/// 用户名
			/// </summary>
			public readonly static Field UserName = new Field("UserName", m_tableName, "UserName");
			/// <summary>
			/// 排序时间列
			/// </summary>
			public readonly static Field SortTime = new Field("SortTime", m_tableName, "SortTime");
			/// <summary>
			/// 配置类型列
			/// </summary>
			public readonly static Field ConfigType = new Field("ConfigType", m_tableName, "ConfigType");
			/// <summary>
			/// 分类列
			/// </summary>
			public readonly static Field Category = new Field("Category", m_tableName, "Category");
			/// <summary>
			/// 描述列
			/// </summary>
			public readonly static Field Describe = new Field("Describe", m_tableName, "Describe");
			/// <summary>
			/// 数据中心列
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
		/// 使用默认配置初始化仓储
		/// </summary>
		public ServerConfigLogRepository()
		{
			db = new DBContext();
		}
		/// <summary>
		/// 使用已有的数据库上下文初始化仓储
		/// </summary>
		/// <param name="dbContext">数据库操作上下文</param>
		public ServerConfigLogRepository(DBContext dbContext)
		{
			db = dbContext;
		}
		/// <summary>
		/// 按配置中的连接字符串名称初始化仓储
		/// </summary>
		/// <param name="connStrName">配置文件中连接字符串的名称</param>
		public ServerConfigLogRepository(string connStrName)
		{
			db = new DBContext(connStrName);
		}
		/// <summary>
		/// 按数据库类型与连接字符串初始化仓储
		/// </summary>
		/// <param name="dbType">数据库类型</param>
		/// <param name="connStr">连接字符串</param>
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
		/// 获取指定表名的实体查询上下文
		/// </summary>
		/// <param name="tableName">表名，为空时使用 ServerConfigLog</param>
		/// <returns>实体查询上下文</returns>
		public ISearch<ServerConfigLog> Search(string tableName = "")
		{
			if (string.IsNullOrEmpty(tableName))
			{
				tableName = "ServerConfigLog";
			}
			return db.Search<ServerConfigLog>(tableName);
		}
		/// <summary>
		/// 获取实体所在表的查询上下文
		/// </summary>
		/// <param name="entity">携带表名信息的实体</param>
		/// <returns>实体查询上下文</returns>
		public ISearch<ServerConfigLog> Search(ServerConfigLog entity)
		{
			return db.Search<ServerConfigLog>(entity);
		}
		/// <summary>
		/// 按主键获取实体
		/// </summary>
		/// <param name="ID">主键 ID</param>
		/// <param name="tableName">表名，为空时使用默认表</param>
		/// <returns>匹配的实体，未找到时为 null</returns>
		public ServerConfigLog GetServerConfigLog(int ID, string tableName = "")
		{
			return Search(tableName).Where(b => b.ID == ID).First();
		}
		/// <summary>
		/// 按默认表分页获取列表
		/// </summary>
		/// <param name="pageIndex">页码</param>
		/// <param name="pageSize">每页记录数</param>
		/// <returns>当前页的实体列表</returns>
		public List<ServerConfigLog> GetList(int pageIndex, int pageSize)
		{
			return this.Search().Page(pageIndex, pageSize).ToList();
		}
		/// <summary>
		/// 按指定表分页获取列表
		/// </summary>
		/// <param name="tableName">表名</param>
		/// <param name="pageIndex">页码，默认 1</param>
		/// <param name="pageSize">每页记录数，默认 12</param>
		/// <returns>当前页的实体列表</returns>
		public List<ServerConfigLog> GetList(string tableName, int pageIndex = 1, int pageSize = 12)
		{
			return this.Search(tableName).Page(pageIndex, pageSize).ToList();
		}
		/// <summary>
		/// 按条件分页查询
		/// </summary>
		/// <param name="lambdaWhere">查询表达式</param>
		/// <param name="tableName">表名，为空时使用默认表</param>
		/// <param name="pageIndex">页码，默认 1</param>
		/// <param name="pageSize">每页记录数，默认 12</param>
		/// <param name="orderBy">排序字段，默认 ID</param>
		/// <param name="asc">是否升序，默认 true</param>
		/// <returns>分页结果，含当前页数据与总数</returns>
		public PagedList<ServerConfigLog> GetPagedList(Expression<Func<ServerConfigLog, bool>> lambdaWhere, string tableName = "", int pageIndex = 1, int pageSize = 12, string orderBy = "ID", bool asc = true)
		{
			return this.Search(tableName).GetPagedList(lambdaWhere, pageIndex, pageSize, orderBy, asc);
		}
		/// <summary>
		/// 插入实体
		/// </summary>
		/// <param name="entity">待插入的实体</param>
		/// <returns>受影响的行数</returns>
		public int Insert(ServerConfigLog entity)
		{
			return db.Insert(entity);
		}
		/// <summary>
		/// 更新实体
		/// </summary>
		/// <param name="entity">待更新的实体</param>
		/// <returns>受影响的行数</returns>
		public int Update(ServerConfigLog entity)
		{
			return db.Update(entity);
		}
		/// <summary>
		/// 删除实体
		/// </summary>
		/// <param name="entity">待删除的实体</param>
		/// <returns>受影响的行数</returns>
		public int Delete(ServerConfigLog entity)
		{
			return db.Delete(entity);
		}
		/// <summary>
		/// 按主键删除实体
		/// </summary>
		/// <param name="ID">主键 ID</param>
		/// <param name="tableName">表名，为空时使用默认表</param>
		/// <returns>受影响的行数，实体不存在时返回 -1</returns>
		public int Delete(int ID, string tableName = "")
		{
			var entity = Search(tableName).Where(b => b.ID == ID).First();
			if (entity == null) return -1;
			entity.Attach(EntityState.Deleted);
			return db.Save(entity);
		}
		/// <summary>
		/// 批量删除实体
		/// </summary>
		/// <param name="entities">待删除的实体列表</param>
		/// <returns>受影响的行数</returns>
		public int Deletes(List<ServerConfigLog> entities)
		{
			return db.Delete<ServerConfigLog>(entities);
		}
		/// <summary>
		/// 持久化实体
		/// </summary>
		/// <param name="entity">待持久化的实体</param>
		/// <returns>受影响的行数</returns>
		public int Save(ServerConfigLog entity)
		{
			return db.Save<ServerConfigLog>(entity);
		}
		/// <summary>
		/// 批量持久化实体
		/// </summary>
		/// <param name="entities">待持久化的实体列表</param>
		/// <returns>受影响的行数</returns>
		public int Save(List<ServerConfigLog> entities)
		{
			return db.Save<ServerConfigLog>(entities);
		}
		/// <summary>
		/// 在指定事务中持久化实体
		/// </summary>
		/// <param name="tran">数据库事务</param>
		/// <param name="entity">待持久化的实体</param>
		/// <returns>受影响的行数</returns>
		public int Save(DbTransaction tran, ServerConfigLog entity)
		{
			return db.Save<ServerConfigLog>(tran, entity);
		}
		/// <summary>
		/// 在指定事务中批量持久化实体
		/// </summary>
		/// <param name="tran">数据库事务</param>
		/// <param name="entities">待持久化的实体列表</param>
		/// <returns>受影响的行数</returns>
		public int Save(DbTransaction tran, List<ServerConfigLog> entities)
		{
			return db.Save<ServerConfigLog>(tran, entities);
		}
		/// <summary>
		/// 构建执行原生 SQL 语句的上下文
		/// </summary>
		/// <param name="sql">要执行的 SQL 语句</param>
		/// <returns>SQL 执行上下文</returns>
		public SqlSection ExecuteSQL(string sql)
		{
			return db.FromSql(sql);
		}
		/// <summary>
		/// 构建执行存储过程的上下文
		/// </summary>
		/// <param name="procName">存储过程名</param>
		/// <returns>存储过程执行上下文</returns>
		public ProcSection ExcuteProc(string procName)
		{
			return db.FromProc(procName);
		}
	}
}
