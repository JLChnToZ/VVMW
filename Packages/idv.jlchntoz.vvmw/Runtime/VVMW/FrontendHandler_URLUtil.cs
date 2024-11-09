using System;
using System.Globalization;
using UnityEngine;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class FrontendHandler {
        char[] punyCodeSplitDelimiters = new char[] { '-' };

        string UnescapeUrl(VRCUrl url) {
            if (VRCUrl.IsNullOrEmpty(url)) return "";
            var title = url.Get();
            string result;
            int index = title.IndexOf("://");
            if (index >= 0) {
                index += 3;
                result = title.Substring(0, index);
                int nextIndex = title.IndexOf('/', index);
                if (nextIndex < 0) nextIndex = title.Length;
                var domainParts = title.Substring(index, nextIndex - index).Split('.');
                for (int i = 0; i < domainParts.Length; i++) {
                    var fragment = domainParts[i];
                    if (fragment.StartsWith("xn--"))
                        domainParts[i] = DecodePunycode(fragment.Substring(4));
                }
                result += string.Join(".", domainParts);
                index = nextIndex;
            } else {
                result = "";
                index = 0;
            }
            while (index >= 0) {
                int offset = title.IndexOf('%', index);
                if (offset < 0 || offset + 3 > title.Length) {
                    result += title.Substring(index);
                    break;
                }
                if (offset > index) {
                    result += title.Substring(index, offset - index);
                    index = offset;
                }
                // Not even a valid hex number
                if (!byte.TryParse(title.Substring(offset + 1, 2), NumberStyles.HexNumber, null, out byte b)) {
                    result += '%';
                    index++;
                    continue;
                }
                int utf32, length;
                if      ((b & 0x80) == 0x00) { utf32 = b;        length = 1; }
                else if ((b & 0xE0) == 0xC0) { utf32 = b & 0x1F; length = 2; }
                else if ((b & 0xF0) == 0xE0) { utf32 = b & 0x0F; length = 3; }
                else if ((b & 0xF8) == 0xF0) { utf32 = b & 0x07; length = 4; }
                else if ((b & 0xFC) == 0xF8) { utf32 = b & 0x03; length = 5; }
                else if ((b & 0xFE) == 0xFC) { utf32 = b & 0x01; length = 6; }
                else { result += '%'; index++; continue; } // Invalid UTF-8
                // Not enough bytes
                if (index + length * 3 > title.Length) {
                    result += '%';
                    index++;
                    continue;
                }
                for (int i = 1; i < length; i++) {
                    offset = index + i * 3;
                    if (title[offset] != '%' ||
                        !byte.TryParse(title.Substring(offset + 1, 2), NumberStyles.HexNumber, null, out b) ||
                        (b & 0xC0) != 0x80) {
                        result += '%';
                        utf32 = 0;
                        break;
                    }
                    utf32 <<= 6;
                    utf32 |= b & 0x3F;
                }
                if (utf32 == 0) {
                    index++;
                    continue;
                }
                result += char.ConvertFromUtf32(utf32);
                index += length * 3;
            }
            return result;
        }

        string DecodePunycode(string source) {
            var result = new char[source.Length];
            int outputLength = 0;
            int basic = Mathf.Max(0, source.LastIndexOf('-'));
            for (int i = 0; i < basic; i++) {
                if (source[i] >= 0x80) return source;
                result[outputLength++] = source[i];
            }
            for (int i = 0, j = basic > 0 ? basic + 1 : 0, length = source.Length, n = 0x80, bias = 72; j < length; i++, outputLength++) {
                int oldI = i;
                for (int w = 1, k = 36; ; k += 36) {
                    if (j >= length) return source;
                    int digit = BasicToDigit(source[j++]);
                    if (digit < 0 || digit >= 36 || digit > Mathf.Floor((float)(int.MaxValue - i) / w)) return source;
                    i += digit * w;
                    int t = k <= bias ? 1 : k >= bias + 26 ? 26 : k - bias;
                    if (digit < t) break;
                    int baseMinusT = 36 - t;
                    if (w > Mathf.Floor((float)int.MaxValue / baseMinusT)) return source;
                    w *= baseMinusT;
                }
                int outOffset = outputLength + 1;
                bias = Adapt(i - oldI, outputLength + 1, oldI == 0);
                if (i / outOffset > int.MaxValue - n) return source;
                n += i / outOffset;
                i %= outOffset;
                Array.Copy(result, i, result, i + 1, outputLength - i);
                result[i] = (char)n;
            }
            return new string(result, 0, outputLength);
        }

        int BasicToDigit(char cp) {
            if (cp >= 'a' && cp <= 'z') return cp - 'a';
            if (cp >= 'A' && cp <= 'Z') return cp - 'A';
            if (cp >= '0' && cp <= '9') return cp - '0' + 26;
            return -1;
        }

        int Adapt(int delta, int numPoints, bool firstTime) {
            delta = firstTime ? delta / 700 : delta / 2;
            delta += delta / numPoints;
            int k;
            for (k = 0; delta > 455; k += 36)
                delta /= 35;
            return k + 36 * delta / (delta + 38);
        }
    }
}