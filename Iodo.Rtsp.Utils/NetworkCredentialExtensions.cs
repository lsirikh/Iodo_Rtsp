using System.Net;

namespace Iodo.Rtsp.Utils;

internal static class NetworkCredentialExtensions
{
	public static bool IsEmpty(this NetworkCredential networkCredential)
	{
		if (string.IsNullOrEmpty(networkCredential.UserName) || networkCredential.Password == null)
		{
			return true;
		}
		return false;
	}
}
