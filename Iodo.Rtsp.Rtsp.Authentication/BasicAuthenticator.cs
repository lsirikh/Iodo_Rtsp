using System;
using System.Net;
using System.Text;

namespace Iodo.Rtsp.Rtsp.Authentication;

internal class BasicAuthenticator : Authenticator
{
	public BasicAuthenticator(NetworkCredential credentials)
		: base(credentials)
	{
	}

	public override string GetResponse(uint nonceCounter, string uri, string method, byte[] entityBodyBytes)
	{
		string s = base.Credentials.UserName + ":" + base.Credentials.Password;
		return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
	}
}
