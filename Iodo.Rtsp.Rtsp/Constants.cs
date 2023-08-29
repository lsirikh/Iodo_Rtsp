namespace Iodo.Rtsp.Rtsp;

internal static class Constants
{
	public const int DefaultHttpPort = 80;

	public const int DefaultRtspPort = 554;

	public static readonly byte[] RtspProtocolNameBytes = new byte[4] { 82, 84, 83, 80 };

	public const int MaxResponseHeadersSize = 8192;

	public static readonly byte[] DoubleCrlfBytes = new byte[4] { 13, 10, 13, 10 };

	public const int UdpReceiveBufferSize = 2048;
}
