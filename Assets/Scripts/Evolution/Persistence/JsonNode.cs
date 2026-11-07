using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Evolution.Persistence
{
    // Minimal JSON reader, the other half of JsonBuilder. Parses text into a tree of nodes.
    // A missing key or index returns an empty node instead of throwing, so reading an old save file
    // that does not have a new field simply gives the fallback value.
    public sealed class JsonNode
    {
        public enum NodeType { Missing, Null, Bool, Number, String, Array, Object }

        private static readonly JsonNode MissingNode = new JsonNode(NodeType.Missing);

        public NodeType Type { get; }

        private bool _bool;
        private double _number;
        private string _string;
        private List<JsonNode> _items;
        private Dictionary<string, JsonNode> _fields;

        private JsonNode(NodeType type)
        {
            Type = type;
        }

        public bool Exists => Type != NodeType.Missing && Type != NodeType.Null;

        public JsonNode this[string key] =>
            Type == NodeType.Object && _fields.TryGetValue(key, out var value) ? value : MissingNode;

        public JsonNode this[int index] =>
            Type == NodeType.Array && index >= 0 && index < _items.Count ? _items[index] : MissingNode;

        public int Count => Type == NodeType.Array ? _items.Count : Type == NodeType.Object ? _fields.Count : 0;

        public IEnumerable<JsonNode> Items => Type == NodeType.Array ? _items : (IEnumerable<JsonNode>)Array.Empty<JsonNode>();

        // ---------------- Typed reads ----------------

        public double AsDouble(double fallback = 0) => Type == NodeType.Number ? _number : fallback;
        public float AsFloat(float fallback = 0f) => Type == NodeType.Number ? (float)_number : fallback;
        public int AsInt(int fallback = 0) => Type == NodeType.Number ? (int)Math.Round(_number) : fallback;
        public bool AsBool(bool fallback = false) => Type == NodeType.Bool ? _bool : fallback;
        public string AsString(string fallback = null) => Type == NodeType.String ? _string : fallback;

        public float[] AsFloatArray()
        {
            if (Type != NodeType.Array) return null;
            var values = new float[_items.Count];
            for (var i = 0; i < values.Length; i++) values[i] = _items[i].AsFloat();
            return values;
        }

        public int[] AsIntArray()
        {
            if (Type != NodeType.Array) return null;
            var values = new int[_items.Count];
            for (var i = 0; i < values.Length; i++) values[i] = _items[i].AsInt();
            return values;
        }

        // ---------------- Parser ----------------

        public static JsonNode Parse(string text)
        {
            if (text == null) throw new FormatException("JSON text is null.");
            var position = 0;
            var root = ParseValue(text, ref position);
            SkipWhitespace(text, ref position);
            if (position != text.Length) throw Error(text, position, "unexpected text after the end");
            return root;
        }

        private static JsonNode ParseValue(string text, ref int position)
        {
            SkipWhitespace(text, ref position);
            if (position >= text.Length) throw Error(text, position, "unexpected end");

            var c = text[position];
            switch (c)
            {
                case '{': return ParseObject(text, ref position);
                case '[': return ParseArray(text, ref position);
                case '"': return new JsonNode(NodeType.String) { _string = ParseString(text, ref position) };
                case 't': Expect(text, ref position, "true"); return new JsonNode(NodeType.Bool) { _bool = true };
                case 'f': Expect(text, ref position, "false"); return new JsonNode(NodeType.Bool) { _bool = false };
                case 'n': Expect(text, ref position, "null"); return new JsonNode(NodeType.Null);
                default:
                    if (c == '-' || char.IsDigit(c)) return ParseNumber(text, ref position);
                    throw Error(text, position, $"unexpected character '{c}'");
            }
        }

        private static JsonNode ParseObject(string text, ref int position)
        {
            var node = new JsonNode(NodeType.Object) { _fields = new Dictionary<string, JsonNode>() };
            position++; // {
            SkipWhitespace(text, ref position);
            if (Peek(text, position) == '}')
            {
                position++;
                return node;
            }

            while (true)
            {
                SkipWhitespace(text, ref position);
                if (Peek(text, position) != '"') throw Error(text, position, "expected a key");
                var key = ParseString(text, ref position);

                SkipWhitespace(text, ref position);
                if (Peek(text, position) != ':') throw Error(text, position, "expected ':'");
                position++;

                node._fields[key] = ParseValue(text, ref position);

                SkipWhitespace(text, ref position);
                var next = Peek(text, position);
                position++;
                if (next == '}') return node;
                if (next != ',') throw Error(text, position - 1, "expected ',' or '}'");
            }
        }

        private static JsonNode ParseArray(string text, ref int position)
        {
            var node = new JsonNode(NodeType.Array) { _items = new List<JsonNode>() };
            position++; // [
            SkipWhitespace(text, ref position);
            if (Peek(text, position) == ']')
            {
                position++;
                return node;
            }

            while (true)
            {
                node._items.Add(ParseValue(text, ref position));

                SkipWhitespace(text, ref position);
                var next = Peek(text, position);
                position++;
                if (next == ']') return node;
                if (next != ',') throw Error(text, position - 1, "expected ',' or ']'");
            }
        }

        private static string ParseString(string text, ref int position)
        {
            position++; // opening quote
            var result = new StringBuilder();
            while (position < text.Length)
            {
                var c = text[position++];
                if (c == '"') return result.ToString();
                if (c != '\\')
                {
                    result.Append(c);
                    continue;
                }

                if (position >= text.Length) break;
                var escaped = text[position++];
                switch (escaped)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (position + 4 > text.Length) throw Error(text, position, "bad \\u escape");
                        result.Append((char)int.Parse(text.Substring(position, 4), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture));
                        position += 4;
                        break;
                    default: throw Error(text, position - 1, $"bad escape '\\{escaped}'");
                }
            }
            throw Error(text, position, "unterminated string");
        }

        private static JsonNode ParseNumber(string text, ref int position)
        {
            var start = position;
            while (position < text.Length && "+-0123456789.eE".IndexOf(text[position]) >= 0) position++;

            var raw = text.Substring(start, position - start);
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw Error(text, start, $"bad number '{raw}'");
            return new JsonNode(NodeType.Number) { _number = value };
        }

        private static void Expect(string text, ref int position, string word)
        {
            if (string.CompareOrdinal(text, position, word, 0, word.Length) != 0)
                throw Error(text, position, $"expected '{word}'");
            position += word.Length;
        }

        private static char Peek(string text, int position) => position < text.Length ? text[position] : '\0';

        private static void SkipWhitespace(string text, ref int position)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
        }

        private static FormatException Error(string text, int position, string message) =>
            new FormatException($"Invalid JSON at character {position}: {message}.");
    }
}
