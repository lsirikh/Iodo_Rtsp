using System;

namespace Iodo.Rtsp.Utils;

internal class BitStreamReader
{
	private byte[] _buffer;

	private int _count;

	private int _startOffset;

	private int _bitsPosition;

	public void ReInitialize(ArraySegment<byte> byteSegment)
	{
		_buffer = byteSegment.Array;
		_startOffset = byteSegment.Offset;
		_bitsPosition = 0;
		_count = byteSegment.Count;
	}

	public int ReadBit()
	{
		int num = _bitsPosition / 8;
		if (num == _count)
		{
			return -1;
		}
		int num2 = 7 - _bitsPosition % 8;
		int result = (_buffer[_startOffset + num] >> num2) & 1;
		_bitsPosition++;
		return result;
	}

	public int ReadBits(int count)
	{
		if (count > 32)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		int num = 0;
		while (count > 0)
		{
			num <<= 1;
			int num2 = ReadBit();
			if (num2 == -1)
			{
				return num2;
			}
			num |= num2;
			count--;
		}
		return num;
	}

	public int ReadUe()
	{
		int i;
		for (i = 0; i < 31; i++)
		{
			int num = ReadBit();
			switch (num)
			{
			case -1:
				return num;
			case 0:
				continue;
			}
			break;
		}
		if (i > 0)
		{
			long num2 = ReadBits(i);
			if (num2 == -1)
			{
				return -1;
			}
			return (int)((1 << i) - 1 + num2);
		}
		return 0;
	}
}
