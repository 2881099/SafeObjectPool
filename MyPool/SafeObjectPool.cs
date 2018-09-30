using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public partial class SafeObjectPool {

	private SafeObjectPoolOptions _options { get; set; }

	private List<SafeObjectPoolItem> _allItems = new List<SafeObjectPoolItem>();
	private Queue<SafeObjectPoolItem> _freeItems = new Queue<SafeObjectPoolItem>();

	private Queue<GetSyncQueueInfo> _getSyncQueue = new Queue<GetSyncQueueInfo>();
	private Queue<TaskCompletionSource<SafeObjectPoolItem>> _getAsyncQueue = new Queue<TaskCompletionSource<SafeObjectPoolItem>>();

	private object _lock_freeItems = new object();
	private object _lock_getSyncQueue = new object();

	private bool _isAvailable = true;
	public bool IsAvailable { get => _isAvailable; set { _isAvailable = value; UnavailableTime = value ? null : new DateTime?(DateTime.Now); } }
	public DateTime? UnavailableTime { get; private set; }

	public string Statistics => $"Pool: {_freeItems.Count}/{_allItems.Count}, Get wait: {_getSyncQueue.Count}, GetAsync wait: {_getAsyncQueue.Count}";
	public string StatisticsFullily {
		get {
			var sb = new StringBuilder();
			
			sb.AppendLine(Statistics);
			sb.AppendLine("");

			foreach (var item in _allItems) {
				sb.AppendLine($"{item.Value}, Times: {item.GetTimes}, ThreadId(R/G): {item.LastReturnThreadId}/{item.LastGetThreadId}, Time(R/G): {item.LastReturnTime}/{item.LastGetTime}, ");
			}

			return sb.ToString();
		}
	}

	public SafeObjectPool(int poolsize) : this(new SafeObjectPoolOptions { PoolSize = poolsize }) {
	}
	public SafeObjectPool(SafeObjectPoolOptions options) {
		_options = options;
	}

	/// <summary>
	/// 获取可用资源，或创建资源
	/// </summary>
	/// <returns></returns>
	private SafeObjectPoolItem getFree() {
		SafeObjectPoolItem item = null;

		if (_freeItems.Any())
			lock (_lock_freeItems)
				if (_freeItems.Any())
					item = _freeItems.Dequeue();

		if (item == null && _allItems.Count < _options.PoolSize) {

			lock (_lock_freeItems)
				if (_allItems.Count < _options.PoolSize)
					_allItems.Add(item = new SafeObjectPoolItem());

			if (item != null) {
				//TODO: 实例被创建
			}
		}

		return item;
	}

	/// <summary>
	/// 获取资源
	/// </summary>
	/// <param name="timeout">超时</param>
	/// <returns>资源</returns>
	public SafeObjectPoolItem Get(TimeSpan? timeout = null) {
		//TODO: OnGetPre

		var item = getFree();
		if (item == null) {

			if (timeout == null) timeout = _options.SyncGetTimeout;

			var queueItem = new GetSyncQueueInfo();

			lock (_lock_getSyncQueue)
				_getSyncQueue.Enqueue(queueItem);

			if (queueItem.Wait.Wait(timeout.Value))
				item = queueItem.ReturnValue;

			if (item == null) item = queueItem.ReturnValue;
			if (item == null) lock (queueItem.Lock) queueItem.IsTimeout = (item = queueItem.ReturnValue) == null;
			if (item == null) item = queueItem.ReturnValue;

			if (item == null) {

				//TODO: OnGetTimeout

				if (_options.IsThrowGetTimeoutException)
					throw new Exception($"SafeObjectPool 获取超时（{timeout.Value.TotalSeconds}秒），设置 Options.IsThrowGetTimeoutException 可以避免该异常。");

				return null;
			}
		}

		item.LastGetThreadId = Thread.CurrentThread.ManagedThreadId;
		item.LastGetTime = DateTime.Now;
		Interlocked.Increment(ref item._getTimes);

		//TODO: OnGetCompleted

		return item;
	}

	/// <summary>
	/// 获取资源
	/// </summary>
	/// <returns>资源</returns>
	async public Task<SafeObjectPoolItem> GetAsync() {
		//TODO: OnGetPre

		var item = getFree();
		if (item == null) {

			var tcs = new TaskCompletionSource<SafeObjectPoolItem>();

			lock (_lock_getSyncQueue)
				_getAsyncQueue.Enqueue(tcs);

			item = await tcs.Task;
		}

		item.LastGetThreadId = Thread.CurrentThread.ManagedThreadId;
		item.LastGetTime = DateTime.Now;
		Interlocked.Increment(ref item._getTimes);

		//TODO: OnGetCompleted

		return item;
	}

	/// <summary>
	/// 使用完毕后，归还资源
	/// </summary>
	/// <param name="item"></param>
	public void Return(SafeObjectPoolItem item) {
		//TODO: OnReturnPre

		item.LastReturnThreadId = Thread.CurrentThread.ManagedThreadId;
		item.LastReturnTime = DateTime.Now;

		bool isReturn = false;

		if (_options.ReturnPriority ==  SafeObjectPoolReturnPriority.Async) {

			isReturn = returnAsync(item, isReturn);
			isReturn = returnSync(item, isReturn);
		}

		if (_options.ReturnPriority == SafeObjectPoolReturnPriority.Sync) {

			isReturn = returnSync(item, isReturn);
			isReturn = returnAsync(item, isReturn);
		}

		//无排队，直接归还
		if (isReturn == false) {

			lock (_lock_freeItems)
				_freeItems.Enqueue(item);
		}

		//TODO: OnReturnCompleted
	}

	/// <summary>
	/// 激活 Get 的排队，进行 item 转让
	/// </summary>
	/// <param name="isReturn">是否转让成功</param>
	/// <returns>是否转换成功</returns>
	private bool returnSync(SafeObjectPoolItem item, bool isReturn) {

		while (isReturn == false && _getSyncQueue.Any()) {

			GetSyncQueueInfo queueItem = null;

			lock (_lock_getSyncQueue)
				if (_getSyncQueue.Any())
					queueItem = _getSyncQueue.Dequeue();

			if (queueItem != null) {

				lock (queueItem.Lock)
					if (queueItem.IsTimeout == false)
						queueItem.ReturnValue = item;

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
	/// 激活 GetAsync 的排队，进行 item 转让
	/// </summary>
	/// <param name="isReturn">是否转让成功</param>
	/// <returns>是否转换成功</returns>
	private bool returnAsync(SafeObjectPoolItem item, bool isReturn) {

		if (isReturn == false && _getAsyncQueue.Any()) {

			TaskCompletionSource<SafeObjectPoolItem> tcs = null;

			lock (_lock_getSyncQueue)
				if (_getAsyncQueue.Any())
					tcs = _getAsyncQueue.Dequeue();

			if (tcs != null)
				isReturn = tcs.TrySetResult(item);
		}

		return isReturn;
	}
}