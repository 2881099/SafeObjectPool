using System;
using System.Collections.Generic;
using System.Text;

namespace SafeObjectPool.Policy {
	public interface IPolicy<T> {

		/// <summary>
		/// 池容量
		/// </summary>
		int PoolSize { get; set; }

		/// <summary>
		/// 默认获取超时设置
		/// </summary>
		TimeSpan SyncGetTimeout { get; set; }

		/// <summary>
		/// 获取超时后，是否抛出异常
		/// </summary>
		bool IsThrowGetTimeoutException { get; set; }

		/// <summary>
		/// 资源归还时，优先转让级别设置
		/// </summary>
		ReturnPriority ReturnPriority { get; set; }


		/// <summary>
		/// 对象池的对象被创建时
		/// </summary>
		/// <returns>返回被创建的对象</returns>
		T Create();

		/// <summary>
		/// 从对象池获取对象超时的时候触发，通过该方法统计
		/// </summary>
		void GetTimeout();

		/// <summary>
		/// 从对象池获取对象成功的时候触发，通过该方法统计或初始化对象
		/// </summary>
		/// <param name="obj">资源对象</param>
		/// <param name="isAsync">同步或异步</param>
		void Get(Object<T> obj, bool isAsync);

		/// <summary>
		/// 归还对象给对象池的时候触发
		/// </summary>
		/// <param name="obj"></param>
		void Return(Object<T> obj);
	}
}