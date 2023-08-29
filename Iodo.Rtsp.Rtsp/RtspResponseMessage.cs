#define DEBUG
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Iodo.Rtsp.Rtsp;

internal class RtspResponseMessage : RtspMessage
{
	private static readonly ArraySegment<byte> EmptySegment = new ArraySegment<byte>(Array.Empty<byte>(), 0, 0);

	public RtspStatusCode StatusCode { get; }

	public ArraySegment<byte> ResponseBody { get; set; } = EmptySegment;


	public RtspResponseMessage(RtspStatusCode statusCode, Version protocolVersion, uint cSeq, NameValueCollection headers)
		: base(cSeq, protocolVersion, headers)
	{
		StatusCode = statusCode;
	}

	public static RtspResponseMessage Parse(ArraySegment<byte> byteSegment)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		MemoryStream stream = new MemoryStream(byteSegment.Array, byteSegment.Offset, byteSegment.Count, writable: false);
		StreamReader streamReader = new StreamReader(stream);
		string text = streamReader.ReadLine();
		if (text == null)
		{
			throw new RtspParseResponseException("Empty response");
		}
		string[] firstLineTokens = GetFirstLineTokens(text);
		string protocolNameVersion = firstLineTokens[0];
		Version protocolVersion = ParseProtocolVersion(protocolNameVersion);
		RtspStatusCode statusCode = ParseStatusCode(firstLineTokens[1]);
		NameValueCollection nameValueCollection = HeadersParser.ParseHeaders(streamReader);
		uint result = 0u;
		string text2 = nameValueCollection.Get("CSEQ");
		if (text2 != null)
		{
			uint.TryParse(text2, out result);
		}
		return new RtspResponseMessage(statusCode, protocolVersion, result, nameValueCollection);
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("RTSP/{0} {1} {2}\r\nCSeq: {3}\r\n", base.ProtocolVersion, (int)StatusCode, StatusCode, base.CSeq);
		string[] allKeys = base.Headers.AllKeys;
		foreach (string text in allKeys)
		{
			stringBuilder.AppendFormat("{0}: {1}\r\n", text, base.Headers.Get(text));
		}
		if (ResponseBody.Count != 0)
		{
			stringBuilder.AppendLine();
			string @string = Encoding.ASCII.GetString(ResponseBody.Array, ResponseBody.Offset, ResponseBody.Count);
			stringBuilder.Append(@string);
		}
		return stringBuilder.ToString();
	}

	private static RtspStatusCode ParseStatusCode(string statusCode)
	{
		if (!int.TryParse(statusCode, out var result))
		{
			throw new RtspParseResponseException("Invalid status code: " + statusCode);
		}
		return (RtspStatusCode)result;
	}

	private static string[] GetFirstLineTokens(string startLine)
	{
		string[] array = startLine.Split(new char[1] { ' ' });
		if (array.Length == 0)
		{
			throw new RtspParseResponseException("Missing method");
		}
		if (array.Length == 1)
		{
			throw new RtspParseResponseException("Missing URI");
		}
		if (array.Length == 2)
		{
			throw new RtspParseResponseException("Missing protocol version");
		}
		return array;
	}

	private static Version ParseProtocolVersion(string protocolNameVersion)
	{
		int num = protocolNameVersion.IndexOf('/');
		if (num == -1)
		{
			throw new RtspParseResponseException("Invalid protocol name/version format: " + protocolNameVersion);
		}
		string text = protocolNameVersion.Substring(num + 1);
		if (!Version.TryParse(text, out Version result))
		{
			throw new RtspParseResponseException("Invalid RTSP protocol version: " + text);
		}
		return result;
	}
}
