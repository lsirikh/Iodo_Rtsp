#define DEBUG
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Tpkt;

internal class TpktStream
{
	private static readonly byte[] TpktHeaderIdArray = new byte[1] { 36 };

	private byte[] _readBuffer = new byte[8192];

	private byte[] _writeBuffer = new byte[0];

	private int _nonParsedDataSize;

	private int _nonParsedDataOffset;

	private readonly Stream _stream;

	public TpktStream(Stream stream)
	{
		_stream = stream ?? throw new ArgumentNullException("stream");
	}

	public async Task<TpktPayload> ReadAsync()
	{
		int nextTpktPositon = await FindNextPacketAsync();
		int usefulDataSize2 = _nonParsedDataSize - nextTpktPositon;
		if (nextTpktPositon != 0)
		{
			Buffer.BlockCopy(_readBuffer, nextTpktPositon, _readBuffer, 0, usefulDataSize2);
		}
		int readCount2 = 4 - usefulDataSize2;
		if (readCount2 > 0)
		{
			await _stream.ReadExactAsync(_readBuffer, usefulDataSize2, readCount2);
			usefulDataSize2 = 0;
		}
		else
		{
			usefulDataSize2 = -readCount2;
		}
		int channel = _readBuffer[1];
		int payloadSize = BigEndianConverter.ReadUInt16(_readBuffer, 2);
		int totalSize = 4 + payloadSize;
		if (_readBuffer.Length < totalSize)
		{
			Array.Resize(newSize: SystemMemory.RoundToPageAlignmentSize(totalSize), array: ref _readBuffer);
		}
		readCount2 = payloadSize - usefulDataSize2;
		if (readCount2 > 0)
		{
			await _stream.ReadExactAsync(_readBuffer, 4 + usefulDataSize2, readCount2);
			_nonParsedDataSize = 0;
		}
		else
		{
			_nonParsedDataSize = -readCount2;
			_nonParsedDataOffset = totalSize;
		}
		ArraySegment<byte> payloadSegment = new ArraySegment<byte>(_readBuffer, 4, payloadSize);
		return new TpktPayload(channel, payloadSegment);
	}

	public async Task WriteAsync(int channel, ArraySegment<byte> payloadSegment)
	{
		Debug.Assert(payloadSegment.Array != null, "payloadSegment.Array != null");
		int packetSize = 4 + payloadSegment.Count;
		if (_writeBuffer.Length < packetSize)
		{
			_writeBuffer = new byte[packetSize];
			_writeBuffer[0] = 36;
		}
		_writeBuffer[1] = (byte)channel;
		_writeBuffer[2] = (byte)(payloadSegment.Count >> 8);
		_writeBuffer[3] = (byte)payloadSegment.Count;
		Buffer.BlockCopy(payloadSegment.Array, payloadSegment.Offset, _writeBuffer, 4, payloadSegment.Count);
		await _stream.WriteAsync(_writeBuffer, 0, packetSize);
	}

	private async Task<int> FindNextPacketAsync()
	{
		if (_nonParsedDataSize != 0)
		{
			Buffer.BlockCopy(_readBuffer, _nonParsedDataOffset, _readBuffer, 0, _nonParsedDataSize);
		}
		int packetPosition;
		while (true)
		{
			int num;
			packetPosition = (num = FindTpktSignature(_nonParsedDataSize));
			if (num != -1)
			{
				break;
			}
			_nonParsedDataSize = await _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length);
			if (_nonParsedDataSize == 0)
			{
				throw new EndOfStreamException("End of TPKT stream");
			}
		}
		return packetPosition;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int FindTpktSignature(int dataSize)
	{
		if (dataSize == 0)
		{
			return -1;
		}
		if (_readBuffer[0] == 36)
		{
			return 0;
		}
		if (dataSize == 1)
		{
			return -1;
		}
		return ArrayUtils.IndexOfBytes(_readBuffer, TpktHeaderIdArray, 1, --dataSize);
	}
}
