namespace Iodo.Rtsp.Utils;

internal static class SystemMemory
{
	private const int SystemPageSize = 4096;

	public static int RoundToPageAlignmentSize(int size)
	{
		return (size + 4096 - 1) / 4096 * 4096;
	}
}
