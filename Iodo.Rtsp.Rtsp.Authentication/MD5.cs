using System;
using System.Text;

namespace Iodo.Rtsp.Rtsp.Authentication;

internal static class MD5
{
	private struct ABCDStruct
	{
		public uint A;

		public uint B;

		public uint C;

		public uint D;
	}

	public static string GetHashHexValues(string value)
	{
		byte[] hash = GetHash(value);
		return ToHexString(hash);
	}

	public static string GetHashHexValues(byte[] value)
	{
		byte[] hash = GetHash(value);
		return ToHexString(hash);
	}

	public static byte[] GetHash(string input, Encoding encoding)
	{
		if (input == null)
		{
			throw new ArgumentNullException("input");
		}
		if (encoding == null)
		{
			throw new ArgumentNullException("encoding");
		}
		byte[] bytes = encoding.GetBytes(input);
		return GetHash(bytes);
	}

	public static byte[] GetHash(string input)
	{
		return GetHash(input, Encoding.UTF8);
	}

	public static byte[] GetHash(byte[] input)
	{
		if (input == null)
		{
			throw new ArgumentNullException("input");
		}
		ABCDStruct aBCDStruct = default(ABCDStruct);
		aBCDStruct.A = 1732584193u;
		aBCDStruct.B = 4023233417u;
		aBCDStruct.C = 2562383102u;
		aBCDStruct.D = 271733878u;
		ABCDStruct abcdValue = aBCDStruct;
		int i;
		for (i = 0; i <= input.Length - 64; i += 64)
		{
			GetHashBlock(input, ref abcdValue, i);
		}
		return GetHashFinalBlock(input, i, input.Length - i, abcdValue, (long)input.Length * 8L);
	}

	private static string ToHexString(byte[] hashBytes, bool upperCase = false)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = (upperCase ? "X2" : "x2");
		foreach (byte b in hashBytes)
		{
			stringBuilder.Append(b.ToString(text));
		}
		return stringBuilder.ToString();
	}

	private static byte[] GetHashFinalBlock(byte[] input, int ibStart, int cbSize, ABCDStruct abcd, long len)
	{
		byte[] array = new byte[64];
		byte[] bytes = BitConverter.GetBytes(len);
		Array.Copy(input, ibStart, array, 0, cbSize);
		array[cbSize] = 128;
		if (cbSize < 56)
		{
			Array.Copy(bytes, 0, array, 56, 8);
			GetHashBlock(array, ref abcd, 0);
		}
		else
		{
			GetHashBlock(array, ref abcd, 0);
			array = new byte[64];
			Array.Copy(bytes, 0, array, 56, 8);
			GetHashBlock(array, ref abcd, 0);
		}
		byte[] array2 = new byte[16];
		Array.Copy(BitConverter.GetBytes(abcd.A), 0, array2, 0, 4);
		Array.Copy(BitConverter.GetBytes(abcd.B), 0, array2, 4, 4);
		Array.Copy(BitConverter.GetBytes(abcd.C), 0, array2, 8, 4);
		Array.Copy(BitConverter.GetBytes(abcd.D), 0, array2, 12, 4);
		return array2;
	}

	private static void GetHashBlock(byte[] input, ref ABCDStruct abcdValue, int ibStart)
	{
		uint[] array = Converter(input, ibStart);
		uint a = abcdValue.A;
		uint b = abcdValue.B;
		uint c = abcdValue.C;
		uint d = abcdValue.D;
		a = R1(a, b, c, d, array[0], 7, 3614090360u);
		d = R1(d, a, b, c, array[1], 12, 3905402710u);
		c = R1(c, d, a, b, array[2], 17, 606105819u);
		b = R1(b, c, d, a, array[3], 22, 3250441966u);
		a = R1(a, b, c, d, array[4], 7, 4118548399u);
		d = R1(d, a, b, c, array[5], 12, 1200080426u);
		c = R1(c, d, a, b, array[6], 17, 2821735955u);
		b = R1(b, c, d, a, array[7], 22, 4249261313u);
		a = R1(a, b, c, d, array[8], 7, 1770035416u);
		d = R1(d, a, b, c, array[9], 12, 2336552879u);
		c = R1(c, d, a, b, array[10], 17, 4294925233u);
		b = R1(b, c, d, a, array[11], 22, 2304563134u);
		a = R1(a, b, c, d, array[12], 7, 1804603682u);
		d = R1(d, a, b, c, array[13], 12, 4254626195u);
		c = R1(c, d, a, b, array[14], 17, 2792965006u);
		b = R1(b, c, d, a, array[15], 22, 1236535329u);
		a = R2(a, b, c, d, array[1], 5, 4129170786u);
		d = R2(d, a, b, c, array[6], 9, 3225465664u);
		c = R2(c, d, a, b, array[11], 14, 643717713u);
		b = R2(b, c, d, a, array[0], 20, 3921069994u);
		a = R2(a, b, c, d, array[5], 5, 3593408605u);
		d = R2(d, a, b, c, array[10], 9, 38016083u);
		c = R2(c, d, a, b, array[15], 14, 3634488961u);
		b = R2(b, c, d, a, array[4], 20, 3889429448u);
		a = R2(a, b, c, d, array[9], 5, 568446438u);
		d = R2(d, a, b, c, array[14], 9, 3275163606u);
		c = R2(c, d, a, b, array[3], 14, 4107603335u);
		b = R2(b, c, d, a, array[8], 20, 1163531501u);
		a = R2(a, b, c, d, array[13], 5, 2850285829u);
		d = R2(d, a, b, c, array[2], 9, 4243563512u);
		c = R2(c, d, a, b, array[7], 14, 1735328473u);
		b = R2(b, c, d, a, array[12], 20, 2368359562u);
		a = R3(a, b, c, d, array[5], 4, 4294588738u);
		d = R3(d, a, b, c, array[8], 11, 2272392833u);
		c = R3(c, d, a, b, array[11], 16, 1839030562u);
		b = R3(b, c, d, a, array[14], 23, 4259657740u);
		a = R3(a, b, c, d, array[1], 4, 2763975236u);
		d = R3(d, a, b, c, array[4], 11, 1272893353u);
		c = R3(c, d, a, b, array[7], 16, 4139469664u);
		b = R3(b, c, d, a, array[10], 23, 3200236656u);
		a = R3(a, b, c, d, array[13], 4, 681279174u);
		d = R3(d, a, b, c, array[0], 11, 3936430074u);
		c = R3(c, d, a, b, array[3], 16, 3572445317u);
		b = R3(b, c, d, a, array[6], 23, 76029189u);
		a = R3(a, b, c, d, array[9], 4, 3654602809u);
		d = R3(d, a, b, c, array[12], 11, 3873151461u);
		c = R3(c, d, a, b, array[15], 16, 530742520u);
		b = R3(b, c, d, a, array[2], 23, 3299628645u);
		a = R4(a, b, c, d, array[0], 6, 4096336452u);
		d = R4(d, a, b, c, array[7], 10, 1126891415u);
		c = R4(c, d, a, b, array[14], 15, 2878612391u);
		b = R4(b, c, d, a, array[5], 21, 4237533241u);
		a = R4(a, b, c, d, array[12], 6, 1700485571u);
		d = R4(d, a, b, c, array[3], 10, 2399980690u);
		c = R4(c, d, a, b, array[10], 15, 4293915773u);
		b = R4(b, c, d, a, array[1], 21, 2240044497u);
		a = R4(a, b, c, d, array[8], 6, 1873313359u);
		d = R4(d, a, b, c, array[15], 10, 4264355552u);
		c = R4(c, d, a, b, array[6], 15, 2734768916u);
		b = R4(b, c, d, a, array[13], 21, 1309151649u);
		a = R4(a, b, c, d, array[4], 6, 4149444226u);
		d = R4(d, a, b, c, array[11], 10, 3174756917u);
		c = R4(c, d, a, b, array[2], 15, 718787259u);
		b = R4(b, c, d, a, array[9], 21, 3951481745u);
		abcdValue.A = a + abcdValue.A;
		abcdValue.B = b + abcdValue.B;
		abcdValue.C = c + abcdValue.C;
		abcdValue.D = d + abcdValue.D;
	}

	private static uint R1(uint a, uint b, uint c, uint d, uint x, int s, uint t)
	{
		return b + Lsr(a + ((b & c) | ((b ^ 0xFFFFFFFFu) & d)) + x + t, s);
	}

	private static uint R2(uint a, uint b, uint c, uint d, uint x, int s, uint t)
	{
		return b + Lsr(a + ((b & d) | (c & (d ^ 0xFFFFFFFFu))) + x + t, s);
	}

	private static uint R3(uint a, uint b, uint c, uint d, uint x, int s, uint t)
	{
		return b + Lsr(a + (b ^ c ^ d) + x + t, s);
	}

	private static uint R4(uint a, uint b, uint c, uint d, uint x, int s, uint t)
	{
		return b + Lsr(a + (c ^ (b | (d ^ 0xFFFFFFFFu))) + x + t, s);
	}

	private static uint Lsr(uint i, int s)
	{
		return (i << s) | (i >> 32 - s);
	}

	private static uint[] Converter(byte[] input, int ibStart)
	{
		uint[] array = new uint[16];
		for (int i = 0; i < 16; i++)
		{
			array[i] = input[ibStart + i * 4];
			array[i] += (uint)(input[ibStart + i * 4 + 1] << 8);
			array[i] += (uint)(input[ibStart + i * 4 + 2] << 16);
			array[i] += (uint)(input[ibStart + i * 4 + 3] << 24);
		}
		return array;
	}
}
