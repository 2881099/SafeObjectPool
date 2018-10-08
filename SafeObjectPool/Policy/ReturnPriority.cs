using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SafeObjectPool.Policy {

	public enum ReturnPriority {

		/// <summary>
		/// 同步
		/// </summary>
		Sync = 1,

		/// <summary>
		/// 异步
		/// </summary>
		Async = 2
	}
}