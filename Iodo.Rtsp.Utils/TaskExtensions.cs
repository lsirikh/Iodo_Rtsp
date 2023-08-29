using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Iodo.Rtsp.Utils;

internal static class TaskExtensions
{
	public static void IgnoreExceptions(this Task task)
	{
		task.ContinueWith(HandleException, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
	}

	[MethodImpl(MethodImplOptions.NoOptimization)]
	private static void HandleException(Task task)
	{
		AggregateException exception = task.Exception;
	}
}
