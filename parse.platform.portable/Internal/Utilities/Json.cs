// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Parse.Internal.Utilities
{
    /// <summary>
    /// A simple recursive-descent JSON Parser based on the grammar defined at http://www.json.org
    /// and http://tools.ietf.org/html/rfc4627
    /// </summary>
    public class Json
    {
        /// <summary>
        /// Place at the start of a regex to force the match to begin wherever the search starts (i.e.
        /// anchored at the index of the first character of the search, even when that search starts
        /// in the middle of the string).
        /// </summary>
        private const string StartOfString = "\\G";

        private const char StartObject = '{';
        private const char EndObject = '}';
        private const char StartArray = '[';
        private const char EndArray = ']';
        private const char ValueSeparator = ',';
        private const char NameSeparator = ':';
        private static readonly char[] FalseValue = "false".ToCharArray();
        private static readonly char[] TrueValue = "true".ToCharArray();
        private static readonly char[] NullValue = "null".ToCharArray();

        private static readonly Regex NumberValue = new Regex(StartOfString +
                                                              @"-?(?:0|[1-9]\d*)(?<frac>\.\d+)?(?<exp>(?:e|E)(?:-|\+)?\d+)?");

        private static readonly Regex StringValue = new Regex(StartOfString +
                                                              "\"(?<content>(?:[^\\\\\"]|(?<escape>\\\\(?:[\\\\\"/bfnrt]|u[0-9a-fA-F]{4})))*)\"",
            RegexOptions.Multiline);

        private static readonly Regex EscapePattern = new Regex("\\\\|\"|[\u0000-\u001F]");

        private class JsonStringParser
        {
            private string Input { get; }

            private char[] InputAsArray { get; }

            public int CurrentIndex { get; private set; }

            private void Skip(int skip)
            {
                CurrentIndex += skip;
            }

            public JsonStringParser(string input)
            {
                Input = input;
                InputAsArray = input.ToCharArray();
            }

            /// <summary>
            /// Parses JSON object syntax (e.g. '{}')
            /// </summary>
            internal bool ParseObject(out object output)
            {
                output = null;
                if (!Accept(StartObject))
                {
                    return false;
                }

                var dict = new Dictionary<string, object>();
                while (true)
                {
                    if (!ParseMember(out var pairValue))
                    {
                        break;
                    }

                    if (pairValue is Tuple<string, object> pair) dict[pair.Item1] = pair.Item2;
                    if (!Accept(ValueSeparator))
                    {
                        break;
                    }
                }

                if (!Accept(EndObject))
                {
                    return false;
                }

                output = dict;
                return true;
            }

            /// <summary>
            /// Parses JSON member syntax (e.g. '"keyname" : null')
            /// </summary>
            private bool ParseMember(out object output)
            {
                output = null;
                if (!ParseString(out var key))
                {
                    return false;
                }

                if (!Accept(NameSeparator))
                {
                    return false;
                }

                if (!ParseValue(out var value))
                {
                    return false;
                }

                output = new Tuple<string, object>((string) key, value);
                return true;
            }

            /// <summary>
            /// Parses JSON array syntax (e.g. '[]')
            /// </summary>
            internal bool ParseArray(out object output)
            {
                output = null;
                if (!Accept(StartArray))
                {
                    return false;
                }

                var list = new List<object>();
                while (true)
                {
                    if (!ParseValue(out var value))
                    {
                        break;
                    }

                    list.Add(value);
                    if (!Accept(ValueSeparator))
                    {
                        break;
                    }
                }

                if (!Accept(EndArray))
                {
                    return false;
                }

                output = list;
                return true;
            }

            /// <summary>
            /// Parses a value (i.e. the right-hand side of an object member assignment or
            /// an element in an array)
            /// </summary>
            private bool ParseValue(out object output)
            {
                if (Accept(FalseValue))
                {
                    output = false;
                    return true;
                }
                else if (Accept(NullValue))
                {
                    output = null;
                    return true;
                }
                else if (Accept(TrueValue))
                {
                    output = true;
                    return true;
                }

                return ParseObject(out output) ||
                       ParseArray(out output) ||
                       ParseNumber(out output) ||
                       ParseString(out output);
            }

            /// <summary>
            /// Parses a JSON string (e.g. '"foo\u1234bar\n"')
            /// </summary>
            private bool ParseString(out object output)
            {
                output = null;
                Match m;
                if (!Accept(StringValue, out m))
                {
                    return false;
                }

                // handle escapes:
                int offset = 0;
                var contentCapture = m.Groups["content"];
                var builder = new StringBuilder(contentCapture.Value);
                foreach (Capture escape in m.Groups["escape"].Captures)
                {
                    int index = (escape.Index - contentCapture.Index) - offset;
                    offset += escape.Length - 1;
                    builder.Remove(index + 1, escape.Length - 1);
                    switch (escape.Value[1])
                    {
                        case '\"':
                            builder[index] = '\"';
                            break;
                        case '\\':
                            builder[index] = '\\';
                            break;
                        case '/':
                            builder[index] = '/';
                            break;
                        case 'b':
                            builder[index] = '\b';
                            break;
                        case 'f':
                            builder[index] = '\f';
                            break;
                        case 'n':
                            builder[index] = '\n';
                            break;
                        case 'r':
                            builder[index] = '\r';
                            break;
                        case 't':
                            builder[index] = '\t';
                            break;
                        case 'u':
                            builder[index] = (char) ushort.Parse(escape.Value.Substring(2),
                                NumberStyles.AllowHexSpecifier);
                            break;
                        default:
                            throw new ArgumentException("Unexpected escape character in string: " + escape.Value);
                    }
                }

                output = builder.ToString();
                return true;
            }

            /// <summary>
            /// Parses a number. Returns a long if the number is an integer or has an exponent,
            /// otherwise returns a double.
            /// </summary>
            private bool ParseNumber(out object output)
            {
                output = null;
                Match m;
                if (!Accept(NumberValue, out m))
                {
                    return false;
                }

                if (m.Groups["frac"].Length > 0 || m.Groups["exp"].Length > 0)
                {
                    // It's a double.
                    output = double.Parse(m.Value, CultureInfo.InvariantCulture);
                    return true;
                }
                else
                {
                    output = long.Parse(m.Value, CultureInfo.InvariantCulture);
                    return true;
                }
            }

            /// <summary>
            /// Matches the string to a regex, consuming part of the string and returning the match.
            /// </summary>
            private bool Accept(Regex matcher, out Match match)
            {
                match = matcher.Match(Input, CurrentIndex);
                if (match.Success)
                {
                    Skip(match.Length);
                }

                return match.Success;
            }

            /// <summary>
            /// Find the first occurrences of a character, consuming part of the string.
            /// </summary>
            private bool Accept(char condition)
            {
                int step = 0;
                int strLen = InputAsArray.Length;
                int currentStep = CurrentIndex;
                char currentChar;

                // Remove whitespace
                while (currentStep < strLen &&
                       ((currentChar = InputAsArray[currentStep]) == ' ' ||
                        currentChar == '\r' ||
                        currentChar == '\t' ||
                        currentChar == '\n'))
                {
                    ++step;
                    ++currentStep;
                }

                bool match = (currentStep < strLen) && (InputAsArray[currentStep] == condition);
                if (match)
                {
                    ++step;
                    ++currentStep;

                    // Remove whitespace
                    while (currentStep < strLen &&
                           ((currentChar = InputAsArray[currentStep]) == ' ' ||
                            currentChar == '\r' ||
                            currentChar == '\t' ||
                            currentChar == '\n'))
                    {
                        ++step;
                        ++currentStep;
                    }

                    Skip(step);
                }

                return match;
            }

            /// <summary>
            /// Find the first occurrences of a string, consuming part of the string.
            /// </summary>
            private bool Accept(char[] condition)
            {
                int step = 0;
                int strLen = InputAsArray.Length;
                int currentStep = CurrentIndex;
                char currentChar;

                // Remove whitespace
                while (currentStep < strLen &&
                       ((currentChar = InputAsArray[currentStep]) == ' ' ||
                        currentChar == '\r' ||
                        currentChar == '\t' ||
                        currentChar == '\n'))
                {
                    ++step;
                    ++currentStep;
                }

                bool strMatch = true;
                for (int i = 0; currentStep < strLen && i < condition.Length; ++i, ++currentStep)
                {
                    if (InputAsArray[currentStep] != condition[i])
                    {
                        strMatch = false;
                        break;
                    }
                }

                bool match = (currentStep < strLen) && strMatch;
                if (match)
                {
                    Skip(step + condition.Length);
                }

                return match;
            }
        }

        /// <summary>
        /// Parses a JSON-text as defined in http://tools.ietf.org/html/rfc4627, returning an
        /// IDictionary&lt;string, object&gt; or an IList&lt;object&gt; depending on whether
        /// the value was an array or dictionary. Nested objects also match these types.
        /// </summary>
        public static object Parse(string input)
        {
            object output;
            input = input.Trim();
            JsonStringParser parser = new JsonStringParser(input);

            if ((parser.ParseObject(out output) ||
                 parser.ParseArray(out output)) &&
                parser.CurrentIndex == input.Length)
            {
                return output;
            }

            throw new ArgumentException("Input JSON was invalid.");
        }

        /// <summary>
        /// Encodes a dictionary into a JSON string. Supports values that are
        /// IDictionary&lt;string, object&gt;, IList&lt;object&gt;, strings,
        /// nulls, and any of the primitive types.
        /// </summary>
        public static string Encode(IDictionary<string, object> dict)
        {
            if (dict == null)
            {
                throw new ArgumentNullException();
            }

            if (dict.Count == 0)
            {
                return "{}";
            }

            var builder = new StringBuilder("{");
            foreach (var pair in dict)
            {
                builder.Append(Encode(pair.Key));
                builder.Append(":");
                builder.Append(Encode(pair.Value));
                builder.Append(",");
            }

            builder[builder.Length - 1] = '}';
            return builder.ToString();
        }

        /// <summary>
        /// Encodes a list into a JSON string. Supports values that are
        /// IDictionary&lt;string, object&gt;, IList&lt;object&gt;, strings,
        /// nulls, and any of the primitive types.
        /// </summary>
        public static string Encode(IList<object> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException();
            }

            if (list.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");
            foreach (var item in list)
            {
                builder.Append(Encode(item));
                builder.Append(",");
            }

            builder[builder.Length - 1] = ']';
            return builder.ToString();
        }

        /// <summary>
        /// Encodes an object into a JSON string.
        /// </summary>
        public static string Encode(object obj)
        {
            var dict = obj as IDictionary<string, object>;
            if (dict != null)
            {
                return Encode(dict);
            }

            var list = obj as IList<object>;
            if (list != null)
            {
                return Encode(list);
            }

            var str = obj as string;
            if (str != null)
            {
                str = EscapePattern.Replace(str, m =>
                {
                    switch (m.Value[0])
                    {
                        case '\\':
                            return "\\\\";
                        case '\"':
                            return "\\\"";
                        case '\b':
                            return "\\b";
                        case '\f':
                            return "\\f";
                        case '\n':
                            return "\\n";
                        case '\r':
                            return "\\r";
                        case '\t':
                            return "\\t";
                        default:
                            return "\\u" + ((ushort) m.Value[0]).ToString("x4");
                    }
                });
                return "\"" + str + "\"";
            }

            if (obj == null)
            {
                return "null";
            }

            if (obj is bool)
            {
                if ((bool) obj)
                {
                    return "true";
                }
                else
                {
                    return "false";
                }
            }

            if (!obj.GetType().GetTypeInfo().IsPrimitive)
            {
                throw new ArgumentException("Unable to encode objects of type " + obj.GetType());
            }

            return Convert.ToString(obj, CultureInfo.InvariantCulture);
        }
    }
}