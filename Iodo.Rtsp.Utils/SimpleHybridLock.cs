using System;
using System.Threading;

namespace Iodo.Rtsp.Utils;

internal sealed class SimpleHybridLock : IDisposable
{
	private int _waiters;

	private readonly AutoResetEvent _waiterLock = new AutoResetEvent(initialState: false);

	public void Enter()
	{
		if (Interlocked.Increment(ref _waiters) != 1)
		{
			_waiterLock.WaitOne();
		}
	}

	public void Leave()
	{
		if (Interlocked.Decrement(ref _waiters) != 0)
		{
			_waiterLock.Set();
		}
	}

	public void Dispose()
	{
		_waiterLock.Dispose();
	}
}
