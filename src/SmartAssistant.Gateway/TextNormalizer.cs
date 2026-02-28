using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SmartAssistant.Gateway;

public sealed class EncodingNormalizationException : Exception
{
	public string FieldPath { get; }
	public string Sample { get; }

	public EncodingNormalizationException(string fieldPath, string sample, string message = "invalid text encoding")
		: base(message)
	{
		FieldPath = fieldPath;
		Sample = sample.Length <= 80 ? sample : sample[..80];
	}
}

public static class TextNormalizer
{
	private static readonly UTF8Encoding Utf8Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
	private static readonly Encoding Latin1 = Encoding.Latin1;
	private static readonly Encoding Win1252;
	private static readonly string[] MojibakeMarkers = ["Ã", "Â", "æ", "ç", "å", "ä", "é", "è", "ê", "ô", "ö", "ï", "ð"];

	static TextNormalizer()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		Win1252 = Encoding.GetEncoding(1252);
	}

	public static string NormalizeText(string value, string fieldPath, bool strict)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		if (!LooksMojibake(value))
		{
			return value;
		}

		string? repaired = RepairText(value);
		if (!string.IsNullOrEmpty(repaired) && QualityScore(repaired) > QualityScore(value))
		{
			return repaired;
		}

		if (strict)
		{
			throw new EncodingNormalizationException(fieldPath, value);
		}
		return value;
	}

	public static Dictionary<string, string>? NormalizeStringDictionary(
		Dictionary<string, string>? value,
		string fieldPath,
		bool strict)
	{
		if (value is null)
		{
			return null;
		}

		Dictionary<string, string> normalized = new(value.Count, StringComparer.OrdinalIgnoreCase);
		foreach ((string key, string current) in value)
		{
			normalized[key] = NormalizeText(current ?? string.Empty, $"{fieldPath}.{key}", strict);
		}
		return normalized;
	}

	public static Dictionary<string, object?>? NormalizeObjectDictionary(
		Dictionary<string, object?>? value,
		string fieldPath,
		bool strict)
	{
		if (value is null)
		{
			return null;
		}

		Dictionary<string, object?> normalized = new(value.Count, StringComparer.OrdinalIgnoreCase);
		foreach ((string key, object? current) in value)
		{
			normalized[key] = NormalizeUnknown(current, $"{fieldPath}.{key}", strict);
		}
		return normalized;
	}

	private static object? NormalizeUnknown(object? value, string fieldPath, bool strict)
	{
		return value switch
		{
			null => null,
			string text => NormalizeText(text, fieldPath, strict),
			Dictionary<string, string> map => NormalizeStringDictionary(map, fieldPath, strict),
			Dictionary<string, object?> map => NormalizeObjectDictionary(map, fieldPath, strict),
			JsonElement element => NormalizeJsonElement(element, fieldPath, strict),
			IReadOnlyList<object> rows => rows.Select((item, index) => NormalizeUnknown(item, $"{fieldPath}[{index}]", strict)).ToList(),
			_ => value,
		};
	}

	private static object? NormalizeJsonElement(JsonElement element, string fieldPath, bool strict)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.String:
				return NormalizeText(element.GetString() ?? string.Empty, fieldPath, strict);
			case JsonValueKind.Number:
				if (element.TryGetInt64(out long longValue))
				{
					return longValue;
				}
				if (element.TryGetDouble(out double doubleValue))
				{
					return doubleValue;
				}
				return element.GetRawText();
			case JsonValueKind.True:
			case JsonValueKind.False:
				return element.GetBoolean();
			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				return null;
			case JsonValueKind.Array:
				List<object?> list = new();
				int index = 0;
				foreach (JsonElement item in element.EnumerateArray())
				{
					list.Add(NormalizeJsonElement(item, $"{fieldPath}[{index}]", strict));
					index += 1;
				}
				return list;
			case JsonValueKind.Object:
				Dictionary<string, object?> map = new(StringComparer.OrdinalIgnoreCase);
				foreach (JsonProperty item in element.EnumerateObject())
				{
					map[item.Name] = NormalizeJsonElement(item.Value, $"{fieldPath}.{item.Name}", strict);
				}
				return map;
			default:
				return element.GetRawText();
		}
	}

	private static bool LooksMojibake(string value)
	{
		if (value.Contains('�'))
		{
			return true;
		}
		if (ControlCharCount(value) > 0)
		{
			return true;
		}
		int markerHits = 0;
		foreach (string marker in MojibakeMarkers)
		{
			markerHits += CountOccurrences(value, marker);
		}
		return markerHits >= 2;
	}

	private static int ControlCharCount(string value)
	{
		int count = 0;
		foreach (char ch in value)
		{
			if (ch >= (char)0x80 && ch <= (char)0x9F)
			{
				count += 1;
			}
		}
		return count;
	}

	private static string? RepairText(string value)
	{
		string best = value;
		double bestScore = QualityScore(value);
		foreach (Encoding source in new[] { Latin1, Win1252 })
		{
			string? candidate = TryDecode(value, source);
			if (string.IsNullOrEmpty(candidate) || candidate == value)
			{
				continue;
			}

			double score = QualityScore(candidate);
			if (score > bestScore)
			{
				best = candidate;
				bestScore = score;
			}
		}
		return best == value ? null : best;
	}

	private static string? TryDecode(string value, Encoding sourceEncoding)
	{
		try
		{
			byte[] raw = sourceEncoding.GetBytes(value);
			return Utf8Strict.GetString(raw);
		}
		catch
		{
			return null;
		}
	}

	private static double QualityScore(string value)
	{
		int cjkCount = value.Count(ch => ch is >= '一' and <= '龿');
		int printableCount = value.Count(ch => !char.IsControl(ch));
		int markerHits = 0;
		foreach (string marker in MojibakeMarkers)
		{
			markerHits += CountOccurrences(value, marker);
		}
		int replacementCount = value.Count(ch => ch == '�');
		return (cjkCount * 6.0)
		       + (printableCount * 0.05)
		       - (ControlCharCount(value) * 8.0)
		       - (replacementCount * 12.0)
		       - (markerHits * 2.0);
	}

	private static int CountOccurrences(string value, string marker)
	{
		if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(marker))
		{
			return 0;
		}
		int count = 0;
		int index = 0;
		while (true)
		{
			index = value.IndexOf(marker, index, StringComparison.Ordinal);
			if (index < 0)
			{
				break;
			}
			count += 1;
			index += marker.Length;
		}
		return count;
	}
}
