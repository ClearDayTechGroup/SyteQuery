namespace SyteQuery.Features.Metadata.Models;

/// <summary>
/// Represents information about a database trigger.
/// </summary>
public sealed record TriggerInfo(string Name, bool IsEnabled);
