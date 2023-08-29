using System.Net.Sockets;

namespace Iodo.Rtsp.Utils;

internal static class NetworkClientFactory
{
	private const int TcpReceiveBufferDefaultSize = 65536;

	private const int UdpReceiveBufferDefaultSize = 131072;

	private const int SIO_UDP_CONNRESET = -1744830452;

	private static readonly byte[] EmptyOptionInValue = new byte[4];

	public static Socket CreateTcpClient()
	{
		return new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
		{
			ReceiveBufferSize = 65536,
			DualMode = true,
			NoDelay = true
		};
	}

	public static Socket CreateUdpClient()
	{
		Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
		{
			ReceiveBufferSize = 131072,
			DualMode = true
		};
		socket.IOControl((IOControlCode)(-1744830452L), EmptyOptionInValue, null);
		return socket;
	}
}
