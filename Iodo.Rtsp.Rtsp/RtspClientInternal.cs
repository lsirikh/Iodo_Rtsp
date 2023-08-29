#define DEBUG
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Iodo.Rtsp.Codecs.Audio;
using Iodo.Rtsp.Codecs.Video;
using Iodo.Rtsp.MediaParsers;
using Iodo.Rtsp.RawFrames;
using Iodo.Rtsp.Rtcp;
using Iodo.Rtsp.Rtp;
using Iodo.Rtsp.Sdp;
using Iodo.Rtsp.Tpkt;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Rtsp;

internal sealed class RtspClientInternal : IDisposable
{
	private const int RtcpReportIntervalBaseMs = 5000;

	private static readonly char[] TransportAttributesSeparator = new char[1] { ';' };

	private readonly ConnectionParameters _connectionParameters;

	private readonly Func<IRtspTransportClient> _transportClientProvider;

	private readonly RtspRequestMessageFactory _requestMessageFactory;

	private readonly Dictionary<int, ITransportStream> _streamsMap = new Dictionary<int, ITransportStream>();

	private readonly ConcurrentDictionary<int, Socket> _udpClientsMap = new ConcurrentDictionary<int, Socket>();

	private readonly Dictionary<int, RtcpReceiverReportsProvider> _reportProvidersMap = new Dictionary<int, RtcpReceiverReportsProvider>();

	private TpktStream _tpktStream;

	private readonly SimpleHybridLock _hybridLock = new SimpleHybridLock();

	private readonly Random _random = RandomGeneratorFactory.CreateGenerator();

	private IRtspTransportClient _rtspTransportClient;

	private int _rtspKeepAliveTimeoutMs;

	private readonly CancellationTokenSource _serverCancellationTokenSource = new CancellationTokenSource();

	private bool _isServerSupportsGetParameterRequest;

	private int _disposed;

	public Action<RawFrame> FrameReceived;

	public RtspClientInternal(ConnectionParameters connectionParameters, Func<IRtspTransportClient> transportClientProvider = null)
	{
		_connectionParameters = connectionParameters ?? throw new ArgumentNullException("connectionParameters");
		_transportClientProvider = transportClientProvider ?? new Func<IRtspTransportClient>(CreateTransportClient);
		Uri fixedRtspUri = connectionParameters.GetFixedRtspUri();
		_requestMessageFactory = new RtspRequestMessageFactory(fixedRtspUri, connectionParameters.UserAgent);
	}

	public async Task ConnectAsync(CancellationToken token)
	{
		Volatile.Write(value: _transportClientProvider(), location: ref _rtspTransportClient);
		await _rtspTransportClient.ConnectAsync(token);
		RtspRequestMessage optionsRequest = _requestMessageFactory.CreateOptionsRequest();
		RtspResponseMessage optionsResponse = await _rtspTransportClient.ExecuteRequest(optionsRequest, token);
		if (optionsResponse.StatusCode == RtspStatusCode.Ok)
		{
			ParsePublicHeader(optionsResponse.Headers[WellKnownHeaders.Public]);
		}
		RtspRequestMessage describeRequest = _requestMessageFactory.CreateDescribeRequest();
		RtspResponseMessage describeResponse = await _rtspTransportClient.EnsureExecuteRequest(describeRequest, token);
		string contentBaseHeader = describeResponse.Headers[WellKnownHeaders.ContentBase];
		if (!string.IsNullOrEmpty(contentBaseHeader))
		{
			_requestMessageFactory.ContentBase = new Uri(contentBaseHeader);
		}
		SdpParser parser = new SdpParser();
		IEnumerable<RtspTrackInfo> tracks = parser.Parse(describeResponse.ResponseBody);
		bool anyTrackRequested = false;
		foreach (RtspMediaTrackInfo track in GetTracksToSetup(tracks))
		{
			await SetupTrackAsync(track, token);
			anyTrackRequested = true;
		}
		if (!anyTrackRequested)
		{
			throw new RtspClientException("Any suitable track is not found");
		}
		RtspRequestMessage playRequest = _requestMessageFactory.CreatePlayRequest();
		await _rtspTransportClient.EnsureExecuteRequest(playRequest, token, 1);
	}

	public async Task ReceiveAsync(CancellationToken token)
	{
		if (_rtspTransportClient == null)
		{
			throw new InvalidOperationException("Client should be connected first");
		}
		TimeSpan nextRtspKeepAliveInterval2 = GetNextRtspKeepAliveInterval();
		using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_serverCancellationTokenSource.Token, token);
		CancellationToken linkedToken = linkedTokenSource.Token;
		Task receiveTask = ((_connectionParameters.RtpTransport == RtpTransportProtocol.TCP) ? ReceiveOverTcpAsync(_rtspTransportClient.GetStream(), linkedToken) : ReceiveOverUdpAsync(linkedToken));
		if (!_isServerSupportsGetParameterRequest)
		{
			await receiveTask;
		}
		else
		{
			Task rtspKeepAliveDelayTask = Task.Delay(nextRtspKeepAliveInterval2, linkedToken);
			while (true)
			{
				Task result = await Task.WhenAny(new Task[2] { receiveTask, rtspKeepAliveDelayTask });
				if (result == receiveTask || result.IsCanceled)
				{
					break;
				}
				nextRtspKeepAliveInterval2 = GetNextRtspKeepAliveInterval();
				rtspKeepAliveDelayTask = Task.Delay(nextRtspKeepAliveInterval2, linkedToken);
				await SendRtspKeepAliveAsync(linkedToken);
			}
			await receiveTask;
		}
		if (linkedToken.IsCancellationRequested)
		{
			await CloseRtspSessionAsync(CancellationToken.None);
		}
	}

	public void Dispose()
	{
		if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
		{
			return;
		}
		if (_udpClientsMap.Count != 0)
		{
			foreach (Socket value in _udpClientsMap.Values)
			{
				value.Close();
			}
		}
		IRtspTransportClient rtspTransportClient = Volatile.Read(ref _rtspTransportClient);
		if (rtspTransportClient != null)
		{
			_rtspTransportClient.Dispose();
		}
	}

	private IRtspTransportClient CreateTransportClient()
	{
		if (_connectionParameters.ConnectionUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.InvariantCultureIgnoreCase))
		{
			return new RtspHttpTransportClient(_connectionParameters);
		}
		return new RtspTcpTransportClient(_connectionParameters);
	}

	private TimeSpan GetNextRtspKeepAliveInterval()
	{
		return TimeSpan.FromMilliseconds(_random.Next(_rtspKeepAliveTimeoutMs / 2, _rtspKeepAliveTimeoutMs * 3 / 4));
	}

	private int GetNextRtcpReportIntervalMs()
	{
		return 5000 + _random.Next(0, 11) * 100;
	}

	private async Task SetupTrackAsync(RtspMediaTrackInfo track, CancellationToken token)
	{
		Socket rtpClient = null;
		Socket rtcpClient = null;
		RtspResponseMessage setupResponse;
		int rtpChannelNumber;
		int rtcpChannelNumber;
		if (_connectionParameters.RtpTransport == RtpTransportProtocol.UDP)
		{
			rtpClient = NetworkClientFactory.CreateUdpClient();
			rtcpClient = NetworkClientFactory.CreateUdpClient();
			try
			{
				IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
				rtpClient.Bind(endPoint);
				int rtpPort = ((IPEndPoint)rtpClient.LocalEndPoint).Port;
				endPoint = new IPEndPoint(IPAddress.Any, rtpPort + 1);
				try
				{
					rtcpClient.Bind(endPoint);
				}
				catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
				{
					endPoint = new IPEndPoint(IPAddress.Any, 0);
					rtcpClient.Bind(endPoint);
				}
				int rtcpPort = ((IPEndPoint)rtcpClient.LocalEndPoint).Port;
				RtspRequestMessage setupRequest2 = _requestMessageFactory.CreateSetupUdpUnicastRequest(track.TrackName, rtpPort, rtcpPort);
				setupResponse = await _rtspTransportClient.EnsureExecuteRequest(setupRequest2, token);
			}
			catch
			{
				rtpClient.Close();
				rtcpClient.Close();
				throw;
			}
		}
		else
		{
			int channelCounter = _streamsMap.Count;
			rtpChannelNumber = channelCounter;
			int num = channelCounter + 1;
			rtcpChannelNumber = num;
			RtspRequestMessage setupRequest2 = _requestMessageFactory.CreateSetupTcpInterleavedRequest(track.TrackName, rtpChannelNumber, rtcpChannelNumber);
			setupResponse = await _rtspTransportClient.EnsureExecuteRequest(setupRequest2, token);
		}
		string transportHeader = setupResponse.Headers[WellKnownHeaders.Transport];
		if (string.IsNullOrEmpty(transportHeader))
		{
			throw new RtspBadResponseException("Transport header is not found");
		}
		string portsAttributeName = ((_connectionParameters.RtpTransport == RtpTransportProtocol.UDP) ? "server_port" : "interleaved");
		string[] transportAttributes = transportHeader.Split(TransportAttributesSeparator, StringSplitOptions.RemoveEmptyEntries);
		string portsAttribute = transportAttributes.FirstOrDefault((string a) => a.StartsWith(portsAttributeName, StringComparison.InvariantCultureIgnoreCase));
		if (portsAttribute == null || !TryParseSeverPorts(portsAttribute, out rtpChannelNumber, out rtcpChannelNumber))
		{
			throw new RtspBadResponseException("Server ports are not found");
		}
		if (_connectionParameters.RtpTransport == RtpTransportProtocol.UDP)
		{
			string sourceAttribute = transportAttributes.FirstOrDefault((string a) => a.StartsWith("source", StringComparison.InvariantCultureIgnoreCase));
			int equalSignIndex = default(int);
			int num2;
			if (sourceAttribute != null)
			{
				int num;
				equalSignIndex = (num = sourceAttribute.IndexOf("=", StringComparison.CurrentCultureIgnoreCase));
				num2 = ((num != -1) ? 1 : 0);
			}
			else
			{
				num2 = 0;
			}
			IPAddress sourceAddress;
			if (num2 != 0)
			{
				int num = equalSignIndex + 1;
				sourceAddress = IPAddress.Parse(sourceAttribute.Substring(num).Trim());
			}
			else
			{
				sourceAddress = ((IPEndPoint)_rtspTransportClient.RemoteEndPoint).Address;
			}
			Debug.Assert(rtpClient != null, "rtpClient != null");
			rtpClient.Connect(new IPEndPoint(sourceAddress, rtpChannelNumber));
			Debug.Assert(rtcpClient != null, "rtcpClient != null");
			rtcpClient.Connect(new IPEndPoint(sourceAddress, rtcpChannelNumber));
			ArraySegment<byte> udpHolePunchingPacketSegment = new ArraySegment<byte>(Array.Empty<byte>());
			await rtpClient.SendAsync(udpHolePunchingPacketSegment, SocketFlags.None);
			await rtcpClient.SendAsync(udpHolePunchingPacketSegment, SocketFlags.None);
			_udpClientsMap[rtpChannelNumber] = rtpClient;
			_udpClientsMap[rtcpChannelNumber] = rtcpClient;
		}
		ParseSessionHeader(setupResponse.Headers[WellKnownHeaders.Session]);
		IMediaPayloadParser mediaPayloadParser = MediaPayloadParser.CreateFrom(track.Codec);
		IRtpSequenceAssembler rtpSequenceAssembler;
		if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
		{
			rtpSequenceAssembler = null;
			mediaPayloadParser.FrameGenerated = OnFrameGeneratedLockfree;
		}
		else
		{
			rtpSequenceAssembler = new RtpSequenceAssembler(2048, 256);
			mediaPayloadParser.FrameGenerated = OnFrameGeneratedThreadSafe;
		}
		RtpStream rtpStream = new RtpStream(mediaPayloadParser, track.SamplesFrequency, rtpSequenceAssembler);
		_streamsMap.Add(rtpChannelNumber, rtpStream);
		RtcpStream rtcpStream = new RtcpStream();
		rtcpStream.SessionShutdown += delegate
		{
			_serverCancellationTokenSource.Cancel();
		};
		_streamsMap.Add(rtcpChannelNumber, rtcpStream);
		uint senderSyncSourceId = (uint)_random.Next();
		RtcpReceiverReportsProvider rtcpReportsProvider = new RtcpReceiverReportsProvider(rtpStream, rtcpStream, senderSyncSourceId);
		_reportProvidersMap.Add(rtpChannelNumber, rtcpReportsProvider);
	}

	private async Task SendRtspKeepAliveAsync(CancellationToken token)
	{
		RtspRequestMessage getParameterRequest = _requestMessageFactory.CreateGetParameterRequest();
		if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
		{
			await _rtspTransportClient.SendRequestAsync(getParameterRequest, token);
		}
		else
		{
			await _rtspTransportClient.EnsureExecuteRequest(getParameterRequest, token);
		}
	}

	private async Task CloseRtspSessionAsync(CancellationToken token)
	{
		RtspRequestMessage teardownRequest = _requestMessageFactory.CreateTeardownRequest();
		if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
		{
			await _rtspTransportClient.SendRequestAsync(teardownRequest, token);
		}
		else
		{
			await _rtspTransportClient.EnsureExecuteRequest(teardownRequest, token);
		}
	}

	private IEnumerable<RtspMediaTrackInfo> GetTracksToSetup(IEnumerable<RtspTrackInfo> tracks)
	{
		foreach (RtspMediaTrackInfo track in tracks.OfType<RtspMediaTrackInfo>())
		{
			if (track.Codec is VideoCodecInfo && (_connectionParameters.RequiredTracks & RequiredTracks.Video) != 0)
			{
				yield return track;
			}
			else if (track.Codec is AudioCodecInfo && (_connectionParameters.RequiredTracks & RequiredTracks.Audio) != 0)
			{
				yield return track;
			}
		}
	}

	private void ParsePublicHeader(string publicHeader)
	{
		if (!string.IsNullOrEmpty(publicHeader))
		{
			string value = RtspMethod.GET_PARAMETER.ToString();
			if (publicHeader.IndexOf(value, StringComparison.InvariantCulture) != -1)
			{
				_isServerSupportsGetParameterRequest = true;
			}
		}
	}

	private void ParseSessionHeader(string sessionHeader)
	{
		uint timeout = 0u;
		if (!string.IsNullOrEmpty(sessionHeader))
		{
			int num = sessionHeader.IndexOf(';');
			if (num != -1)
			{
				TryParseTimeoutParameter(sessionHeader, out timeout);
				_requestMessageFactory.SessionId = sessionHeader.Substring(0, num);
			}
			else
			{
				_requestMessageFactory.SessionId = sessionHeader;
			}
		}
		if (timeout == 0)
		{
			timeout = 60u;
		}
		_rtspKeepAliveTimeoutMs = (int)(timeout * 1000);
	}

	private bool TryParseSeverPorts(string portsAttribute, out int rtpPort, out int rtcpPort)
	{
		rtpPort = 0;
		rtcpPort = 0;
		int num = portsAttribute.IndexOf('=');
		if (num == -1)
		{
			return false;
		}
		int num2 = ++num;
		if (num2 == portsAttribute.Length)
		{
			return false;
		}
		while (portsAttribute[num2] == ' ')
		{
			if (++num2 == portsAttribute.Length)
			{
				return false;
			}
		}
		int num3 = portsAttribute.IndexOf('-', num);
		if (num3 == -1)
		{
			return false;
		}
		string s = portsAttribute.Substring(num2, num3 - num2);
		if (!int.TryParse(s, out rtpPort))
		{
			return false;
		}
		int num4 = ++num3;
		if (num4 == portsAttribute.Length)
		{
			return false;
		}
		int num5 = num4;
		while (portsAttribute[num5] != ';' && ++num5 != portsAttribute.Length)
		{
		}
		string s2 = portsAttribute.Substring(num4, num5 - num4);
		return int.TryParse(s2, out rtcpPort);
	}

	private static void TryParseTimeoutParameter(string sessionHeader, out uint timeout)
	{
		timeout = 0u;
		int num = sessionHeader.IndexOf(';');
		if (num == -1)
		{
			return;
		}
		int num2 = sessionHeader.IndexOf("timeout", ++num, StringComparison.InvariantCultureIgnoreCase);
		if (num2 == -1)
		{
			return;
		}
		num2 += "timeout".Length;
		int num3 = sessionHeader.IndexOf('=', num2);
		if (num3 == -1)
		{
			return;
		}
		int num4 = ++num3;
		if (num4 == sessionHeader.Length)
		{
			return;
		}
		while (sessionHeader[num4] == ' ' || sessionHeader[num4] == '"')
		{
			if (++num4 == sessionHeader.Length)
			{
				return;
			}
		}
		int num5 = num4;
		while (sessionHeader[num5] >= '0' && sessionHeader[num5] <= '9' && ++num5 != sessionHeader.Length)
		{
		}
		string s = sessionHeader.Substring(num4, num5 - num4);
		uint.TryParse(s, out timeout);
	}

	private void OnFrameGeneratedLockfree(RawFrame frame)
	{
		FrameReceived?.Invoke(frame);
	}

	private void OnFrameGeneratedThreadSafe(RawFrame frame)
	{
		if (FrameReceived == null)
		{
			return;
		}
		_hybridLock.Enter();
		try
		{
			FrameReceived(frame);
		}
		finally
		{
			_hybridLock.Leave();
		}
	}

	private async Task ReceiveOverTcpAsync(Stream rtspStream, CancellationToken token)
	{
		_tpktStream = new TpktStream(rtspStream);
		int nextRtcpReportInterval = GetNextRtcpReportIntervalMs();
		int lastTimeRtcpReportsSent = Environment.TickCount;
		MemoryStream bufferStream = new MemoryStream();
		while (!token.IsCancellationRequested)
		{
			TpktPayload payload = await _tpktStream.ReadAsync();
			if (_streamsMap.TryGetValue(payload.Channel, out var stream))
			{
				stream.Process(payload.PayloadSegment);
			}
			int ticksNow = Environment.TickCount;
			if (!TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsSent, nextRtcpReportInterval))
			{
				continue;
			}
			lastTimeRtcpReportsSent = ticksNow;
			nextRtcpReportInterval = GetNextRtcpReportIntervalMs();
			foreach (KeyValuePair<int, RtcpReceiverReportsProvider> pair in _reportProvidersMap)
			{
				IEnumerable<RtcpPacket> packets = pair.Value.GetReportPackets();
				ArraySegment<byte> byteSegment = SerializeRtcpPackets(packets, bufferStream);
				int rtcpChannel = pair.Key + 1;
				await _tpktStream.WriteAsync(rtcpChannel, byteSegment);
			}
			stream = null;
		}
	}

	private Task ReceiveOverUdpAsync(CancellationToken token)
	{
		List<Task> list = new List<Task>(_udpClientsMap.Count / 2);
		foreach (KeyValuePair<int, Socket> item2 in _udpClientsMap)
		{
			int key = item2.Key;
			Socket value = item2.Value;
			ITransportStream transportStream = _streamsMap[key];
			Task item;
			if (transportStream is RtpStream rtpStream)
			{
				RtcpReceiverReportsProvider reportsProvider = _reportProvidersMap[key];
				item = ReceiveRtpFromUdpAsync(value, rtpStream, reportsProvider, token);
			}
			else
			{
				item = ReceiveRtcpFromUdpAsync(value, transportStream, token);
			}
			list.Add(item);
		}
		return Task.WhenAll(list);
	}

	private async Task ReceiveRtpFromUdpAsync(Socket client, RtpStream rtpStream, RtcpReceiverReportsProvider reportsProvider, CancellationToken token)
	{
		byte[] readBuffer = new byte[2048];
		ArraySegment<byte> bufferSegment = new ArraySegment<byte>(readBuffer);
		int nextRtcpReportInterval = GetNextRtcpReportIntervalMs();
		int lastTimeRtcpReportsSent = Environment.TickCount;
		MemoryStream bufferStream = new MemoryStream();
		while (!token.IsCancellationRequested)
		{
			ArraySegment<byte> payloadSegment = new ArraySegment<byte>(readBuffer, 0, await client.ReceiveAsync(bufferSegment, SocketFlags.None));
			rtpStream.Process(payloadSegment);
			int ticksNow = Environment.TickCount;
			if (TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsSent, nextRtcpReportInterval))
			{
				lastTimeRtcpReportsSent = ticksNow;
				nextRtcpReportInterval = GetNextRtcpReportIntervalMs();
				IEnumerable<RtcpPacket> packets = reportsProvider.GetReportPackets();
				ArraySegment<byte> byteSegment = SerializeRtcpPackets(packets, bufferStream);
				await client.SendAsync(byteSegment, SocketFlags.None);
			}
		}
	}

	private static async Task ReceiveRtcpFromUdpAsync(Socket client, ITransportStream stream, CancellationToken token)
	{
		byte[] readBuffer = new byte[2048];
		ArraySegment<byte> bufferSegment = new ArraySegment<byte>(readBuffer);
		while (!token.IsCancellationRequested)
		{
			ArraySegment<byte> payloadSegment = new ArraySegment<byte>(readBuffer, 0, await client.ReceiveAsync(bufferSegment, SocketFlags.None));
			stream.Process(payloadSegment);
		}
	}

	private ArraySegment<byte> SerializeRtcpPackets(IEnumerable<RtcpPacket> packets, MemoryStream bufferStream)
	{
		bufferStream.Position = 0L;
		foreach (ISerializablePacket item in packets.Cast<ISerializablePacket>())
		{
			item.Serialize(bufferStream);
		}
		byte[] buffer = bufferStream.GetBuffer();
		return new ArraySegment<byte>(buffer, 0, (int)bufferStream.Position);
	}
}
