using System;
using System.Collections.Generic;
using System.Text;

namespace SafeObjectPool {
	
	public class Object<T> : IDisposable {

		internal ObjectPool<T> Pool;

		/// <summary>
		/// 资源对象
		/// </summary>
		public T Value { get; internal set; }

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
		/// 创建时间
		/// </summary>
		public DateTime CreateTime { get; internal set; } = DateTime.Now;

		/// <summary>
		/// 最后获取时的线程id
		/// </summary>
		public int LastGetThreadId { get; internal set; }

		/// <summary>
		/// 最后归还时的线程id
		/// </summary>
		public int LastReturnThreadId { get; internal set; }

		public override string ToString() {
			return $"{this.Value}, Times: {this.GetTimes}, ThreadId(R/G): {this.LastReturnThreadId}/{this.LastGetThreadId}, Time(R/G): {this.LastReturnTime.ToString("yyyy-MM-dd HH:mm:ss:ms")}/{this.LastGetTime.ToString("yyyy-MM-dd HH:mm:ss:ms")}";
		}

		/// <summary>
		/// 重置 Value 值
		/// </summary>
		public void ResetValue() {
			(this.Value as IDisposable)?.Dispose();
			this.Value = this.Pool.Policy.OnCreate();
		}

		public void Dispose() {
			Pool?.Return(this);
		}
	}
}