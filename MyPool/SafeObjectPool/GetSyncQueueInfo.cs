using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

partial class SafeObjectPool {

	class GetSyncQueueInfo : IDisposable {

		internal ManualResetEventSlim Wait { get; set; } = new ManualResetEventSlim();

		internal SafeObjectPoolItem ReturnValue { get; set; }

		internal object Lock = new object();

		internal bool IsTimeout { get; set; } = false;

		public void Dispose() {
			try {
				if (Wait != null) Wait.Dispose();
			} catch {
			}
		}
	}
}
