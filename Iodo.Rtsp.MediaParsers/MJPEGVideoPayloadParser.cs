#define DEBUG
using System;
using System.Diagnostics;
using System.IO;
using Iodo.Rtsp.RawFrames.Video;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.MediaParsers;

internal class MJPEGVideoPayloadParser : MediaPayloadParser
{
	private const int JpegHeaderSize = 8;

	private const int JpegMaxSize = 16777216;

	private static readonly ArraySegment<byte> JpegEndMarkerByteSegment = new ArraySegment<byte>(RawJpegFrame.EndMarkerBytes);

	private static readonly byte[] DefaultQuantizers = new byte[128]
	{
		16, 11, 12, 14, 12, 10, 16, 14, 13, 14,
		18, 17, 16, 19, 24, 40, 26, 24, 22, 22,
		24, 49, 35, 37, 29, 40, 58, 51, 61, 60,
		57, 51, 56, 55, 64, 72, 92, 78, 64, 68,
		87, 69, 55, 56, 80, 109, 81, 87, 95, 98,
		103, 104, 103, 62, 77, 113, 121, 112, 100, 120,
		92, 101, 103, 99, 17, 18, 18, 24, 21, 24,
		47, 26, 26, 47, 99, 66, 56, 66, 99, 99,
		99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
		99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
		99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
		99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
		99, 99, 99, 99, 99, 99, 99, 99
	};

	private static readonly byte[] LumDcCodelens = new byte[16]
	{
		0, 1, 5, 1, 1, 1, 1, 1, 1, 0,
		0, 0, 0, 0, 0, 0
	};

	private static readonly byte[] LumDcSymbols = new byte[12]
	{
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
		10, 11
	};

	private static readonly byte[] LumAcCodelens = new byte[16]
	{
		0, 2, 1, 3, 3, 2, 4, 3, 5, 5,
		4, 4, 0, 0, 1, 125
	};

	private static readonly byte[] LumAcSymbols = new byte[162]
	{
		1, 2, 3, 0, 4, 17, 5, 18, 33, 49,
		65, 6, 19, 81, 97, 7, 34, 113, 20, 50,
		129, 145, 161, 8, 35, 66, 177, 193, 21, 82,
		209, 240, 36, 51, 98, 114, 130, 9, 10, 22,
		23, 24, 25, 26, 37, 38, 39, 40, 41, 42,
		52, 53, 54, 55, 56, 57, 58, 67, 68, 69,
		70, 71, 72, 73, 74, 83, 84, 85, 86, 87,
		88, 89, 90, 99, 100, 101, 102, 103, 104, 105,
		106, 115, 116, 117, 118, 119, 120, 121, 122, 131,
		132, 133, 134, 135, 136, 137, 138, 146, 147, 148,
		149, 150, 151, 152, 153, 154, 162, 163, 164, 165,
		166, 167, 168, 169, 170, 178, 179, 180, 181, 182,
		183, 184, 185, 186, 194, 195, 196, 197, 198, 199,
		200, 201, 202, 210, 211, 212, 213, 214, 215, 216,
		217, 218, 225, 226, 227, 228, 229, 230, 231, 232,
		233, 234, 241, 242, 243, 244, 245, 246, 247, 248,
		249, 250
	};

	private static readonly byte[] ChmDcCodelens = new byte[16]
	{
		0, 3, 1, 1, 1, 1, 1, 1, 1, 1,
		1, 0, 0, 0, 0, 0
	};

	private static readonly byte[] ChmDcSymbols = new byte[12]
	{
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
		10, 11
	};

	private static readonly byte[] ChmAcCodelens = new byte[16]
	{
		0, 2, 1, 2, 4, 4, 3, 4, 7, 5,
		4, 4, 0, 1, 2, 119
	};

	private static readonly byte[] ChmAcSymbols = new byte[162]
	{
		0, 1, 2, 3, 17, 4, 5, 33, 49, 6,
		18, 65, 81, 7, 97, 113, 19, 34, 50, 129,
		8, 20, 66, 145, 161, 177, 193, 9, 35, 51,
		82, 240, 21, 98, 114, 209, 10, 22, 36, 52,
		225, 37, 241, 23, 24, 25, 26, 38, 39, 40,
		41, 42, 53, 54, 55, 56, 57, 58, 67, 68,
		69, 70, 71, 72, 73, 74, 83, 84, 85, 86,
		87, 88, 89, 90, 99, 100, 101, 102, 103, 104,
		105, 106, 115, 116, 117, 118, 119, 120, 121, 122,
		130, 131, 132, 133, 134, 135, 136, 137, 138, 146,
		147, 148, 149, 150, 151, 152, 153, 154, 162, 163,
		164, 165, 166, 167, 168, 169, 170, 178, 179, 180,
		181, 182, 183, 184, 185, 186, 194, 195, 196, 197,
		198, 199, 200, 201, 202, 210, 211, 212, 213, 214,
		215, 216, 217, 218, 226, 227, 228, 229, 230, 231,
		232, 233, 234, 242, 243, 244, 245, 246, 247, 248,
		249, 250
	};

	private readonly MemoryStream _frameStream;

	private int _currentDri;

	private int _currentQ;

	private int _currentType;

	private int _currentFrameWidth;

	private int _currentFrameHeight;

	private bool _hasExternalQuantizationTable;

	private byte[] _jpegHeaderBytes = new byte[0];

	private ArraySegment<byte> _jpegHeaderBytesSegment;

	private byte[] _quantizationTables = new byte[0];

	private int _quantizationTablesLength;

	public MJPEGVideoPayloadParser()
	{
		_frameStream = new MemoryStream(65536);
	}

	public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		if (byteSegment.Count < 8)
		{
			throw new MediaPayloadParserException("Input data size is smaller than JPEG header size");
		}
		int num = byteSegment.Offset + 1;
		int num2 = BigEndianConverter.ReadUInt24(byteSegment.Array, num);
		num += 3;
		int num3 = byteSegment.Array[num++];
		int num4 = byteSegment.Array[num++];
		int num5 = byteSegment.Array[num++] * 8;
		int num6 = byteSegment.Array[num++] * 8;
		int num7 = 0;
		if (num3 > 63)
		{
			num7 = BigEndianConverter.ReadUInt16(byteSegment.Array, num);
			num += 4;
		}
		if (num2 == 0)
		{
			if (_frameStream.Position != 0)
			{
				GenerateFrame(timeOffset);
			}
			bool flag = false;
			if (num4 > 127 && byteSegment.Array[num] == 0)
			{
				_hasExternalQuantizationTable = true;
				int num8 = BigEndianConverter.ReadUInt16(byteSegment.Array, num + 2);
				num += 4;
				if (!ArrayUtils.IsBytesEquals(byteSegment.Array, num, num8, _quantizationTables, 0, _quantizationTablesLength))
				{
					if (_quantizationTables.Length < num8)
					{
						_quantizationTables = new byte[num8];
					}
					Buffer.BlockCopy(byteSegment.Array, num, _quantizationTables, 0, num8);
					_quantizationTablesLength = num8;
					flag = true;
				}
				num += num8;
			}
			if (flag || _currentType != num3 || _currentQ != num4 || _currentFrameWidth != num5 || _currentFrameHeight != num6 || _currentDri != num7)
			{
				_currentType = num3;
				_currentQ = num4;
				_currentFrameWidth = num5;
				_currentFrameHeight = num6;
				_currentDri = num7;
				ReInitializeJpegHeader();
			}
			_frameStream.Write(_jpegHeaderBytesSegment.Array, _jpegHeaderBytesSegment.Offset, _jpegHeaderBytesSegment.Count);
		}
		if (num2 == 0 || _frameStream.Position != 0)
		{
			if (_frameStream.Position > 16777216)
			{
				throw new MediaPayloadParserException($"Jpeg frame is too large, more than {16} Mb");
			}
			int num9 = byteSegment.Offset + byteSegment.Count - num;
			if (num9 < 0)
			{
				throw new MediaPayloadParserException($"Invalid payload size: {num9}");
			}
			_frameStream.Write(byteSegment.Array, num, num9);
		}
	}

	public override void ResetState()
	{
		_frameStream.Position = 0L;
	}

	private void ReInitializeJpegHeader()
	{
		if (!_hasExternalQuantizationTable)
		{
			GenerateQuantizationTables(_currentQ);
		}
		int jpegHeaderSize = GetJpegHeaderSize(_currentDri);
		_jpegHeaderBytes = new byte[jpegHeaderSize];
		_jpegHeaderBytesSegment = new ArraySegment<byte>(_jpegHeaderBytes);
		FillJpegHeader(_jpegHeaderBytes, _currentType, _currentFrameWidth, _currentFrameHeight, _currentDri);
	}

	private void GenerateQuantizationTables(int factor)
	{
		_quantizationTablesLength = 128;
		if (_quantizationTables.Length < _quantizationTablesLength)
		{
			_quantizationTables = new byte[_quantizationTablesLength];
		}
		if (factor < 1)
		{
			factor = 1;
		}
		else if (factor > 99)
		{
			factor = 99;
		}
		int num = ((factor >= 50) ? (200 - factor * 2) : (5000 / factor));
		for (int i = 0; i < 128; i++)
		{
			int num2 = (DefaultQuantizers[i] * num + 50) / 100;
			if (num2 < 1)
			{
				num2 = 1;
			}
			else if (num2 > 255)
			{
				num2 = 255;
			}
			_quantizationTables[i] = (byte)num2;
		}
	}

	private int GetJpegHeaderSize(int dri)
	{
		int quantizationTablesLength = _quantizationTablesLength;
		int num = quantizationTablesLength / 2;
		quantizationTablesLength = num * 2;
		int num2 = ((quantizationTablesLength <= 64) ? 1 : 2);
		return 485 + num2 * 5 + quantizationTablesLength + ((dri > 0) ? 6 : 0);
	}

	private void FillJpegHeader(byte[] buffer, int type, int width, int height, int dri)
	{
		int num = ((_quantizationTablesLength <= 64) ? 1 : 2);
		int num2 = 0;
		buffer[num2++] = byte.MaxValue;
		buffer[num2++] = 216;
		buffer[num2++] = byte.MaxValue;
		buffer[num2++] = 224;
		buffer[num2++] = 0;
		buffer[num2++] = 16;
		buffer[num2++] = 74;
		buffer[num2++] = 70;
		buffer[num2++] = 73;
		buffer[num2++] = 70;
		buffer[num2++] = 0;
		buffer[num2++] = 1;
		buffer[num2++] = 1;
		buffer[num2++] = 0;
		buffer[num2++] = 0;
		buffer[num2++] = 1;
		buffer[num2++] = 0;
		buffer[num2++] = 1;
		buffer[num2++] = 0;
		buffer[num2++] = 0;
		if (dri > 0)
		{
			buffer[num2++] = byte.MaxValue;
			buffer[num2++] = 221;
			buffer[num2++] = 0;
			buffer[num2++] = 4;
			buffer[num2++] = (byte)(dri >> 8);
			buffer[num2++] = (byte)dri;
		}
		int num3 = ((num == 1) ? _quantizationTablesLength : (_quantizationTablesLength / 2));
		buffer[num2++] = byte.MaxValue;
		buffer[num2++] = 219;
		buffer[num2++] = 0;
		buffer[num2++] = (byte)(num3 + 3);
		buffer[num2++] = 0;
		int num4 = 0;
		Buffer.BlockCopy(_quantizationTables, num4, buffer, num2, num3);
		num4 += num3;
		num2 += num3;
		if (num > 1)
		{
			num3 = _quantizationTablesLength - _quantizationTablesLength / 2;
			buffer[num2++] = byte.MaxValue;
			buffer[num2++] = 219;
			buffer[num2++] = 0;
			buffer[num2++] = (byte)(num3 + 3);
			buffer[num2++] = 1;
			Buffer.BlockCopy(_quantizationTables, num4, buffer, num2, num3);
			num2 += num3;
		}
		buffer[num2++] = byte.MaxValue;
		buffer[num2++] = 192;
		buffer[num2++] = 0;
		buffer[num2++] = 17;
		buffer[num2++] = 8;
		buffer[num2++] = (byte)(height >> 8);
		buffer[num2++] = (byte)height;
		buffer[num2++] = (byte)(width >> 8);
		buffer[num2++] = (byte)width;
		buffer[num2++] = 3;
		buffer[num2++] = 1;
		buffer[num2++] = (byte)((((uint)type & (true ? 1u : 0u)) != 0) ? 34 : 33);
		buffer[num2++] = 0;
		buffer[num2++] = 2;
		buffer[num2++] = 17;
		buffer[num2++] = ((num != 1) ? ((byte)1) : ((byte)0));
		buffer[num2++] = 3;
		buffer[num2++] = 17;
		buffer[num2++] = ((num != 1) ? ((byte)1) : ((byte)0));
		CreateHuffmanHeader(buffer, num2, LumDcCodelens, LumDcCodelens.Length, LumDcSymbols, LumDcSymbols.Length, 0, 0);
		num2 += 5 + LumDcCodelens.Length + LumDcSymbols.Length;
		CreateHuffmanHeader(buffer, num2, LumAcCodelens, LumAcCodelens.Length, LumAcSymbols, LumAcSymbols.Length, 0, 1);
		num2 += 5 + LumAcCodelens.Length + LumAcSymbols.Length;
		CreateHuffmanHeader(buffer, num2, ChmDcCodelens, ChmDcCodelens.Length, ChmDcSymbols, ChmDcSymbols.Length, 1, 0);
		num2 += 5 + ChmDcCodelens.Length + ChmDcSymbols.Length;
		CreateHuffmanHeader(buffer, num2, ChmAcCodelens, ChmAcCodelens.Length, ChmAcSymbols, ChmAcSymbols.Length, 1, 1);
		num2 += 5 + ChmAcCodelens.Length + ChmAcSymbols.Length;
		buffer[num2++] = byte.MaxValue;
		buffer[num2++] = 218;
		buffer[num2++] = 0;
		buffer[num2++] = 12;
		buffer[num2++] = 3;
		buffer[num2++] = 1;
		buffer[num2++] = 0;
		buffer[num2++] = 2;
		buffer[num2++] = 17;
		buffer[num2++] = 3;
		buffer[num2++] = 17;
		buffer[num2++] = 0;
		buffer[num2++] = 63;
		buffer[num2] = 0;
	}

	private static void CreateHuffmanHeader(byte[] buffer, int offset, byte[] codelens, int ncodes, byte[] symbols, int nsymbols, int tableNo, int tableClass)
	{
		buffer[offset++] = byte.MaxValue;
		buffer[offset++] = 196;
		buffer[offset++] = 0;
		buffer[offset++] = (byte)(3 + ncodes + nsymbols);
		buffer[offset++] = (byte)((tableClass << 4) | tableNo);
		Buffer.BlockCopy(codelens, 0, buffer, offset, ncodes);
		offset += ncodes;
		Buffer.BlockCopy(symbols, 0, buffer, offset, nsymbols);
	}

	private void GenerateFrame(TimeSpan timeOffset)
	{
		if (!ArrayUtils.EndsWith(_frameStream.GetBuffer(), 0, (int)_frameStream.Position, RawJpegFrame.EndMarkerBytes))
		{
			MemoryStream frameStream = _frameStream;
			ArraySegment<byte> jpegEndMarkerByteSegment = JpegEndMarkerByteSegment;
			byte[]? array = jpegEndMarkerByteSegment.Array;
			jpegEndMarkerByteSegment = JpegEndMarkerByteSegment;
			int offset = jpegEndMarkerByteSegment.Offset;
			jpegEndMarkerByteSegment = JpegEndMarkerByteSegment;
			frameStream.Write(array, offset, jpegEndMarkerByteSegment.Count);
		}
		DateTime frameTimestamp = GetFrameTimestamp(timeOffset);
		ArraySegment<byte> frameSegment = new ArraySegment<byte>(_frameStream.GetBuffer(), 0, (int)_frameStream.Position);
		_frameStream.Position = 0L;
		RawJpegFrame e = new RawJpegFrame(frameTimestamp, frameSegment);
		OnFrameGenerated(e);
	}
}
