using System.Collections.Specialized;
using System.IO;

namespace Iodo.Rtsp.Rtsp;

internal static class HeadersParser
{
	public static NameValueCollection ParseHeaders(StreamReader headersReader)
	{
		NameValueCollection nameValueCollection = new NameValueCollection();
		string text;
		while (!string.IsNullOrEmpty(text = headersReader.ReadLine()))
		{
			int num = text.IndexOf(':');
			if (num != -1)
			{
				string name = text.Substring(0, num).Trim().ToUpperInvariant();
				string value = text.Substring(++num).Trim();
				nameValueCollection.Add(name, value);
			}
		}
		return nameValueCollection;
	}
}
