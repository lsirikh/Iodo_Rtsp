using System;

namespace Iodo.Rtsp.Rtsp;

[Serializable]
public class RtspBadResponseCodeException : RtspClientException
{
	public RtspStatusCode Code { get; }

	public RtspBadResponseCodeException(RtspStatusCode code)
		: base($"Bad response code: {code}")
	{
		Code = code;
	}
}
