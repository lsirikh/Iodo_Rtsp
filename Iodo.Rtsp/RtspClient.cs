using System;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Iodo.Rtsp.RawFrames;
using Iodo.Rtsp.Rtsp;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp;

public sealed class RtspClient : IRtspClient, IDisposable
{
	private readonly Func<IRtspTransportClient> _transportClientProvider;

	private bool _anyFrameReceived;

	private RtspClientInternal _rtspClientInternal;

	private int _disposed;

	public ConnectionParameters ConnectionParameters { get; }

	public event EventHandler<RawFrame> FrameReceived;

	public RtspClient(ConnectionParameters connectionParameters)
	{
		ConnectionParameters = connectionParameters ?? throw new ArgumentNullException("connectionParameters");
	}

	internal RtspClient(ConnectionParameters connectionParameters, Func<IRtspTransportClient> transportClientProvider)
	{
		ConnectionParameters = connectionParameters ?? throw new ArgumentNullException("connectionParameters");
		_transportClientProvider = transportClientProvider ?? throw new ArgumentNullException("transportClientProvider");
	}

	~RtspClient()
	{
		Dispose();
	}

	public async Task ConnectAsync(CancellationToken token)
	{
		await Task.Run(async delegate
		{
			_rtspClientInternal = CreateRtspClientInternal(ConnectionParameters, _transportClientProvider);
			try
			{
				Task connectionTask = _rtspClientInternal.ConnectAsync(token);
				if (connectionTask.IsCompleted)
				{
					await connectionTask;
				}
				else
				{
					CancellationTokenSource delayTaskCancelTokenSource = new CancellationTokenSource();
					using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(delayTaskCancelTokenSource.Token, token);
					Task delayTask = Task.Delay(cancellationToken: linkedTokenSource.Token, delay: ConnectionParameters.ConnectTimeout);
					object obj = connectionTask;
					if (obj != await Task.WhenAny(new Task[2] { connectionTask, delayTask }))
					{
						connectionTask.IgnoreExceptions();
						if (delayTask.IsCanceled)
						{
							throw new OperationCanceledException();
						}
						throw new TimeoutException();
					}
					delayTaskCancelTokenSource.Cancel();
					await connectionTask;
				}
			}
			catch (Exception ex)
			{
				Exception e = ex;
				_rtspClientInternal.Dispose();
				Volatile.Write(ref _rtspClientInternal, null);
				if (e is TimeoutException)
				{
					throw new RtspClientException("Connection timeout", e);
				}
				if (e is OperationCanceledException)
				{
					throw;
				}
				if ((e is RtspBadResponseCodeException rtspBadResponseCodeException && rtspBadResponseCodeException.Code == RtspStatusCode.Unauthorized) || (e is HttpBadResponseCodeException httpBadResponseCodeException && httpBadResponseCodeException.Code == HttpStatusCode.Unauthorized))
				{
					throw new InvalidCredentialException("Invalid login and/or password");
				}
				if (!(e is RtspClientException))
				{
					throw new RtspClientException("Connection error", e);
				}
				throw;
			}
		}, token).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task ReceiveAsync(CancellationToken token)
	{
		if (_rtspClientInternal == null)
		{
			throw new InvalidOperationException("Client should be connected first");
		}
		try
		{
			Task receiveInternalTask = _rtspClientInternal.ReceiveAsync(token);
			if (receiveInternalTask.IsCompleted)
			{
				await receiveInternalTask;
				return;
			}
			CancellationTokenSource delayTaskCancelTokenSource = new CancellationTokenSource();
			using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(delayTaskCancelTokenSource.Token, token);
			CancellationToken delayTaskToken = linkedTokenSource.Token;
			while (true)
			{
				_anyFrameReceived = false;
				Task result = await Task.WhenAny(new Task[2]
				{
					receiveInternalTask,
					Task.Delay(ConnectionParameters.ReceiveTimeout, delayTaskToken)
				}).ConfigureAwait(continueOnCapturedContext: false);
				if (result == receiveInternalTask)
				{
					break;
				}
				if (result.IsCanceled)
				{
					bool flag = ConnectionParameters.CancelTimeout == TimeSpan.Zero;
					bool flag2 = flag;
					if (!flag2)
					{
						flag2 = await Task.WhenAny(new Task[2]
						{
							receiveInternalTask,
							Task.Delay(ConnectionParameters.CancelTimeout, CancellationToken.None)
						}) != receiveInternalTask;
					}
					if (flag2)
					{
						_rtspClientInternal.Dispose();
					}
					await Task.WhenAny(receiveInternalTask);
					throw new OperationCanceledException();
				}
				if (!Volatile.Read(ref _anyFrameReceived))
				{
					receiveInternalTask.IgnoreExceptions();
					throw new RtspClientException("Receive timeout", new TimeoutException());
				}
			}
			delayTaskCancelTokenSource.Cancel();
			await receiveInternalTask;
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (RtspClientException)
		{
			throw;
		}
		catch (Exception ex4)
		{
			Exception e = ex4;
			throw new RtspClientException("Receive error", e);
		}
		finally
		{
			_rtspClientInternal.Dispose();
			Volatile.Write(ref _rtspClientInternal, null);
		}
	}

	public void Dispose()
	{
		if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
		{
			Volatile.Read(ref _rtspClientInternal)?.Dispose();
			GC.SuppressFinalize(this);
		}
	}

	private RtspClientInternal CreateRtspClientInternal(ConnectionParameters connectionParameters, Func<IRtspTransportClient> transportClientProvider)
	{
		return new RtspClientInternal(connectionParameters, transportClientProvider)
		{
			FrameReceived = delegate(RawFrame frame)
			{
				Volatile.Write(ref _anyFrameReceived, value: true);
				this.FrameReceived?.Invoke(this, frame);
			}
		};
	}
}
