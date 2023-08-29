#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Iodo.Rtsp.Codecs;
using Iodo.Rtsp.Codecs.Audio;
using Iodo.Rtsp.Codecs.Video;
using Iodo.Rtsp.RawFrames.Video;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Sdp;

internal class SdpParser
{
	private class PayloadFormatInfo
	{
		public string TrackName { get; set; }

		public CodecInfo CodecInfo { get; set; }

		public int SamplesFrequency { get; set; }

		public PayloadFormatInfo(CodecInfo codecInfo, int samplesFrequency)
		{
			CodecInfo = codecInfo;
			SamplesFrequency = samplesFrequency;
		}
	}

	private readonly Dictionary<int, PayloadFormatInfo> _payloadFormatNumberToInfoMap = new Dictionary<int, PayloadFormatInfo>();

	private PayloadFormatInfo _lastParsedFormatInfo;

	public IEnumerable<RtspTrackInfo> Parse(ArraySegment<byte> payloadSegment)
	{
		Debug.Assert(payloadSegment.Array != null, "payloadSegment.Array != null");
		if (payloadSegment.Count == 0)
		{
			throw new ArgumentException("Empty SDP document", "payloadSegment");
		}
		_payloadFormatNumberToInfoMap.Clear();
		_lastParsedFormatInfo = null;
		MemoryStream stream = new MemoryStream(payloadSegment.Array, payloadSegment.Offset, payloadSegment.Count);
		StreamReader streamReader = new StreamReader(stream);
		string text;
		while (!string.IsNullOrEmpty(text = streamReader.ReadLine()))
		{
			if (text[0] == 'm')
			{
				ParseMediaLine(text);
			}
			else if (text[0] == 'a')
			{
				ParseAttributesLine(text);
			}
		}
		return from fi in _payloadFormatNumberToInfoMap.Values
			where fi.TrackName != null && fi.CodecInfo != null
			select new RtspMediaTrackInfo(fi.TrackName, fi.CodecInfo, fi.SamplesFrequency);
	}

	private void ParseMediaLine(string line)
	{
		string[] array = line.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 4)
		{
			_lastParsedFormatInfo = null;
			return;
		}
		string s = array[3];
		if (!int.TryParse(s, out var result))
		{
			_lastParsedFormatInfo = null;
			return;
		}
		CodecInfo codecInfo = TryCreateCodecInfo(result);
		int samplesFrequencyFromPayloadType = GetSamplesFrequencyFromPayloadType(result);
		_lastParsedFormatInfo = new PayloadFormatInfo(codecInfo, samplesFrequencyFromPayloadType);
		_payloadFormatNumberToInfoMap[result] = _lastParsedFormatInfo;
	}

	private void ParseAttributesLine(string line)
	{
		int num = line.IndexOf('=');
		if (num == -1)
		{
			return;
		}
		int num2 = line.IndexOf(':', num);
		if (num2 == -1)
		{
			return;
		}
		num++;
		int num3 = num2 - num;
		if (num3 == 0)
		{
			return;
		}
		string text = line.Substring(num, num3).Trim().ToUpperInvariant();
		num2++;
		if (num2 != line.Length)
		{
			string attributeValue = line.Substring(num2).TrimStart(Array.Empty<char>());
			switch (text)
			{
			case "RTPMAP":
				ParseRtpMapAttribute(attributeValue);
				break;
			case "CONTROL":
				ParseControlAttribute(attributeValue);
				break;
			case "FMTP":
				ParseFmtpAttribute(attributeValue);
				break;
			}
		}
	}

	private void ParseRtpMapAttribute(string attributeValue)
	{
		int num = attributeValue.IndexOf(' ');
		if (num < 1)
		{
			return;
		}
		string s = attributeValue.Substring(0, num);
		if (!int.TryParse(s, out var result))
		{
			return;
		}
		int num2 = num;
		while (attributeValue[num2] == ' ')
		{
			if (++num2 == attributeValue.Length)
			{
				return;
			}
		}
		int result2 = 0;
		int result3 = 0;
		int num3 = attributeValue.IndexOf('/', num2);
		string text;
		if (num3 == -1)
		{
			text = attributeValue.Substring(num2);
		}
		else
		{
			text = attributeValue.Substring(num2, num3 - num2);
			num3++;
			int num4 = attributeValue.IndexOf('/', num3);
			if (num4 == -1)
			{
				int.TryParse(attributeValue.Substring(num3), out result2);
			}
			else
			{
				int.TryParse(attributeValue.Substring(num3, num4 - num3), out result2);
				int.TryParse(attributeValue.Substring(++num4), out result3);
			}
		}
		if (_payloadFormatNumberToInfoMap.TryGetValue(result, out var value))
		{
			if (result2 == 0)
			{
				result2 = value.SamplesFrequency;
			}
			else
			{
				value.SamplesFrequency = result2;
			}
			string codecName = text.ToUpperInvariant();
			value.CodecInfo = TryCreateCodecInfo(codecName, result2, result3);
		}
	}

	private void ParseControlAttribute(string attributeValue)
	{
		if (_lastParsedFormatInfo != null)
		{
			_lastParsedFormatInfo.TrackName = attributeValue;
		}
	}

	private void ParseFmtpAttribute(string attributeValue)
	{
		int num = attributeValue.IndexOf(' ');
		if (num < 1)
		{
			return;
		}
		string s = attributeValue.Substring(0, num);
		if (!int.TryParse(s, out var result) || !_payloadFormatNumberToInfoMap.TryGetValue(result, out var value))
		{
			return;
		}
		int num2 = num;
		while (attributeValue[num2] == ' ')
		{
			if (++num2 == attributeValue.Length)
			{
				return;
			}
		}
		string text = attributeValue.Substring(num2);
		string[] formatAttributes = Array.ConvertAll(text.Split(new char[1] { ';' }), (string p) => p.Trim());
		if (value.CodecInfo is H264CodecInfo h264CodecInfo)
		{
			ParseH264FormatAttributes(formatAttributes, h264CodecInfo);
		}
		else if (value.CodecInfo is AACCodecInfo aacCodecInfo)
		{
			ParseAACFormatAttributes(formatAttributes, aacCodecInfo);
		}
	}

	private static void ParseH264FormatAttributes(string[] formatAttributes, H264CodecInfo h264CodecInfo)
	{
		string text = formatAttributes.FirstOrDefault((string fa) => fa.StartsWith("sprop-parameter-sets", StringComparison.InvariantCultureIgnoreCase));
		if (text != null)
		{
			string formatParameterValue = GetFormatParameterValue(text);
			int num = formatParameterValue.IndexOf(',');
			if (num == -1)
			{
				byte[] spsPpsBytes = RawH264Frame.StartMarker.Concat(Convert.FromBase64String(formatParameterValue)).ToArray();
				h264CodecInfo.SpsPpsBytes = spsPpsBytes;
				return;
			}
			IEnumerable<byte> first = RawH264Frame.StartMarker.Concat(Convert.FromBase64String(formatParameterValue.Substring(0, num)));
			num++;
			IEnumerable<byte> second = RawH264Frame.StartMarker.Concat(Convert.FromBase64String(formatParameterValue.Substring(num)));
			h264CodecInfo.SpsPpsBytes = first.Concat(second).ToArray();
		}
	}

	private static void ParseAACFormatAttributes(string[] formatAttributes, AACCodecInfo aacCodecInfo)
	{
		string text = formatAttributes.FirstOrDefault((string fa) => fa.StartsWith("sizeLength", StringComparison.InvariantCultureIgnoreCase));
		if (text == null)
		{
			throw new SdpParserException("SizeLength parameters is not found");
		}
		string text2 = formatAttributes.FirstOrDefault((string fa) => fa.StartsWith("indexLength", StringComparison.InvariantCultureIgnoreCase));
		if (text2 == null)
		{
			throw new SdpParserException("IndexLength parameters is not found");
		}
		string text3 = formatAttributes.FirstOrDefault((string fa) => fa.StartsWith("indexDeltaLength", StringComparison.InvariantCultureIgnoreCase));
		if (text3 == null)
		{
			throw new SdpParserException("IndexDeltaLength parameters is not found");
		}
		aacCodecInfo.SizeLength = int.Parse(GetFormatParameterValue(text));
		aacCodecInfo.IndexLength = int.Parse(GetFormatParameterValue(text2));
		aacCodecInfo.IndexDeltaLength = int.Parse(GetFormatParameterValue(text3));
		string text4 = formatAttributes.FirstOrDefault((string fa) => fa.StartsWith("config", StringComparison.InvariantCultureIgnoreCase));
		if (text4 != null)
		{
			aacCodecInfo.ConfigBytes = Hex.StringToByteArray(GetFormatParameterValue(text4));
		}
	}

	private static string GetFormatParameterValue(string formatParameter)
	{
		if (formatParameter == null)
		{
			throw new ArgumentNullException("formatParameter");
		}
		int num = formatParameter.IndexOf('=');
		if (num == -1)
		{
			throw new SdpParserException("Bad parameter format: " + formatParameter);
		}
		num++;
		if (num == formatParameter.Length)
		{
			throw new SdpParserException("Empty parameter value: " + formatParameter);
		}
		return formatParameter.Substring(num);
	}

	private static CodecInfo TryCreateCodecInfo(int payloadFormatNumber)
	{
		CodecInfo result = null;
		switch (payloadFormatNumber)
		{
		case 0:
			result = new G711UCodecInfo();
			break;
		case 2:
			result = new G726CodecInfo(32000);
			break;
		case 8:
			result = new G711ACodecInfo();
			break;
		case 10:
			result = new PCMCodecInfo(44100, 16, 2);
			break;
		case 11:
			result = new PCMCodecInfo(44100, 16, 1);
			break;
		case 26:
			result = new MJPEGCodecInfo();
			break;
		case 105:
			result = new H264CodecInfo();
			break;
		}
		return result;
	}

	private static CodecInfo TryCreateCodecInfo(string codecName, int samplesFrequency, int channels)
	{
		if (codecName == "JPEG")
		{
			return new MJPEGCodecInfo();
		}
		if (codecName == "H264")
		{
			return new H264CodecInfo();
		}
		bool flag = codecName == "PCMU";
		bool flag2 = codecName == "PCMA";
		if (flag || flag2)
		{
			G711CodecInfo g711CodecInfo = ((!flag) ? ((G711CodecInfo)new G711ACodecInfo()) : ((G711CodecInfo)new G711UCodecInfo()));
			if (samplesFrequency != 0)
			{
				g711CodecInfo.SampleRate = samplesFrequency;
			}
			if (channels != 0)
			{
				g711CodecInfo.Channels = channels;
			}
			return g711CodecInfo;
		}
		if (codecName == "L16" || codecName == "L8")
		{
			return new PCMCodecInfo((samplesFrequency != 0) ? samplesFrequency : 8000, (codecName == "L16") ? 16 : 8, (channels == 0) ? 1 : channels);
		}
		if (codecName == "MPEG4-GENERIC")
		{
			return new AACCodecInfo();
		}
		if (codecName.Contains("726"))
		{
			int bitrate;
			if (codecName.Contains("16"))
			{
				bitrate = 16000;
			}
			else if (codecName.Contains("24"))
			{
				bitrate = 24000;
			}
			else if (codecName.Contains("32"))
			{
				bitrate = 32000;
			}
			else
			{
				if (!codecName.Contains("40"))
				{
					return null;
				}
				bitrate = 40000;
			}
			G726CodecInfo g726CodecInfo = new G726CodecInfo(bitrate);
			if (samplesFrequency != 0)
			{
				g726CodecInfo.SampleRate = samplesFrequency;
			}
			else if (channels != 0)
			{
				g726CodecInfo.Channels = channels;
			}
			return g726CodecInfo;
		}
		return null;
	}

	protected static int GetSamplesFrequencyFromPayloadType(int payloadFormatNumber)
	{
		switch (payloadFormatNumber)
		{
		case 0:
		case 2:
		case 3:
		case 4:
		case 5:
		case 7:
		case 8:
		case 9:
		case 12:
		case 15:
		case 18:
			return 8000;
		case 6:
			return 16000;
		case 10:
		case 11:
			return 44100;
		case 16:
			return 11025;
		case 17:
			return 22050;
		case 14:
		case 25:
		case 26:
		case 28:
		case 31:
		case 32:
		case 33:
		case 34:
			return 90000;
		default:
			return 0;
		}
	}
}
