using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public static class WorldFileValidator
{
    public const int MaxFileSizeBytes = 1024 * 1024;
    public const int MaxBlocks = 256;
    public const int MaxWireBlocks = 128;
    public const int MaxGates = 32;
    public const int MaxConnections = 128;
    public const int MaxLogicNodes = 96;

    private const int MaxJsonDepth = 32;
    private const int MaxWorldCoordinate = 10000;
    private const float MaxScale = 1000f;
    private const int MaxFootprintDimension = 16;

    public static bool TryReadFile(string path, out string json, out string error)
    {
        json = null;
        error = null;

        if (string.IsNullOrWhiteSpace(path) || !string.Equals(
            Path.GetExtension(path),
            ".json",
            StringComparison.OrdinalIgnoreCase))
        {
            error = "Select a file with the .json extension.";
            return false;
        }

        try
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaxFileSizeBytes)
                {
                    error = $"World files must be {MaxFileSizeBytes / 1024} KB or smaller.";
                    return false;
                }

                using (MemoryStream contents = new MemoryStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (contents.Length + bytesRead > MaxFileSizeBytes)
                        {
                            error = $"World files must be {MaxFileSizeBytes / 1024} KB or smaller.";
                            return false;
                        }
                        contents.Write(buffer, 0, bytesRead);
                    }

                    json = new UTF8Encoding(false, true).GetString(contents.ToArray());
                }
            }
            if (json.Length > 0 && json[0] == '\uFEFF') json = json.Substring(1);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is DecoderFallbackException)
        {
            error = $"Could not read the selected world file: {exception.Message}";
            return false;
        }
    }

    public static bool TryValidate(string json, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The selected file is empty.";
            return false;
        }
        if (Encoding.UTF8.GetByteCount(json) > MaxFileSizeBytes)
        {
            error = $"World files must be {MaxFileSizeBytes / 1024} KB or smaller.";
            return false;
        }

        object parsed;
        try
        {
            parsed = new JsonParser(json).Parse();
        }
        catch (FormatException exception)
        {
            error = "The selected file is not valid JSON: " + exception.Message;
            return false;
        }

        if (!(parsed is Dictionary<string, object> root))
        {
            error = "The JSON root must be a Silicon Sandbox world object.";
            return false;
        }

        if (!TryGetInteger(root, "version", out int version) ||
            version < 0 || version > WorldSaveSystem.CurrentSaveVersion)
        {
            error = "The world save is missing a supported integer version.";
            return false;
        }
        if (!TryGetString(root, "worldName", out string worldName) ||
            string.IsNullOrWhiteSpace(worldName) ||
            worldName.Length > MenuTraversal.MaxWorldNameCharacters ||
            ContainsControlCharacters(worldName))
        {
            error = $"The world save must contain a valid worldName of 1-{MenuTraversal.MaxWorldNameCharacters} characters.";
            return false;
        }
        if (!root.TryGetValue("blocks", out object blocksValue) ||
            !(blocksValue is List<object> blocks))
        {
            error = "The world save must contain a blocks array.";
            return false;
        }
        if (blocks.Count > MaxBlocks)
        {
            error = $"The world contains too many blocks. The development limit is {MaxBlocks}.";
            return false;
        }

        for (int index = 0; index < blocks.Count; index++)
        {
            if (!(blocks[index] is Dictionary<string, object> block))
            {
                error = $"Block {index} must be a JSON object.";
                return false;
            }
            if (!TryGetString(block, "prefabName", out string prefabName) ||
                string.IsNullOrWhiteSpace(prefabName))
            {
                error = $"Block {index} is missing a valid prefabName.";
                return false;
            }
            if (!TryGetVector(block, "position", 3, out double[] position) ||
                !WithinWorldBounds(position))
            {
                error = $"Block {index} has a missing or invalid position.";
                return false;
            }
            if (!TryGetVector(block, "rotation", 4, out double[] rotation) ||
                !IsMeaningfulRotation(rotation))
            {
                error = $"Block {index} has a missing or invalid rotation.";
                return false;
            }

            if (block.TryGetValue("scale", out object scaleValue))
            {
                if (!TryVector(scaleValue, 3, out double[] scale) ||
                    !IsValidScale(scale, version < WorldSaveSystem.CurrentSaveVersion))
                {
                    error = $"Block {index} has an invalid scale.";
                    return false;
                }
            }
            else if (version == WorldSaveSystem.CurrentSaveVersion)
            {
                error = $"Block {index} is missing the required scale field.";
                return false;
            }

            if (block.TryGetValue("footprintSize", out object footprintValue))
            {
                if (!TryVector(footprintValue, 3, out double[] footprint) ||
                    !IsValidFootprint(footprint, version < WorldSaveSystem.CurrentSaveVersion))
                {
                    error = $"Block {index} has an invalid footprintSize.";
                    return false;
                }
            }
            else if (version == WorldSaveSystem.CurrentSaveVersion)
            {
                error = $"Block {index} is missing the required footprintSize field.";
                return false;
            }

            if (!TryOptionalBoolean(block, "isGround", version, out error) ||
                !TryOptionalBoolean(block, "hasWireEndpointMask", version, out error) ||
                !TryOptionalBoolean(block, "hasSourceState", version, out error))
            {
                error = $"Block {index} has a missing or invalid boolean field. {error}";
                return false;
            }
            if (!TryOptionalMask(block, "wireEndpointMask", version, out error) ||
                !TryOptionalMask(block, "wireJoinMask", version, out error))
            {
                error = $"Block {index} has an invalid wire mask. {error}";
                return false;
            }
            if (!TryOptionalInteger(block, "sourceState", version, 0, 1, out error))
            {
                error = $"Block {index} has an invalid sourceState. {error}";
                return false;
            }
        }

        return true;
    }

    private static bool TryOptionalBoolean(
        Dictionary<string, object> block,
        string field,
        int version,
        out string error)
    {
        error = null;
        if (!block.TryGetValue(field, out object value))
        {
            if (version == WorldSaveSystem.CurrentSaveVersion)
            {
                error = $"'{field}' is required.";
                return false;
            }
            return true;
        }

        if (value is bool) return true;
        error = $"'{field}' must be true or false.";
        return false;
    }

    private static bool TryOptionalMask(
        Dictionary<string, object> block,
        string field,
        int version,
        out string error)
    {
        error = null;
        if (!block.TryGetValue(field, out object value))
        {
            if (version == WorldSaveSystem.CurrentSaveVersion && field != "wireJoinMask")
            {
                error = $"'{field}' is required.";
                return false;
            }
            return true;
        }

        if (TryInteger(value, out int mask) && mask >= 0 && mask <= 0x3f) return true;
        error = $"'{field}' must be an integer wire mask from 0 to 63.";
        return false;
    }

    private static bool TryOptionalInteger(
        Dictionary<string, object> block,
        string field,
        int version,
        int minimum,
        int maximum,
        out string error)
    {
        error = null;
        if (!block.TryGetValue(field, out object value))
        {
            if (version == WorldSaveSystem.CurrentSaveVersion)
            {
                error = $"'{field}' is required.";
                return false;
            }
            return true;
        }

        if (TryInteger(value, out int number) && number >= minimum && number <= maximum) return true;
        error = $"'{field}' must be an integer from {minimum} to {maximum}.";
        return false;
    }

    private static bool TryGetString(Dictionary<string, object> source, string field, out string value)
    {
        value = null;
        if (!source.TryGetValue(field, out object raw) || !(raw is string text)) return false;
        value = text;
        return true;
    }

    private static bool TryGetInteger(Dictionary<string, object> source, string field, out int value)
    {
        value = 0;
        return source.TryGetValue(field, out object raw) && TryInteger(raw, out value);
    }

    private static bool TryInteger(object raw, out int value)
    {
        value = 0;
        if (!(raw is double number) || number < int.MinValue || number > int.MaxValue ||
            Math.Truncate(number) != number)
        {
            return false;
        }

        value = (int)number;
        return true;
    }

    private static bool TryGetVector(
        Dictionary<string, object> source,
        string field,
        int componentCount,
        out double[] components)
    {
        components = null;
        return source.TryGetValue(field, out object raw) && TryVector(raw, componentCount, out components);
    }

    private static bool TryVector(object raw, int componentCount, out double[] components)
    {
        components = null;
        if (!(raw is Dictionary<string, object> vector)) return false;
        string[] names = componentCount == 4
            ? new[] { "x", "y", "z", "w" }
            : new[] { "x", "y", "z" };
        components = new double[componentCount];
        for (int index = 0; index < componentCount; index++)
        {
            if (!vector.TryGetValue(names[index], out object value) ||
                !(value is double number) || double.IsNaN(number) || double.IsInfinity(number))
            {
                components = null;
                return false;
            }
            components[index] = number;
        }

        return true;
    }

    private static bool WithinWorldBounds(double[] position)
    {
        foreach (double value in position)
        {
            if (value < -MaxWorldCoordinate || value > MaxWorldCoordinate) return false;
        }
        return true;
    }

    private static bool ContainsControlCharacters(string value)
    {
        foreach (char character in value)
        {
            if (char.IsControl(character)) return true;
        }
        return false;
    }

    private static bool IsMeaningfulRotation(double[] rotation)
    {
        double magnitudeSquared = 0;
        foreach (double component in rotation)
        {
            if (Math.Abs(component) > 1.001) return false;
            magnitudeSquared += component * component;
        }
        return magnitudeSquared >= 0.25 && magnitudeSquared <= 2.25;
    }

    private static bool IsValidScale(double[] scale, bool allowLegacyZero)
    {
        bool allZero = true;
        foreach (double value in scale)
        {
            if (value != 0) allZero = false;
        }
        if (allowLegacyZero && allZero) return true;

        foreach (double value in scale)
        {
            if (value <= 0 || value > MaxScale) return false;
        }
        return true;
    }

    private static bool IsValidFootprint(double[] footprint, bool allowLegacyZero)
    {
        foreach (double value in footprint)
        {
            if (allowLegacyZero && value == 0) continue;
            if (value < 1 || value > MaxFootprintDimension || Math.Truncate(value) != value) return false;
        }
        return true;
    }

    private sealed class JsonParser
    {
        private readonly string text;
        private int index;

        public JsonParser(string text)
        {
            this.text = text;
        }

        public object Parse()
        {
            SkipWhitespace();
            object value = ParseValue(0);
            SkipWhitespace();
            if (index != text.Length) Fail("unexpected content after the root value");
            return value;
        }

        private object ParseValue(int depth)
        {
            if (depth > MaxJsonDepth) Fail("JSON nesting is too deep");
            SkipWhitespace();
            if (index >= text.Length) Fail("unexpected end of file");

            switch (text[index])
            {
                case '{': return ParseObject(depth + 1);
                case '[': return ParseArray(depth + 1);
                case '"': return ParseString();
                case 't': ReadLiteral("true"); return true;
                case 'f': ReadLiteral("false"); return false;
                case 'n': ReadLiteral("null"); return null;
                default: return ParseNumber();
            }
        }

        private Dictionary<string, object> ParseObject(int depth)
        {
            index++;
            SkipWhitespace();
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (TryConsume('}')) return result;

            while (true)
            {
                SkipWhitespace();
                if (index >= text.Length || text[index] != '"') Fail("object keys must be strings");
                string key = ParseString();
                SkipWhitespace();
                Require(':');
                object value = ParseValue(depth);
                if (result.ContainsKey(key)) Fail("duplicate object key '" + key + "'");
                result.Add(key, value);
                SkipWhitespace();
                if (TryConsume('}')) return result;
                Require(',');
            }
        }

        private List<object> ParseArray(int depth)
        {
            index++;
            SkipWhitespace();
            var result = new List<object>();
            if (TryConsume(']')) return result;

            while (true)
            {
                result.Add(ParseValue(depth));
                SkipWhitespace();
                if (TryConsume(']')) return result;
                Require(',');
            }
        }

        private string ParseString()
        {
            Require('"');
            var result = new StringBuilder();
            while (index < text.Length)
            {
                char character = text[index++];
                if (character == '"') return result.ToString();
                if (character < 0x20) Fail("string contains an unescaped control character");
                if (character != '\\')
                {
                    result.Append(character);
                    continue;
                }

                if (index >= text.Length) Fail("incomplete string escape");
                char escape = text[index++];
                switch (escape)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u': result.Append(ReadUnicodeEscape()); break;
                    default: Fail("invalid string escape"); break;
                }
            }

            Fail("unterminated string");
            return null;
        }

        private char ReadUnicodeEscape()
        {
            if (index + 4 > text.Length) Fail("incomplete Unicode escape");
            int value = 0;
            for (int count = 0; count < 4; count++)
            {
                char digit = text[index++];
                int parsed = digit >= '0' && digit <= '9' ? digit - '0'
                    : digit >= 'a' && digit <= 'f' ? digit - 'a' + 10
                    : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10
                    : -1;
                if (parsed < 0) Fail("invalid Unicode escape");
                value = value * 16 + parsed;
            }
            return (char)value;
        }

        private double ParseNumber()
        {
            int start = index;
            if (TryConsume('-') && index >= text.Length) Fail("incomplete number");

            if (TryConsume('0'))
            {
                if (index < text.Length && char.IsDigit(text[index])) Fail("number has a leading zero");
            }
            else
            {
                ReadDigits(required: true);
            }

            if (TryConsume('.')) ReadDigits(required: true);
            if (TryConsume('e') || TryConsume('E'))
            {
                if (!TryConsume('+')) TryConsume('-');
                ReadDigits(required: true);
            }

            string numberText = text.Substring(start, index - start);
            if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
                double.IsNaN(number) || double.IsInfinity(number))
            {
                Fail("invalid or out-of-range number");
            }
            return number;
        }

        private void ReadDigits(bool required)
        {
            int start = index;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
            if (required && start == index) Fail("number is missing digits");
        }

        private void ReadLiteral(string literal)
        {
            if (index + literal.Length > text.Length ||
                string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                Fail("invalid JSON value");
            }
            index += literal.Length;
        }

        private void SkipWhitespace()
        {
            while (index < text.Length &&
                   (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n'))
            {
                index++;
            }
        }

        private bool TryConsume(char expected)
        {
            if (index >= text.Length || text[index] != expected) return false;
            index++;
            return true;
        }

        private void Require(char expected)
        {
            if (!TryConsume(expected)) Fail("expected '" + expected + "'");
        }

        private void Fail(string message)
        {
            throw new FormatException($"{message} at character {index}.");
        }
    }
}
