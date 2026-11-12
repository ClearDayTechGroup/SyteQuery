namespace SyteQuery.Features.DatabaseQuery.Services;

/// <summary>What asking an environment's query IDO to identify itself found.</summary>
public enum IdoStatus
{
    /// <summary>The current IDO, and it returned every result set of the test command.</summary>
    Current,

    /// <summary>The original IDO, which returns only the first result set.</summary>
    Older,

    /// <summary>
    /// The current IDO code is running (it answers in the new format) but only one of the test command's two
    /// result sets came back - so SyteLine's database layer isn't passing the second one on.
    /// </summary>
    CurrentButOneResultSet,

    /// <summary>The test command couldn't be run (credentials, IDO missing, SyteLine down, ...).</summary>
    Unreachable
}

/// <param name="Status">What was found.</param>
/// <param name="FormatVersion">The output format the IDO answered in (1 or 2), when it answered.</param>
/// <param name="Summary">A sentence or two for the user, saying what it means and what to do.</param>
public sealed record IdoProbeResult(IdoStatus Status, int? FormatVersion, string Summary);
