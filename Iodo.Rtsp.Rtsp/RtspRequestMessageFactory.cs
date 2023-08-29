using System;

namespace Iodo.Rtsp.Rtsp;

internal class RtspRequestMessageFactory
{
	private static readonly Version ProtocolVersion = new Version(1, 0);

	private uint _cSeq;

	private readonly Uri _rtspUri;

	private readonly string _userAgent;

	public Uri ContentBase { get; set; }

	public string SessionId { get; set; }

	public RtspRequestMessageFactory(Uri rtspUri, string userAgent)
	{
		_rtspUri = rtspUri ?? throw new ArgumentNullException("rtspUri");
		_userAgent = userAgent;
	}

	public RtspRequestMessage CreateOptionsRequest()
	{
		return new RtspRequestMessage(RtspMethod.OPTIONS, _rtspUri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
	}

	public RtspRequestMessage CreateDescribeRequest()
	{
		RtspRequestMessage rtspRequestMessage = new RtspRequestMessage(RtspMethod.DESCRIBE, _rtspUri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
		rtspRequestMessage.Headers.Add("Accept", "application/sdp");
		return rtspRequestMessage;
	}

	public RtspRequestMessage CreateSetupTcpInterleavedRequest(string trackName, int rtpChannel, int rtcpChannel)
	{
		Uri trackUri = GetTrackUri(trackName);
		RtspRequestMessage rtspRequestMessage = new RtspRequestMessage(RtspMethod.SETUP, trackUri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
		rtspRequestMessage.Headers.Add("Transport", $"RTP/AVP/TCP;unicast;interleaved={rtpChannel}-{rtcpChannel}");
		return rtspRequestMessage;
	}

	public RtspRequestMessage CreateSetupUdpUnicastRequest(string trackName, int rtpPort, int rtcpPort)
	{
		Uri trackUri = GetTrackUri(trackName);
		RtspRequestMessage rtspRequestMessage = new RtspRequestMessage(RtspMethod.SETUP, trackUri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
		rtspRequestMessage.Headers.Add("Transport", $"RTP/AVP/UDP;unicast;client_port={rtpPort}-{rtcpPort}");
		return rtspRequestMessage;
	}

	public RtspRequestMessage CreatePlayRequest()
	{
		Uri contentBasedUri = GetContentBasedUri();
		RtspRequestMessage rtspRequestMessage = new RtspRequestMessage(RtspMethod.PLAY, contentBasedUri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
		rtspRequestMessage.Headers.Add("Range", "npt=0.000-");
		return rtspRequestMessage;
	}

	public RtspRequestMessage CreateTeardownRequest()
	{
		return new RtspRequestMessage(RtspMethod.TEARDOWN, _rtspUri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
	}

	public RtspRequestMessage CreateGetParameterRequest()
	{
		return new RtspRequestMessage(RtspMethod.GET_PARAMETER, _rtspUri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
	}

	private Uri GetContentBasedUri()
	{
		if (ContentBase != null)
		{
			return ContentBase;
		}
		return _rtspUri;
	}

	private uint NextCSeqProvider()
	{
		return ++_cSeq;
	}

	private Uri GetTrackUri(string trackName)
	{
		if (!Uri.IsWellFormedUriString(trackName, UriKind.Absolute))
		{
			UriBuilder uriBuilder = new UriBuilder(GetContentBasedUri());
			bool flag = trackName.StartsWith("/");
			if (uriBuilder.Path.EndsWith("/"))
			{
				uriBuilder.Path += (flag ? trackName.Substring(1) : trackName);
			}
			else
			{
				uriBuilder.Path += (flag ? trackName : ("/" + trackName));
			}
			return uriBuilder.Uri;
		}
		return new Uri(trackName, UriKind.Absolute);
	}
}
