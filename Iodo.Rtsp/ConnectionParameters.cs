using System;
using System.Net;

namespace Iodo.Rtsp;

public class ConnectionParameters
{
	private const string DefaultUserAgent = "RtspClientSharp";

	private Uri _fixedRtspUri;

	public Uri ConnectionUri { get; }

	public RequiredTracks RequiredTracks { get; set; } = RequiredTracks.All;


	public NetworkCredential Credentials { get; }

	public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30.0);


	public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(10.0);


	public TimeSpan CancelTimeout { get; set; } = TimeSpan.FromSeconds(5.0);


	public string UserAgent { get; set; } = "RtspClientSharp";


	public RtpTransportProtocol RtpTransport { get; set; } = RtpTransportProtocol.TCP;


	public ConnectionParameters(Uri connectionUri)
	{
		ValidateUri(connectionUri);
		ConnectionUri = connectionUri;
		Credentials = GetNetworkCredentialsFromUri(connectionUri);
	}

	public ConnectionParameters(Uri connectionUri, NetworkCredential credentials)
	{
		ValidateUri(connectionUri);
		ConnectionUri = connectionUri;
		Credentials = credentials ?? throw new ArgumentNullException("credentials");
	}

	internal Uri GetFixedRtspUri()
	{
		if (_fixedRtspUri != null)
		{
			return _fixedRtspUri;
		}
		UriBuilder uriBuilder = new UriBuilder(ConnectionUri)
		{
			Scheme = "rtsp",
			UserName = string.Empty,
			Password = string.Empty
		};
		if (ConnectionUri.Port == -1)
		{
			uriBuilder.Port = 554;
		}
		_fixedRtspUri = uriBuilder.Uri;
		return _fixedRtspUri;
	}

	private static void ValidateUri(Uri connectionUri)
	{
		if (connectionUri == null)
		{
			throw new ArgumentNullException("connectionUri");
		}
		if (!connectionUri.IsAbsoluteUri)
		{
			throw new ArgumentException("Connection uri should be absolute", "connectionUri");
		}
	}

	private static NetworkCredential GetNetworkCredentialsFromUri(Uri connectionUri)
	{
		string userInfo = connectionUri.UserInfo;
		string userName = null;
		string password = null;
		if (!string.IsNullOrEmpty(userInfo))
		{
			string[] array = userInfo.Split(new char[1] { ':' });
			if (array.Length == 2)
			{
				userName = Uri.UnescapeDataString(array[0]);
				password = Uri.UnescapeDataString(array[1]);
			}
		}
		return new NetworkCredential(userName, password);
	}
}
