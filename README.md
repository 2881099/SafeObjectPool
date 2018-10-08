# 介绍

数据库操作通常是 new SqlConnection()、 Open()、 使用完后 Close()，其实 ado.net 驱动已经现实了连接池管理，不然每次创建、连接、释放相当浪费性能。假设网站的访问在某一时刻突然爆增100万次，new 100万个SqlConnection对象显然会炸掉服务。

对象池使用列表管理一批对象，无论多大并发，始终重复使用创建好的这批对象（从而提升性能），有序的排队申请，使用完后归还资源。

对象池在超过10秒未获取对象时，报出异常：

> SafeObjectPool 获取超时（10秒），设置 Policy.IsThrowGetTimeoutException 可以避免该异常。

# 应用场景

* 数据库连接对象池
* redis连接对象池

# 安装

> Install-Package SafeObjectPool

# 使用方法

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