using System;
using System.Net;
using System.Text;

namespace Iodo.Rtsp.Rtsp.Authentication;

internal class DigestAuthenticator : Authenticator
{
	private readonly string _realm;

	private readonly string _nonce;

	private readonly string _qop;

	private readonly string _cnonce;

	public DigestAuthenticator(NetworkCredential credentials, string realm, string nonce, string qop)
		: base(credentials)
	{
		_realm = realm ?? throw new ArgumentNullException("realm");
		_nonce = nonce ?? throw new ArgumentNullException("nonce");
		if (qop != null)
		{
			int num = qop.IndexOf(',');
			_qop = ((num != -1) ? qop.Substring(0, num) : qop);
		}
		_cnonce = ((uint)Guid.NewGuid().GetHashCode()).ToString("X8");
	}

	public override string GetResponse(uint nonceCounter, string uri, string method, byte[] entityBodyBytes)
	{
		string hashHexValues = MD5.GetHashHexValues(base.Credentials.UserName + ":" + _realm + ":" + base.Credentials.Password);
		string text = method + ":" + uri;
		bool flag = !string.IsNullOrEmpty(_qop);
		if (flag && _qop.Equals("auth-int", StringComparison.InvariantCultureIgnoreCase))
		{
			text = text + ":" + MD5.GetHashHexValues(entityBodyBytes);
		}
		string hashHexValues2 = MD5.GetHashHexValues(text);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", ", base.Credentials.UserName, _realm, _nonce, uri);
		if (!flag)
		{
			string hashHexValues3 = MD5.GetHashHexValues(hashHexValues + ":" + _nonce + ":" + hashHexValues2);
			stringBuilder.AppendFormat("response=\"{0}\"", hashHexValues3);
		}
		else
		{
			string hashHexValues3 = MD5.GetHashHexValues(hashHexValues + ":" + _nonce + ":" + nonceCounter.ToString("X8") + ":" + _cnonce + ":" + _qop + ":" + hashHexValues2);
			stringBuilder.AppendFormat("response=\"{0}\", cnonce=\"{1}\", nc=\"{2:X8}\", qop=\"{3}\"", hashHexValues3, _cnonce, nonceCounter, _qop);
		}
		return stringBuilder.ToString();
	}
}
