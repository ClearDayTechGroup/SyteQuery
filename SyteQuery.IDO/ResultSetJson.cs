using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;

namespace QueryTool
{
    /// <summary>
    /// Turns everything a command returned - every result set, not just the first - into the JSON
    /// SyteQuery reads. Deliberately free of any SyteLine/Infor types so it can be tested on its own.
    ///
    /// Shape (version 2):
    ///   {"version":2,"resultSets":[{"columns":["a","b"],"rows":[{"a":1,"b":"x"}]}, ...]}
    /// "columns" is always present, so a result set with no rows still has its column names.
    /// </summary>
    public static class ResultSetJson
    {
        /// <summary>The output format version. SyteQuery refuses a newer one than it understands.</summary>
        public const int Version = 2;

        /// <summary>
        /// Reads every result set from <paramref name="reader"/> (advancing with NextResult) and returns the JSON.
        /// Statements that produce no result set (INSERT, UPDATE, ...) are skipped, so a command with none
        /// gives an empty "resultSets" array.
        /// </summary>
        public static string Serialize(IDataReader reader)
        {
            string error;
            return Serialize(reader, out error);
        }

        /// <summary>
        /// Same, but if the command fails part-way (say the second statement errors) the result sets read before
        /// the failure are kept and the message is returned in <paramref name="error"/> and in the JSON as
        /// "error" - like SSMS showing the first grid and then the error. <paramref name="error"/> is null on success.
        /// </summary>
        public static string Serialize(IDataReader reader, out string error)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            error = null;
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture))
            using (var w = new JsonTextWriter(sw))
            {
                w.WriteStartObject();
                w.WritePropertyName("version");
                w.WriteValue(Version);
                w.WritePropertyName("resultSets");
                w.WriteStartArray();

                try
                {
                    do
                    {
                        if (reader.FieldCount > 0)
                            WriteResultSet(reader, w);
                    }
                    while (reader.NextResult());
                }
                catch (Exception ex)
                {
                    // WriteResultSet always leaves the set it was writing well-formed (see its finally).
                    error = ex.Message;
                }

                w.WriteEndArray();
                if (error != null)
                {
                    w.WritePropertyName("error");
                    w.WriteValue(error);
                }
                w.WriteEndObject();
            }

            return sb.ToString();
        }

        private static void WriteResultSet(IDataReader reader, JsonWriter w)
        {
            var count = reader.FieldCount;
            var names = UniqueColumnNames(reader, count);

            w.WriteStartObject();

            w.WritePropertyName("columns");
            w.WriteStartArray();
            foreach (var name in names)
                w.WriteValue(name);
            w.WriteEndArray();

            w.WritePropertyName("rows");
            w.WriteStartArray();
            try
            {
                var values = new object[count];
                while (reader.Read())
                {
                    reader.GetValues(values);
                    w.WriteStartObject();
                    for (var i = 0; i < count; i++)
                    {
                        w.WritePropertyName(names[i]);
                        WriteCell(w, values[i]);
                    }
                    w.WriteEndObject();
                }
            }
            finally
            {
                // Even if reading fails part-way, keep the rows so far and close the set properly.
                w.WriteEndArray();
                w.WriteEndObject();
            }
        }

        /// <summary>
        /// Column names must be unique to be JSON object keys (a join like "a.id, b.id" repeats "id"), and an
        /// unnamed column ("SELECT 1") has no name at all. Duplicates become id, id1, id2...; unnamed ones
        /// become Column1, Column2... (the same names a DataTable would give them).
        /// </summary>
        public static string[] UniqueColumnNames(IDataRecord record, int count)
        {
            var names = new string[count];
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < count; i++)
            {
                var name = record.GetName(i);
                if (string.IsNullOrEmpty(name))
                    name = "Column" + (i + 1).ToString(CultureInfo.InvariantCulture);

                var candidate = name;
                var suffix = 1;
                while (!used.Add(candidate))
                    candidate = name + (suffix++).ToString(CultureInfo.InvariantCulture);

                names[i] = candidate;
            }

            return names;
        }

        private static void WriteCell(JsonWriter w, object value)
        {
            if (value == null || value is DBNull)
            {
                w.WriteNull();
            }
            else if (value is IConvertible || value is DateTimeOffset || value is TimeSpan || value is Guid || value is byte[])
            {
                w.WriteValue(value);
            }
            else
            {
                // Types JSON has no notion of (hierarchyid, geography, ...): fall back to their text form.
                w.WriteValue(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }
    }
}
