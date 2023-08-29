using System;

namespace Iodo.Rtsp;

internal interface ITransportStream
{
	void Process(ArraySegment<byte> payloadSegment);
}
