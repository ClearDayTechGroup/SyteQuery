using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.IO;

namespace SyteQuery.Features.SqlFormatting.Services;

public sealed class ScriptDomSqlFormatter : ISqlFormatter
{
    public string Format(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        // SQL ScriptDom parsers are versioned; 150 = SQL Server 2019 syntax.
        // If you need newer syntax, switch to 160 (SQL Server 2022).
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);

        IList<ParseError> errors;
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out errors);

        // If it fails to parse, just return the original so we don’t break the UI.
        if (errors is { Count: > 0 } || fragment is null)
            return sql;

        var options = new SqlScriptGeneratorOptions
        {
            AlignClauseBodies = true,
            AlignSetClauseItem = true,
            AsKeywordOnOwnLine = true,
            IndentationSize = 4,
            KeywordCasing = KeywordCasing.Uppercase,
            IncludeSemicolons = true,
            NewLineBeforeFromClause = true,
            NewLineBeforeJoinClause = true,
            NewLineBeforeWhereClause = true,
            NewLineBeforeGroupByClause = true,
            NewLineBeforeOrderByClause = true,
        };

        var generator = new Sql150ScriptGenerator(options);
        generator.GenerateScript(fragment, out var formatted);

        return formatted ?? sql;
    }
}
