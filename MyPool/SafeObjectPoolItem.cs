using System;
using System.Collections.Generic;
using System.Text;

public class SafeObjectPoolItem {

	/// <summary>
	/// 资源对象
	/// </summary>
	public ISafeObjectPool Value { get; set; }


	internal long _getTimes;
	/// <summary>
	/// 被获取的总次数
	/// </summary>
	public long GetTimes => _getTimes;

	/// 最后获取时的时间
	public DateTime LastGetTime { get; internal set; }

	/// <summary>
	/// 最后归还时的时间
	/// </summary>
	public DateTime LastReturnTime { get; internal set; }

	/// <summary>
	/// 最后获取时的线程id
	/// </summary>
	public int LastGetThreadId { get; internal set; }

	/// <summary>
	/// 最后归还时的线程id
	/// </summary>
	public int LastReturnThreadId { get; internal set; }
}