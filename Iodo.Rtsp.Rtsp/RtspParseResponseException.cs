using System;

namespace Iodo.Rtsp.Rtsp;

[Serializable]
public class RtspParseResponseException : RtspClientException
{
	public RtspParseResponseException(string message)
		: base(message)
	{
	}
}
