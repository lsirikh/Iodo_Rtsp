using System;
using System.Threading;
using System.Threading.Tasks;
using Iodo.Rtsp.RawFrames;

namespace Iodo.Rtsp;

public interface IRtspClient : IDisposable
{
	ConnectionParameters ConnectionParameters { get; }

	event EventHandler<RawFrame> FrameReceived;

	Task ConnectAsync(CancellationToken token);

	Task ReceiveAsync(CancellationToken token);
}
