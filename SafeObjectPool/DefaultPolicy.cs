using SafeObjectPool.Policy;
using System;
using System.Collections.Generic;
using System.Text;

namespace SafeObjectPool {

	public class DefaultPolicy<T> : IPolicy<T> {
		public int PoolSize { get; set; } = 1000;
		public TimeSpan SyncGetTimeout { get; set; } = TimeSpan.FromSeconds(10);
		public bool IsThrowGetTimeoutException { get; set; } = true;
		public ReturnPriority ReturnPriority { get; set; } = ReturnPriority.Sync;

		public Func<T> CreateObject;
		public Action<Object<T>> OnGetObject;

		public T Create() {
			return CreateObject();
		}

		public void Get(Object<T> obj, bool isAsync) {
			if (isAsync) Console.WriteLine("GetAsync: " + obj);
			else Console.WriteLine("Get: " + obj);

			OnGetObject?.Invoke(obj);
		}

		public void GetTimeout() {
			
		}

		public void Return(Object<T> obj) {
			Console.WriteLine("Return: " + obj);
		}
	}
}