using System;
using System.Linq;

namespace Iodo.Rtsp.Utils;

internal static class Hex
{
	public static byte[] StringToByteArray(string hex)
	{
		if (hex == null)
		{
			throw new ArgumentNullException("hex");
		}
		if (hex.Length == 0)
		{
			return Array.Empty<byte>();
		}
		return (from x in Enumerable.Range(0, hex.Length)
			where x % 2 == 0
			select Convert.ToByte(hex.Substring(x, 2), 16)).ToArray();
	}
}
