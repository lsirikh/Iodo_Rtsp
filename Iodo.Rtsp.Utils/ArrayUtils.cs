namespace Iodo.Rtsp.Utils;

internal static class ArrayUtils
{
	public static bool IsBytesEquals(byte[] bytes1, int offset1, int count1, byte[] bytes2, int offset2, int count2)
	{
		if (count1 != count2)
		{
			return false;
		}
		for (int i = 0; i < count1; i++)
		{
			if (bytes1[offset1 + i] != bytes2[offset2 + i])
			{
				return false;
			}
		}
		return true;
	}

	public static bool StartsWith(byte[] array, int offset, int count, byte[] pattern)
	{
		int num = pattern.Length;
		if (count < num)
		{
			return false;
		}
		int num2 = 0;
		while (num2 < num)
		{
			if (array[offset] != pattern[num2])
			{
				return false;
			}
			num2++;
			offset++;
		}
		return true;
	}

	public static bool EndsWith(byte[] array, int offset, int count, byte[] pattern)
	{
		int num = pattern.Length;
		if (count < num)
		{
			return false;
		}
		offset = offset + count - num;
		int num2 = 0;
		while (num2 < num)
		{
			if (array[offset] != pattern[num2])
			{
				return false;
			}
			num2++;
			offset++;
		}
		return true;
	}

	public static int IndexOfBytes(byte[] array, byte[] pattern, int startIndex, int count)
	{
		int num = pattern.Length;
		if (count < num)
		{
			return -1;
		}
		int num2 = startIndex + count;
		int num3 = 0;
		while (startIndex < num2)
		{
			if (array[startIndex] != pattern[num3])
			{
				num3 = 0;
			}
			else if (++num3 == num)
			{
				return startIndex - num3 + 1;
			}
			startIndex++;
		}
		return -1;
	}
}
