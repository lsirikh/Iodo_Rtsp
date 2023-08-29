using System;
using System.Text;

namespace Iodo.Rtsp.Rtsp;

internal class RtspRequestMessage : RtspMessage
{
	private readonly Func<uint> _cSeqProvider;

	public RtspMethod Method { get; }

	public Uri ConnectionUri { get; }

	public string UserAgent { get; }

	public RtspRequestMessage(RtspMethod method, Uri connectionUri, Version protocolVersion, Func<uint> cSeqProvider, string userAgent, string session)
		: base(cSeqProvider(), protocolVersion)
	{
		Method = method;
		ConnectionUri = connectionUri;
		_cSeqProvider = cSeqProvider;
		UserAgent = userAgent;
		if (!string.IsNullOrEmpty(session))
		{
			base.Headers.Add("Session", session);
		}
	}

	public void UpdateSequenceNumber()
	{
		base.CSeq = _cSeqProvider();
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder(512);
		stringBuilder.AppendFormat("{0} {1} RTSP/{2}\r\n", Method, ConnectionUri, base.ProtocolVersion.ToString(2));
		stringBuilder.AppendFormat("CSeq: {0}\r\n", base.CSeq);
		if (!string.IsNullOrEmpty(UserAgent))
		{
			stringBuilder.AppendFormat("User-Agent: {0}\r\n", UserAgent);
		}
		string[] allKeys = base.Headers.AllKeys;
		foreach (string text in allKeys)
		{
			stringBuilder.AppendFormat("{0}: {1}\r\n", text, base.Headers[text]);
		}
		stringBuilder.Append("\r\n");
		return stringBuilder.ToString();
	}
}
