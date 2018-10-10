## 介绍

数据库操作通常是 new SqlConnection()、 Open()、 使用完后 Close()，其实 ado.net 驱动已经现实了连接池管理，不然每次创建、连接、释放相当浪费性能。假设网站的访问在某一时刻突然爆增100万次，new 100万个SqlConnection对象显然会炸掉服务，连接对象，每次创建，connect，disconnect，disponse，显然开销很大。目前看来，最适合做连接对象的池子，对象池里的连接对象，保持长链接，效率最大化。

ado.net自带的链接池不完美，比如占满的时候再请求会报错。ObjectPool 解决池用尽后，再请求不报错，排队等待机制。

对象池容器化管理一批对象，重复使用从而提升性能，有序排队申请，使用完后归还资源。

对象池在超过10秒仍然未获取到对象时，报出异常：

> SafeObjectPool 获取超时（10秒），设置 Policy.IsThrowGetTimeoutException 可以避免该异常。

## 与dapper比武测试

```csharp
[HttpGet("vs_gen")]
async public Task<object> vs_gen() {
	var select = Tag.Select;
	var count = await select.CountAsync();
	var items = await select.Page(page, limit).ToListAsync();

	return new { count, items };
}

[HttpGet("vs_dapper")]
async public Task<object> vs_dapper() {
	var conn = new SqlConnection("Data Source=.;Integrated Security=True;Initial Catalog=cms;Pooling=true;Max Pool Size=11");
	conn.Open();
	var count = await conn.ExecuteScalarAsync<int>("SELECT count(1) FROM[dbo].[tag] a");
	//conn.Close();

	//conn = new SqlConnection("Data Source=.;Integrated Security=True;Initial Catalog=cms;Pooling=true;Max Pool Size=11");
	//conn.Open();
	var items = await conn.QueryAsync("SELECT TOP 20 a.[id], a.[parent_id], a.[name] FROM[dbo].[tag] a");
	conn.Close();

	return new { count, items };
}
```

连接池最大为：10，11

ab -c 10 -n 1000 -s 6000 测试结果差不多

-c 100 时，vs_dapper直接挂了，vs_gen没影响（使用了SafeObjectPool）

> 实践证明ado.net过于暴露，突然的高并发招架不住。

## 应用场景

* 数据库连接对象池
	> [SQLServer连接池](https://github.com/2881099/dng.Mssql/blob/master/Mssql/SqlConnectionPool.cs)、[MySQL连接池](https://github.com/2881099/dng.Mysql/blob/master/MySql.Data.MySqlClient/MySqlConnectionPool.cs)、[PostgreSQL连接池](https://github.com/2881099/dng.Pgsql/blob/master/Npgsql/NpgsqlConnectionPool.cs)
* redis连接对象池

## 安装

> Install-Package SafeObjectPool

## 使用方法

```csharp
var pool = new SafeObjectPool.ObjectPool<MemoryStream>(10, () => new MemoryStream(), obj => {
	if (DateTime.Now.Subtract(obj.LastGetTime).TotalSeconds > 5) {
		// 对象超过5秒未活动，进行操作
	}
});

var obj = pool.Get(); //借
pool.Return(obj); //归还

//或者 using 自动归还
using (var obj = pool.Get()) {

}
```

## SQLServer连接池

```csharp
var pool = new System.Data.SqlClient.SqlConnectionPool("名称", connectionString, 可用时触发的委托, 不可用时触发的委托);
var conn = pool.Get();

try {
	// 使用 ...
	pool.Return(conn); //正常归还
} catch (Exception ex) {
	pool.Return(conn, ex); //发生错误时归还
}
```

## MySQL连接池

```csharp
var pool = new MySql.Data.MySqlClient.MySqlConnectionPool("名称", connectionString, 可用时触发的委托, 不可用时触发的委托);
var conn = pool.Get();

try {
	// 使用 ...
	pool.Return(conn); //正常归还
} catch (Exception ex) {
	pool.Return(conn, ex); //发生错误时归还
}
```

## PostgreSQL连接池

```csharp
var pool = new Npgsql.NpgsqlConnectionPool("名称", connectionString, 可用时触发的委托, 不可用时触发的委托);
var conn = pool.Get();

try {
	// 使用 ...
	pool.Return(conn); //正常归还
} catch (Exception ex) {
	pool.Return(conn, ex); //发生错误时归还
}
```

# 更多连接池正在开发中。。。