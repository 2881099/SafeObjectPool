using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SafeObjectPool.Policy;

namespace SafeObjectPool {

	/// <summary>
	/// 对象池管理类
	/// </summary>
	/// <typeparam name="T">对象类型</typeparam>
	public partial class ObjectPool<T> {

		internal IPolicy<T> _policy;

		private List<Object<T>> _allObjects = new List<Object<T>>();
		private object _allObjectsLock = new object();
		private ConcurrentQueue<Object<T>> _freeObjects = new ConcurrentQueue<Object<T>>();

		private ConcurrentQueue<GetSyncQueueInfo> _getSyncQueue = new ConcurrentQueue<GetSyncQueueInfo>();
		private ConcurrentQueue<TaskCompletionSource<Object<T>>> _getAsyncQueue = new ConcurrentQueue<TaskCompletionSource<Object<T>>>();

		private bool _isAvailable = true;
		/// <summary>
		/// 是否可用
		/// </summary>
		public bool IsAvailable {
			get => _isAvailable;
			set {
				_isAvailable = value;
				UnavailableTime = value ? null : new DateTime?(DateTime.Now);

				// 不可用的时候，将池中所有对象状态设为不可用
				if (value == false) {
					var lastActive = new DateTime(2000, 1, 1);
					lock (_allObjectsLock) {
						foreach (var obj in _allObjects) obj.IsAvailable = value;
					}
				}
			}
		}
		/// <summary>
		/// 不可用时间
		/// </summary>
		public DateTime? UnavailableTime { get; private set; }

		/// <summary>
		/// 统计
		/// </summary>
		public string Statistics => $"Pool: {_freeObjects.Count}/{_allObjects.Count}, Get wait: {_getSyncQueue.Count}, GetAsync wait: {_getAsyncQueue.Count}";
		/// <summary>
		/// 统计（完整)
		/// </summary>
		public string StatisticsFullily {
			get {
				var sb = new StringBuilder();

				sb.AppendLine(Statistics);
				sb.AppendLine("");

				foreach (var obj in _allObjects) {
					sb.AppendLine($"{obj.Value}, Times: {obj.GetTimes}, ThreadId(R/G): {obj.LastReturnThreadId}/{obj.LastGetThreadId}, Time(R/G): {obj.LastReturnTime.ToString("yyyy-MM-dd HH:mm:ss:ms")}/{obj.LastGetTime.ToString("yyyy-MM-dd HH:mm:ss:ms")}, ");
				}

				return sb.ToString();
			}
		}

		/// <summary>
		/// 创建对象池
		/// </summary>
		/// <param name="poolsize">池大小</param>
		/// <param name="createObject">池内对象的创建委托</param>
		/// <param name="onGetObject">获取池内对象成功后，进行使用前操作</param>
		public ObjectPool(int poolsize, Func<T> createObject, Action<Object<T>> onGetObject = null) : this(new DefaultPolicy<T> { PoolSize = poolsize, CreateObject = createObject, OnGetObject = onGetObject }) {
		}
		/// <summary>
		/// 创建对象池
		/// </summary>
		/// <param name="policy">策略</param>
		public ObjectPool(IPolicy<T> policy) {
			_policy = policy;
		}

		/// <summary>
		/// 获取可用资源，或创建资源
		/// </summary>
		/// <returns></returns>
		private Object<T> getFree() {

			if ((_freeObjects.TryDequeue(out var obj) == false || obj == null) && _allObjects.Count < _policy.PoolSize) {

				lock (_allObjectsLock)
					if (_allObjects.Count < _policy.PoolSize)
						_allObjects.Add(obj = new Object<T> { Pool = this });

				if (obj != null)
					obj.Value = _policy.Create();
			}

			return obj;
		}

		/// <summary>
		/// 获取资源
		/// </summary>
		/// <param name="timeout">超时</param>
		/// <returns>资源</returns>
		public Object<T> Get(TimeSpan? timeout = null) {

			var obj = getFree();
			if (obj == null) {

				if (timeout == null) timeout = _policy.SyncGetTimeout;

				var queueItem = new GetSyncQueueInfo();

				_getSyncQueue.Enqueue(queueItem);

				if (queueItem.Wait.Wait(timeout.Value))
					obj = queueItem.ReturnValue;

				if (obj == null) obj = queueItem.ReturnValue;
				if (obj == null) lock (queueItem.Lock) queueItem.IsTimeout = (obj = queueItem.ReturnValue) == null;
				if (obj == null) obj = queueItem.ReturnValue;

				if (obj == null) {

					_policy.GetTimeout();

					if (_policy.IsThrowGetTimeoutException)
						throw new Exception($"SafeObjectPool 获取超时（{timeout.Value.TotalSeconds}秒），设置 Policy.IsThrowGetTimeoutException 可以避免该异常。");

					return null;
				}
			}

			obj.LastGetThreadId = Thread.CurrentThread.ManagedThreadId;
			obj.LastGetTime = DateTime.Now;
			Interlocked.Increment(ref obj._getTimes);

			_policy.Get(obj, false);

			return obj;
		}

		/// <summary>
		/// 获取资源
		/// </summary>
		/// <returns>资源</returns>
		async public Task<Object<T>> GetAsync() {

			var obj = getFree();
			if (obj == null) {

				var tcs = new TaskCompletionSource<Object<T>>();

				_getAsyncQueue.Enqueue(tcs);

				obj = await tcs.Task;
			}

			obj.LastGetThreadId = Thread.CurrentThread.ManagedThreadId;
			obj.LastGetTime = DateTime.Now;
			Interlocked.Increment(ref obj._getTimes);

			_policy.Get(obj, true);

			return obj;
		}

		/// <summary>
		/// 使用完毕后，归还资源
		/// </summary>
		/// <param name="obj">对象</param>
		/// <param name="isRecreate">是否重新创建</param>
		public void Return(Object<T> obj, bool isRecreate = false) {

			if (obj == null) return;

			if (isRecreate) {

				(obj.Value as IDisposable)?.Dispose();

				obj.Value = _policy.Create();
			}

			obj.LastReturnThreadId = Thread.CurrentThread.ManagedThreadId;
			obj.LastReturnTime = DateTime.Now;

			bool isReturn = false;

			if (_policy.ReturnPriority == ReturnPriority.Async) {

				isReturn = returnAsync(obj, isReturn);
				isReturn = returnSync(obj, isReturn);
			}

			if (_policy.ReturnPriority == ReturnPriority.Sync) {

				isReturn = returnSync(obj, isReturn);
				isReturn = returnAsync(obj, isReturn);
			}

			//无排队，直接归还
			if (isReturn == false) {

				try {

					_policy.Return(obj);

				} catch {

					throw;

				} finally {

					_freeObjects.Enqueue(obj);
				}
			}
		}

		/// <summary>
		/// 激活 Get 的排队，进行 obj 转让
		/// </summary>
		/// <param name="isReturn">是否转让成功</param>
		/// <returns>是否转换成功</returns>
		private bool returnSync(Object<T> obj, bool isReturn) {

			while (isReturn == false && _getSyncQueue.Any()) {

				if (_getSyncQueue.TryDequeue(out var queueItem) && queueItem != null) {

					lock (queueItem.Lock)
						if (queueItem.IsTimeout == false)
							queueItem.ReturnValue = obj;

					if (queueItem.ReturnValue != null) {

						queueItem.Wait.Set();
						isReturn = true;
					}

					queueItem.Dispose();
				}
			}

			return isReturn;
		}

		/// <summary>
		/// 激活 GetAsync 的排队，进行 obj 转让
		/// </summary>
		/// <param name="isReturn">是否转让成功</param>
		/// <returns>是否转换成功</returns>
		private bool returnAsync(Object<T> obj, bool isReturn) {

			if (isReturn == false && _getAsyncQueue.Any()) {

				if (_getAsyncQueue.TryDequeue(out var tcs) && tcs != null)
					isReturn = tcs.TrySetResult(obj);
			}

			return isReturn;
		}

		class GetSyncQueueInfo : IDisposable {

			internal ManualResetEventSlim Wait { get; set; } = new ManualResetEventSlim();

			internal Object<T> ReturnValue { get; set; }

			internal object Lock = new object();

			internal bool IsTimeout { get; set; } = false;

			public void Dispose() {
				try {
					if (Wait != null)
						Wait.Dispose();
				} catch {
				}
			}
		}
	}
}