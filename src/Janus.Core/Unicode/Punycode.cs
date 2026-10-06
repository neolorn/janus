using System.Collections.Generic;
using System.Text;

namespace Janus.Core.Unicode;

/// <summary>
/// The Punycode of RFC 3492, which writes a label of any code points in the letters,
/// digits and hyphen a DNS label carries.
/// </summary>
/// <remarks>
/// Serves REG-DOM-001. Both directions refuse where the arithmetic of RFC 3492 section
/// 6.4 would overflow, and the decoder refuses what is not a digit of the encoding, so
/// neither answers a label the other could not have produced.
/// </remarks>
internal static class Punycode
{
    private const int Base = 36;
    private const int MinimumThreshold = 1;
    private const int MaximumThreshold = 26;
    private const int Skew = 38;
    private const int Damp = 700;
    private const int InitialBias = 72;
    private const int InitialCodePoint = 0x80;
    private const int LastCodePoint = 0x10FFFF;
    private const int Delimiter = 0x002D;
    private const int Letters = 26;

    /// <summary>
    /// Encodes a label.
    /// </summary>
    /// <param name="label">The label's code points.</param>
    /// <param name="encoded">The encoded label, without the ACE prefix, or empty.</param>
    /// <returns>Whether the label could be encoded.</returns>
    internal static bool TryEncode(List<int> label, out string encoded)
    {
        var output = new StringBuilder(label.Count * 2);

        encoded = string.Empty;

        foreach (int code in label)
        {
            if (code < InitialCodePoint)
            {
                output.Append((char)code);
            }
        }

        int basic = output.Length;
        int handled = basic;
        int current = InitialCodePoint;
        int delta = 0;
        int bias = InitialBias;

        if (basic > 0)
        {
            output.Append((char)Delimiter);
        }

        while (handled < label.Count)
        {
            int next = Smallest(label, current);

            if (next - current > (int.MaxValue - delta) / (handled + 1))
            {
                return false;
            }

            delta += (next - current) * (handled + 1);
            current = next;

            foreach (int point in label)
            {
                if (point < current && delta++ == int.MaxValue)
                {
                    return false;
                }

                if (point != current)
                {
                    continue;
                }

                AppendInteger(output, delta, bias);
                bias = Adapt(delta, handled + 1, handled == basic);
                delta = 0;
                handled++;
            }

            delta++;
            current++;
        }

        encoded = output.ToString();

        return true;
    }

    /// <summary>
    /// Decodes a label.
    /// </summary>
    /// <param name="encoded">The encoded label's code points, without the ACE prefix.</param>
    /// <param name="label">The label's code points, or none.</param>
    /// <returns>Whether the encoded label reads.</returns>
    internal static bool TryDecode(List<int> encoded, out List<int> label)
    {
        int basic = encoded.LastIndexOf(Delimiter);
        int current = InitialCodePoint;
        int index = 0;
        int bias = InitialBias;

        label = [];

        for (int position = 0; position < basic; position++)
        {
            if (encoded[position] >= InitialCodePoint)
            {
                return false;
            }

            label.Add(encoded[position]);
        }

        // RFC 3492 section 6.2 reads a delimiter as one only where a basic code point
        // stands before it; a delimiter the label opens with is read as a digit, which
        // it is not.
        int read = basic > 0 ? basic + 1 : 0;

        while (read < encoded.Count)
        {
            int before = index;

            if (!TryReadInteger(encoded, ref read, bias, ref index))
            {
                return false;
            }

            bias = Adapt(index - before, label.Count + 1, before == 0);

            if (index / (label.Count + 1) > LastCodePoint - current)
            {
                return false;
            }

            current += index / (label.Count + 1);
            index %= label.Count + 1;
            label.Insert(index, current);
            index++;
        }

        return true;
    }

    private static bool TryReadInteger(List<int> encoded, ref int read, int bias, ref int index)
    {
        int weight = 1;

        for (int position = Base; ; position += Base)
        {
            if (read >= encoded.Count)
            {
                return false;
            }

            int digit = Digit(encoded[read++]);

            if (digit < 0 || digit > (int.MaxValue - index) / weight)
            {
                return false;
            }

            index += digit * weight;

            int threshold = Threshold(position, bias);

            if (digit < threshold)
            {
                return true;
            }

            if (weight > int.MaxValue / (Base - threshold))
            {
                return false;
            }

            weight *= Base - threshold;
        }
    }

    private static void AppendInteger(StringBuilder output, int value, int bias)
    {
        int rest = value;

        for (int position = Base; ; position += Base)
        {
            int threshold = Threshold(position, bias);

            if (rest < threshold)
            {
                break;
            }

            output.Append(Character(threshold + ((rest - threshold) % (Base - threshold))));
            rest = (rest - threshold) / (Base - threshold);
        }

        output.Append(Character(rest));
    }

    private static int Smallest(List<int> label, int from)
    {
        int smallest = int.MaxValue;

        foreach (int code in label)
        {
            if (code >= from && code < smallest)
            {
                smallest = code;
            }
        }

        return smallest;
    }

    private static int Threshold(int position, int bias)
    {
        if (position <= bias)
        {
            return MinimumThreshold;
        }

        return position >= bias + MaximumThreshold ? MaximumThreshold : position - bias;
    }

    private static int Adapt(int delta, int count, bool first)
    {
        int scaled = first ? delta / Damp : delta / 2;
        int position = 0;

        scaled += scaled / count;

        while (scaled > (Base - MinimumThreshold) * MaximumThreshold / 2)
        {
            scaled /= Base - MinimumThreshold;
            position += Base;
        }

        return position + ((Base - MinimumThreshold + 1) * scaled / (scaled + Skew));
    }

    private static int Digit(int code) => code switch
    {
        >= 'a' and <= 'z' => code - 'a',
        >= 'A' and <= 'Z' => code - 'A',
        >= '0' and <= '9' => code - '0' + Letters,
        _ => -1,
    };

    private static char Character(int digit) =>
        (char)(digit < Letters ? 'a' + digit : '0' + digit - Letters);
}
