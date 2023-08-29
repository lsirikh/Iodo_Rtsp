using System;
using System.Collections.Generic;
using System.Net;

namespace Iodo.Rtsp.Rtsp.Authentication;

internal abstract class Authenticator
{
	public NetworkCredential Credentials { get; }

	protected Authenticator(NetworkCredential credentials)
	{
		Credentials = credentials ?? throw new ArgumentNullException("credentials");
	}

	public abstract string GetResponse(uint nonceCounter, string uri, string method, byte[] entityBodyBytes);

	public static Authenticator Create(NetworkCredential credential, string authenticateHeader)
	{
		authenticateHeader = authenticateHeader ?? throw new ArgumentNullException("authenticateHeader");
		if (authenticateHeader.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
		{
			return new BasicAuthenticator(credential);
		}
		if (authenticateHeader.StartsWith("Digest", StringComparison.OrdinalIgnoreCase))
		{
			int num = authenticateHeader.IndexOf(' ');
			if (num != -1)
			{
				string parameters = authenticateHeader.Substring(++num);
				Dictionary<string, string> dictionary = ParseParameters(parameters);
				if (!dictionary.TryGetValue("REALM", out var value))
				{
					throw new ArgumentException("\"realm\" parameter is not found");
				}
				if (!dictionary.TryGetValue("NONCE", out var value2))
				{
					throw new ArgumentException("\"nonce\" parameter is not found");
				}
				dictionary.TryGetValue("QOP", out var value3);
				return new DigestAuthenticator(credential, value, value2, value3);
			}
		}
		throw new ArgumentOutOfRangeException(authenticateHeader, "Invalid authenticate header: " + authenticateHeader);
	}

	private static Dictionary<string, string> ParseParameters(string parameters)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		int num = 0;
		while (num < parameters.Length)
		{
			int num2 = parameters.IndexOf('=', num);
			if (num2 == -1)
			{
				break;
			}
			int length = num2 - num;
			string key = parameters.Substring(num, length).Trim().ToUpperInvariant();
			num2++;
			int num3 = num2;
			if (num3 == parameters.Length)
			{
				break;
			}
			while (parameters[num3] == ' ' && ++num3 != parameters.Length)
			{
			}
			int num4;
			int num5;
			if (parameters[num3] == '"')
			{
				num4 = parameters.IndexOf('"', num2);
				if (num4 == -1)
				{
					break;
				}
				num4++;
				num5 = parameters.IndexOf('"', num4);
				if (num5 == -1)
				{
					break;
				}
				int num6 = parameters.IndexOf(',', num5 + 1);
				num = ((num6 == -1) ? parameters.Length : (++num6));
			}
			else
			{
				num4 = num3;
				int num6 = parameters.IndexOf(',', ++num3);
				if (num6 != -1)
				{
					num5 = num6;
					num = ++num6;
				}
				else
				{
					num5 = parameters.Length;
					num = num5;
				}
			}
			int length2 = num5 - num4;
			string value = parameters.Substring(num4, length2);
			dictionary[key] = value;
		}
		return dictionary;
	}
}
