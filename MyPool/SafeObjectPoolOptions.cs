using System;
using System.Collections.Generic;
using System.Text;

public class SafeObjectPoolOptions {

	/// <summary>
	/// 池容量
	/// </summary>
	public int PoolSize { get; set; } = 1000;

	/// <summary>
	/// 默认获取超时设置
	/// </summary>
	public TimeSpan SyncGetTimeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// 获取超时后，是否抛出异常
	/// </summary>
	public bool IsThrowGetTimeoutException { get; set; } = true;

	/// <summary>
	/// 资源归还时，优先转让级别设置
	/// </summary>
	public SafeObjectPoolReturnPriority ReturnPriority { get; set; } = SafeObjectPoolReturnPriority.Sync;
}
