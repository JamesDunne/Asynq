using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using System.Web;
using System.Globalization;

namespace System
{
    public static class StringExtensions
    {
        /// <summary>
        /// Attempts to remove all HTML tags from content.  Leaves HTML entities in (e.g. &amp;nbsp;)
        /// </summary>
        /// <remarks>
        /// This is not guaranteed to catch all cases of HTML opening tags, specifically ones
        /// with complex attributes.  It is intended for use with simple, user-generated HTML
        /// which wouldn't contain complex attributes, e.g. embedded javascript.
        /// </remarks>
        /// <param name="html">HTML text to remove tags from</param>
        /// <returns>The plain text with HTML entities</returns>
        public static string StripHTMLTags(this string html)
        {
            // HTML opening tags must start with an alpha.
            // HTML closing tags must start with / and an alpha.
            // This will scan text up to the first >, so javascript code that might include a '>'
            // operator embedded within an onclick="" will probably clip the HTML entity early.
            string pattern = @"<([A-Za-z]|/[A-Za-z])(.|\n)*?>";

            return Regex.Replace(html, pattern, String.Empty);
        }

        /// <summary>
        /// Chops a line at a word boundary at or before <paramref name="maxlength"/>.
        /// </summary>
        /// <param name="line">Input text to chop.</param>
        /// <param name="maxlength">Maximum length of the output string. Final result may be fewer characters than this.</param>
        /// <param name="wordbreaks">Set of characters used to END words. For pairing characters (e.g. brackets) only include the ending pair character.</param>
        /// <returns></returns>
        public static string ChopToWord(this string line, int maxlength, params char[] wordbreaks)
        {
            if (String.IsNullOrEmpty(line)) return line;

            if ((wordbreaks == null) || (wordbreaks.Length == 0)) wordbreaks = _wordBreakChars;

            if (line.Length > maxlength)
            {
                int idx = line.LastIndexOfAny(wordbreaks, maxlength - 1);
                if (idx < 0) idx = maxlength; else ++idx;
                return line.Substring(0, idx);
            }
            return line;
        }

        private static char[] _wordBreakChars = new char[] { ' ', '.', '!', ',', ';', ':', '-', '"', '\'', ']', '}' };

        /// <summary>
        /// Chops a line at a word boundary at or before <paramref name="maxlength"/>, adding ellipsis at the end
        /// if it was chopped.
        /// </summary>
        /// <param name="line">Input text to chop.</param>
        /// <param name="maxlength">Maximum length of the output string. Final result may be fewer characters than this.</param>
        /// <param name="ellipsis">The string to use for ellipsis characters, e.g. "..."</param>
        /// <param name="wordbreaks">Set of characters used to END words. For pairing characters (e.g. brackets) only include the ending pair character.</param>
        /// <returns></returns>
        public static string ChopToWordWithEllipsis(this string line, int maxlength, string ellipsis, params char[] wordbreaks)
        {
            if (String.IsNullOrEmpty(line)) return line;

            if ((wordbreaks == null) || (wordbreaks.Length == 0)) wordbreaks = _wordBreakChars;

            if (line.Length > maxlength)
            {
                int idx = line.LastIndexOfAny(wordbreaks, maxlength - 1);
                if (idx < 0) idx = maxlength; else ++idx;
                return line.Substring(0, idx) + ellipsis;
            }
            return line;
        }

        private static char[] wrapChars = new char[] { ' ', '\t', '\r', '\n', '-' };

        /// <summary>
        /// Word-wrap a string of text to separate lines broken at the nth column.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="columns">Maximum number of columns per line</param>
        /// <returns>An enumerable implementation yielding each line</returns>
        public static IEnumerable<string> WordWrap(this string text, int columns)
        {
            string line;
            int i = 0;
            int remainingLength = text.Length;

            // Keep chopping lines until there's not enough text to chop:
            while (remainingLength > columns)
            {
                // Find the last occurrence of a word-wrap character in this line:
                int lastSplitCharIdx = text.LastIndexOfAny(wrapChars, i + columns, columns);
                if (lastSplitCharIdx < 0)
                {
                    // One long uninterruptible line, force a break at the `columns` mark.
                    lastSplitCharIdx = i + columns;
                }

                // Minimum line length threshold at 50%:
                int lineLength = lastSplitCharIdx - i;
                if (lineLength < columns / 2)
                {
                    lineLength = columns;
                }

                // Pick out the text up to the word-wrap character:
                line = text.Substring(i, lineLength).Trim();
                yield return line;

                // Set up for the next chop:
                i += lineLength;
                while (i < text.Length && Char.IsWhiteSpace(text[i])) ++i;
                remainingLength = text.Length - i;
            }

            // Yield the last portion of the string:
            if (remainingLength > 0)
            {
                line = text.Substring(i).Trim();
                yield return line;
            }

            // Done.
            yield break;
        }

        /// <summary>
        /// Performs a Trim() on both strings and then a case-insensitive comparision and returns true if equivalent.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="compare"></param>
        /// <returns></returns>
        public static bool CaseInsensitiveTrimmedEquals(this string self, params string[] compare)
        {
            for (int i = 0; i < compare.Length; ++i)
            {
                if (self == null && compare[i] == null) return true;
                if (self == null || compare[i] == null) return false;
                if (String.Compare(self.Trim(), (compare[i] ?? string.Empty).Trim(), true, System.Globalization.CultureInfo.InvariantCulture) == 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Performs a case-insensitive comparison and returns true if equivalent.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="compare"></param>
        /// <returns></returns>
        public static bool CaseInsensitiveEquals(this string self, params string[] compare)
        {
            for (int i = 0; i < compare.Length; ++i)
            {
                if (self == null && compare[i] == null) return true;
                if (self == null || compare[i] == null) return false;

                if (String.Compare(self, compare[i], true, System.Globalization.CultureInfo.InvariantCulture) == 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Performs a case-insensitive comparison and returns true if the string is contained within the current string.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="compare"></param>
        /// <returns></returns>
        public static bool CaseInsensitiveContains(this string self, params string[] compare)
        {
            string selfLower = (self ?? String.Empty).ToLowerInvariant();

            for (int i = 0; i < compare.Length; ++i)
            {
                if (self == null && compare[i] == null) return true;
                if (self == null || compare[i] == null) return false;

                if (self.ToLowerInvariant().Contains(compare[i].ToLowerInvariant())) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the string is null or empty or just a bunch of worthless whitespace.
        /// </summary>
        /// <remarks>This works because extension methods can be called on null references.</remarks>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(this string self)
        {
            if (self == null) return true;
            if (self.Length == 0) return true;
            // NOTE: This is the kicker over String.IsNullOrEmpty()
            if (self.Trim().Length == 0) return true;
            return false;
        }

        /// <summary>
        /// If the string is null or empty (or is all whitespace), then replace it with
        /// <paramref name="coalesce"/>.
        /// Use this as a slightly more improved null-coalescing operator than C#'s `??` operator.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="coalesce"></param>
        /// <returns></returns>
        public static string NullOrEmptyCoalesce(this string self, string coalesce)
        {
            if (self == null) return coalesce;
            if (self.Length == 0) return coalesce;
            // NOTE: This is the kicker over String.IsNullOrEmpty()
            if (self.Trim().Length == 0) return coalesce;
            return self;
        }


        /// <summary>
        /// If the string is null or empty (or is all whitespace), then replace it with 
        /// <paramref name="coalesce"/>.  Otherwise trim the string.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="coalesce"></param>
        /// <returns></returns>
        public static string TrimOrCoalesce(this string self, string coalesce)
        {
            if (self == null) return coalesce;
            if (self.Length == 0) return coalesce;
            // NOTE: This is the kicker over String.IsNullOrEmpty()
            var trimmed = self.Trim();
            if (trimmed.Length == 0) return coalesce;
            return trimmed;
        }

        /// <summary>
        /// Trims the string if non-null, otherwise returns null.
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string TrimOrNull(this string self)
        {
            if (self == null) return null;

            return self.Trim();
        }

        /// <summary>
        /// Trims the end of string if non-null, otherwise returns null.
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string TrimEndOrNull(this string self)
        {
            if (self == null) return null;

            return self.TrimEnd();
        }

        /// <summary>
        /// Parse a string value into a type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T Parse<T>(this string value)
        {
            // Get default value for type so if string
            // is empty then we can return default value.
            T result = default(T);

            if (!String.IsNullOrEmpty(value))
            {
                // we are not going to handle exception here
                // if you need SafeParse then you should create
                // another method specially for that.
                TypeConverter tc = TypeDescriptor.GetConverter(typeof(T));
                result = (T)tc.ConvertFrom(value);
            }

            return result;
        }

        public static string RemoveIfStartsWith(this string value, params string[] startsWith)
        {
            if (String.IsNullOrEmpty(value)) return value;

            for (int i = 0; i < startsWith.Length; ++i)
            {
                if (String.IsNullOrEmpty(startsWith[i])) continue;

                if (value.StartsWith(startsWith[i]))
                {
                    if (value.Length == startsWith[i].Length) return String.Empty;

                    return value.Substring(startsWith[i].Length);
                }
            }

            return value;
        }

        public static string RemoveIfStartsWith(this string value, string startsWith, StringComparison comparisonType)
        {
            if (String.IsNullOrEmpty(value)) return value;
            if (String.IsNullOrEmpty(startsWith)) return value;

            if (value.StartsWith(startsWith, comparisonType))
            {
                if (value.Length == startsWith.Length) return String.Empty;

                return value.Substring(startsWith.Length);
            }
            return value;
        }

        public static string RemoveIfEndsWith(this string value, params string[] endsWith)
        {
            if (String.IsNullOrEmpty(value)) return value;

            for (int i = 0; i < endsWith.Length; ++i)
            {
                if (String.IsNullOrEmpty(endsWith[i])) continue;

                if (value.EndsWith(endsWith[i]))
                {
                    return value.Substring(0, value.Length - endsWith[i].Length);
                }
            }

            return value;
        }

        public static string RemoveIfEndsWith(this string value, string endsWith, StringComparison comparisonType)
        {
            if (String.IsNullOrEmpty(value)) return value;
            if (String.IsNullOrEmpty(endsWith)) return value;

            if (value.EndsWith(endsWith, comparisonType))
            {
                return value.Substring(0, value.Length - endsWith.Length);
            }
            return value;
        }

        /// <summary>
        /// Parses a list of name=value pairs, delimited by <paramref name="delimiter"/>.  Values may be quoted.  Names must follow C# identifier
        /// rules.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="delimiter">Character to delimit name=value pairs. Suggestions: ':', ';', ','.</param>
        /// <returns></returns>
        public static Dictionary<string, string> ParseNameValues(this string line, char delimiter)
        {
            if (line.IsNullOrEmpty()) return null;

            char[] s = line.ToCharArray();

            // "a=b" minimum length case
            if (s.Length < 3) return null;

            Dictionary<string, string> props = new Dictionary<string, string>();

            int i = 0;
            while (i < s.Length)
            {
                // Skip whitespace:
                while (i < s.Length && Char.IsWhiteSpace(s[i])) ++i;
                if (i >= s.Length) return props;

                int keyStart = i;

                // Get identifier:
                if (s[i] != '_' && !Char.IsLetter(s[i])) return null;
                ++i;
                while (i < s.Length)
                {
                    // Break cases:
                    if (Char.IsWhiteSpace(s[i])) break;
                    if (s[i] == '=') break;

                    // Continue cases:
                    if (Char.IsLetterOrDigit(s[i]))
                    {
                        ++i;
                        continue;
                    }
                    if (s[i] == '_')
                    {
                        ++i;
                        continue;
                    }

                    // Assumed character is unacceptable in a name:
                    return props;
                }
                if (i >= s.Length) return props;

                string name = new string(s, keyStart, i - keyStart);

                // Skip whitespace after name before '=':
                while (i < s.Length && Char.IsWhiteSpace(s[i])) ++i;
                if (i >= s.Length) return props;

                if (s[i] != '=') return props;
                ++i;

                // Skip whitespace after '=' before value:
                while (i < s.Length && Char.IsWhiteSpace(s[i])) ++i;
                if (i >= s.Length) return props;

                string value = null;

                if (s[i] == '"' || s[i] == '\'')
                {
                    // Quoted string:
                    char quotechar = s[i];
                    ++i;

                    StringBuilder sbValue = new StringBuilder();
                    while (i < s.Length)
                    {
                        // Encountered escaped/double quotes?
                        if ((i < s.Length - 1) && (s[i] == quotechar) && (s[i + 1] == quotechar))
                        {
                            sbValue.Append(quotechar);
                            ++i;
                            ++i;
                            continue;
                        }
                        if (s[i] == quotechar) break;

                        // Add the character to the string:
                        sbValue.Append(s[i]);
                        ++i;
                    }
                    if (i >= s.Length) return props;

                    value = sbValue.ToString();

                    // Skip the ending quote character:
                    ++i;
                }
                else
                {
                    // Unquoted string:

                    StringBuilder sbValue = new StringBuilder();
                    while (i < s.Length)
                    {
                        if (s[i] == delimiter) break;

                        sbValue.Append(s[i]);
                        ++i;
                    }

                    value = sbValue.ToString();
                }

                props.Add(name, value);

                if (i >= s.Length) return props;
                if (s[i] != delimiter) return props;

                ++i;
            }

            return props;
        }

        /// <summary>
        /// Turns a string into a char with nulls and empty strings being returned as (char)0.
        /// </summary>
        /// <param name="singleCharacter"></param>
        /// <returns></returns>
        public static char ToSingleChar(this string singleCharacter)
        {
            if (singleCharacter.IsNullOrEmpty()) return (char)0;
            return singleCharacter[0];
        }

        private static char[] keywordSplitChars = new char[] { ' ', '\r', '\n', '\t', ';', ':', '.', ',' };

        /// <summary>
        /// Performs a case-insensitive keyword search in this string.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="keywords">whitespace delimited list of keywords to search for</param>
        /// <returns></returns>
        public static bool KeywordSearch(this string value, string keywords)
        {
            if (keywords.IsNullOrEmpty()) return true;
            if (value.IsNullOrEmpty()) return true;

            string[] words = keywords.Split(keywordSplitChars, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                if (value.CaseInsensitiveContains(word)) return true;
            }
            return false;
        }

        /// <summary>
        /// Takes the current list of keywords and searchs through all given strings.
        /// Performs a boolean AND over the keyword matching to make sure all keywords
        /// have at least one match within any of the given search strings.
        /// </summary>
        /// <param name="keywords">whitespace delimited list of keywords to search for</param>
        /// <param name="search"></param>
        /// <returns></returns>
        public static bool Search(this string keywords, params string[] search)
        {
            if (keywords.IsNullOrEmpty()) return true;
            if (search == null) return true;
            if (search.Length == 0) return true;

            string[] words = keywords.Split(keywordSplitChars, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                bool found = false;
                foreach (string find in search)
                {
                    // Skip null/empty search strings:
                    if (find.IsNullOrEmpty()) continue;

                    if (find.CaseInsensitiveContains(word))
                    {
                        found = true;
                        break;
                    }
                }

                // If the keyword was not found in any of the given search
                // strings, then the whole search has failed:
                if (!found) return false;
            }

            // All the keywords were found in the search strings:
            return true;
        }

        /// <summary>
        /// Replaces named token placeholders in the form of {TOKEN_NAME} with their replacement contents
        /// found in the <paramref name="tokens"/> dictionary.  Case-sensitivity of the token names
        /// is based on the key comparison of the <paramref name="tokens"/> dictionary.
        /// </summary>
        /// <param name="templateText"></param>
        /// <param name="tokens">A dictionary mapping token names to replacement text.  Tokens may consist of
        /// letters, digits, ':' or '_' only.</param>
        /// <returns></returns>
        public static string ReplaceTemplateTokens(this string templateText, IDictionary<string, string> tokens)
        {
            return templateText.ReplaceTemplateTokens(tn => tokens.ContainsKey(tn) ? tokens[tn] : null);
        }

        /// <summary>
        /// Replaces named token placeholders in the form of {TOKEN_NAME} with their replacement contents
        /// found in the <paramref name="tokens"/> dictionary.  Case-sensitivity of the token names
        /// is based on the key comparison of the <paramref name="tokens"/> dictionary.
        /// </summary>
        /// <param name="templateText"></param>
        /// <param name="getTokenReplacementText">A lambda to return text to be replace token names. Tokens may consist of
        /// letters, digits, ':' or '_' only. Tokens are surrounded in { } braces with no spaces allowed.</param>
        /// <returns></returns>
        public static string ReplaceTemplateTokens(this string templateText, Func<string, string> getTokenReplacementText)
        {
            // Replace tokens in the template with the values from the EventPayLoad dictionary:
            StringBuilder sbMsg = new StringBuilder();

            int i = 0;
            while (i < templateText.Length)
            {
                if (templateText[i] == '{')
                {
                    ++i;

                    // XML element names enforce the rule that token names cannot
                    // have spaces in them.  So, we scan for alphanumeric characters
                    // until we hit '}' or a different non-alphanumeric character.
                    int start = i;
                    while (i < templateText.Length)
                    {
                        if (!Char.IsLetterOrDigit(templateText[i]) && (templateText[i] != ':') && (templateText[i] != '_'))
                            break;
                        ++i;
                    }

                    // We hit the end?
                    if (i >= templateText.Length)
                    {
                        // FAIL.
                        i = start;
                        sbMsg.Append('{');
                        continue;
                    }

                    // Did we hit a real '}' character?
                    if (templateText[i] == '}')
                    {
                        // We have a token, sweet...
                        string tokenName = templateText.Substring(start, i - start);

                        ++i;
                        string replText = getTokenReplacementText(tokenName);

                        // Look up the token name in the arguments dictionary, case-insensitive.
                        if (replText != null)
                        {
                            // Insert the token's value from the EventPayLoad dictionary:
                            sbMsg.Append(replText);
                        }
                        else
                        {
                            // Token wasn't found in the EventPayLoad dictionary, so just insert
                            // this text raw:
                            sbMsg.Append('{');
                            sbMsg.Append(tokenName);
                            sbMsg.Append('}');
                        }
                    }
                    else
                    {
                        // Wasn't a token like we thought, just output the '{' and keep going from there:
                        i = start;
                        sbMsg.Append('{');
                        continue;
                    }
                }
                else
                {
                    // Just a regular old character to go straight through to the final text:
                    sbMsg.Append(templateText[i]);
                    ++i;
                }
            }

            return sbMsg.ToString();
        }

        /// <summary>
        /// Filters out a set of characters from the string.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static string Filter(this string value, params char[] filter)
        {
            if (value == null) return null;

            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (filter.Contains(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns true if the string follows normal identifier rules: must begin with a letter or underscore, must continue
        /// with only letters, digits, and underscores thereafter.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsIdentifier(this string value)
        {
            if (value == null) return false;
            if (value.Length == 0) return false;

            if ((value[0] != '_') && !Char.IsLetter(value[0])) return false;
            foreach (char c in value)
            {
                if (!Char.IsLetterOrDigit(c) && (c != '_')) return false;
            }

            return true;
        }

        public static string XmlEncode(this string input)
        {
            string encodedInput = input;
            using (MemoryStream ms = new MemoryStream())
            {
                using (XmlTextWriter xtw = new XmlTextWriter(ms, new UTF8Encoding(false)))
                {
                    xtw.WriteString(input);
                }
                encodedInput = Encoding.UTF8.GetString(ms.ToArray());
            }
            return encodedInput;
        }

        public static string XmlDecode(this string input)
        {
            string decodedInput = input;
            string formattedInput = string.Format("<root>{0}</root>", input);

            byte[] inputBytes = Encoding.UTF8.GetBytes(formattedInput);
            using (MemoryStream ms = new MemoryStream(inputBytes))
            {
                ms.Position = 0;
                using (XmlTextReader xtr = new XmlTextReader(ms))
                {
                    if (xtr.Read())
                        decodedInput = xtr.ReadString();
                }
            }
            return decodedInput;
        }

        /// <summary>
        /// Escapes special characters in the string with backslash characters for formatting as a C-like string literal.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string escapeQuotedString(this string value)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in value)
            {
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '\n') sb.Append("\\n");
                else if (ch == '\r') sb.Append("\\r");
                else if (ch == '\t') sb.Append("\\t");
                else if (ch == '\'') sb.Append("\\\'");
                else if (ch == '\"') sb.Append("\\\"");
                else if (((int)ch < 32) || (int)ch > 127)
                {
                    sb.Append(@"\u");
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0:x4}", (int)ch);
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Attempts to parse the string value as an Int32. If successful, the parsed value is
        /// returned otherwise <paramref name="defaultValue"/> is returned.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue">The default Int32 value to use if parsing fails</param>
        /// <returns></returns>
        public static int TryParseInt32(this string value, int defaultValue)
        {
            if (value == null) return defaultValue;

            int tmp;
            if (Int32.TryParse(value, out tmp)) return tmp;

            return defaultValue;
        }

        public static string RemoveAllSpaces(this string val)
        {
            return RemoveAllCharacters(val, ' ');
        }

        public static string RemoveAllCharacters(this string val, params char[] charactersToRemove)
        {
            StringBuilder sbResult = new StringBuilder(val.Length);
            foreach (char ch in val)
            {
                if (charactersToRemove.Contains(ch)) continue;
                sbResult.Append(ch);
            }
            return sbResult.ToString();
        }

        #region TAB-delimited backslash-escaped formatting

        public static string DecodeTabDelimited(this string value)
        {
            int length = value.Length;
            StringBuilder sbDecoded = new StringBuilder(length);
            for (int i = 0; i < length; ++i)
            {
                char ch = value[i];
                if (ch == '\\')
                {
                    ++i;
                    if (i >= length)
                    {
                        // throw exception?
                        break;
                    }
                    switch (value[i])
                    {
                        case 't': sbDecoded.Append('\t'); break;
                        case 'n': sbDecoded.Append('\n'); break;
                        case 'r': sbDecoded.Append('\r'); break;
                        case '\'': sbDecoded.Append('\''); break;
                        case '\"': sbDecoded.Append('\"'); break;
                        case '\\': sbDecoded.Append('\\'); break;
                        default: break;
                    }
                }
                else sbDecoded.Append(ch);
            }
            return sbDecoded.ToString();
        }

        public static string EncodeTabDelimited(this string value)
        {
            StringBuilder sbResult = new StringBuilder(value.Length * 3 / 2);
            foreach (char ch in value)
            {
                if (ch == '\t') sbResult.Append("\\t");
                else if (ch == '\n') sbResult.Append("\\n");
                else if (ch == '\r') sbResult.Append("\\r");
                else if (ch == '\'') sbResult.Append("\\\'");
                else if (ch == '\"') sbResult.Append("\\\"");
                else if (ch == '\\') sbResult.Append("\\\\");
                else
                {
                    sbResult.Append(ch);
                }
            }
            return sbResult.ToString();
        }

        public static string[] SplitTabDelimited(this string line)
        {
            string[] cols = line.Split('\t');
            int length = cols.Length;
            string[] result = new string[length];
            for (int i = 0; i < length; ++i)
            {
                // Treat \0 string as null:
                if (cols[i] == "\0") result[i] = null;
                else result[i] = DecodeTabDelimited(cols[i]);
            }
            return result;
        }

        public static string JoinTabDelimited(this string[] cols)
        {
            int length = cols.Length;
            string[] tabEncoded = new string[length];
            for (int i = 0; i < length; ++i)
            {
                if (cols[i] == null) tabEncoded[i] = "\0";
                else tabEncoded[i] = EncodeTabDelimited(cols[i]);
            }
            return String.Join("\t", tabEncoded);
        }

        public static string JoinTabDelimited(this object[] cols)
        {
            int length = cols.Length;
            string[] tabEncoded = new string[length];
            for (int i = 0; i < length; ++i)
            {
                object col = cols[i];
                if (col == null) tabEncoded[i] = "\0";
                else
                {
                    string strValue = System.ComponentModel.TypeDescriptor.GetConverter(col.GetType()).ConvertToInvariantString(col);
                    tabEncoded[i] = EncodeTabDelimited(strValue);
                }
            }
            return String.Join("\t", tabEncoded);
        }

        public static string JoinTabDelimited(this IEnumerable<string> cols)
        {
            return JoinTabDelimited(cols.ToArray());
        }

        public static string JoinTabDelimited(this IEnumerable<object> cols)
        {
            return JoinTabDelimited(cols.ToArray());
        }

        #endregion
    }
}