using System.Globalization;
using System.Text;

namespace Evolution.Telemetry
{
    // Minimal JSON writer, so the core does not depend on JsonUtility or Newtonsoft.
    // Only what the telemetry and the save file need: objects, arrays, numbers, bools and strings.
    // JsonNode (Evolution/Persistence) reads it back.
    public class JsonBuilder
    {
        private readonly StringBuilder _text = new StringBuilder();
        private bool _needsComma;

        // false (default): numbers with at most 4 decimals, good for reading the logs.
        // true: numbers written so that reading them back gives exactly the same value (used by the save file).
        public bool ExactNumbers { get; set; }

        public void BeginObject(string name = null)
        {
            Key(name);
            _text.Append('{');
            _needsComma = false;
        }

        public void EndObject()
        {
            _text.Append('}');
            _needsComma = true;
        }

        public void BeginArray(string name)
        {
            Key(name);
            _text.Append('[');
            _needsComma = false;
        }

        public void EndArray()
        {
            _text.Append(']');
            _needsComma = true;
        }

        // Appends an already built JSON value (for example another object's ToJson()).
        public void RawValue(string json) => RawField(null, json);

        // Same, as a named field of the current object.
        public void RawField(string name, string json)
        {
            Key(name);
            _text.Append(json);
            _needsComma = true;
        }

        public void Field(string name, int value) => Value(name, value.ToString(CultureInfo.InvariantCulture));
        public void Field(string name, float value) => Value(name, Number(value));
        public void Field(string name, double value) => Value(name, Number(value));
        public void Field(string name, bool value) => Value(name, value ? "true" : "false");
        public void Field(string name, string value) => Value(name, value == null ? "null" : Quote(value));

        public void Field(string name, float[] values)
        {
            if (values == null)
            {
                Value(name, "null");
                return;
            }

            BeginArray(name);
            foreach (var v in values)
            {
                if (_needsComma) _text.Append(',');
                _text.Append(Number(v));
                _needsComma = true;
            }
            EndArray();
        }

        public void Field(string name, int[] values)
        {
            if (values == null)
            {
                Value(name, "null");
                return;
            }

            BeginArray(name);
            foreach (var v in values)
            {
                if (_needsComma) _text.Append(',');
                _text.Append(v.ToString(CultureInfo.InvariantCulture));
                _needsComma = true;
            }
            EndArray();
        }

        public void Field(string name, string[] values)
        {
            if (values == null)
            {
                Value(name, "null");
                return;
            }

            BeginArray(name);
            foreach (var v in values)
            {
                if (_needsComma) _text.Append(',');
                _text.Append(v == null ? "null" : Quote(v));
                _needsComma = true;
            }
            EndArray();
        }

        public override string ToString() => _text.ToString();

        private void Value(string name, string raw)
        {
            Key(name);
            _text.Append(raw);
            _needsComma = true;
        }

        private void Key(string name)
        {
            if (_needsComma) _text.Append(',');
            if (name != null) _text.Append(Quote(name)).Append(':');
        }

        // Invariant culture: a Brazilian Windows would otherwise write 1,5 instead of 1.5.
        // "R" (round-trip) writes as many digits as needed to read back the very same float.
        private string Number(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "0";
            return value.ToString(ExactNumbers ? "R" : "0.####", CultureInfo.InvariantCulture);
        }

        private string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString(ExactNumbers ? "R" : "0.######", CultureInfo.InvariantCulture);
        }

        private static string Quote(string value)
        {
            var text = new StringBuilder("\"");
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default: text.Append(c); break;
                }
            }
            return text.Append('"').ToString();
        }
    }
}
