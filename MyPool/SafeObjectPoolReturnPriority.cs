using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

public enum SafeObjectPoolReturnPriority {

	/// <summary>
	/// 同步
	/// </summary>
	Sync = 1,

	/// <summary>
	/// 异步
	/// </summary>
	Async = 2
}
