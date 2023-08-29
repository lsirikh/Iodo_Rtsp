using System;

namespace Iodo.Rtsp.Rtsp;

[Serializable]
public class RtspBadResponseException : RtspClientException
{
	public RtspBadResponseException(string message)
		: base(message)
	{
	}
}
