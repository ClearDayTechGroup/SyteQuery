using System.Data;
using System.Reflection;
using System.Text;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.QueryEditor.Models;

namespace SyteQuery.Tests;

/// <summary>
/// A stand-in for the environment manager that answers every query with whatever the test says. Built with
/// DispatchProxy so the tests don't have to implement the manager's many unrelated members.
/// </summary>
public class EnvironmentManagerProxy : DispatchProxy
{
    public Func<string, QueryResult> OnExecute { get; set; } = _ => QueryResult.Success("", "");

    public static IEnvironmentSessionManager Create(Func<string, QueryResult> onExecute)
    {
        var proxy = DispatchProxy.Create<IEnvironmentSessionManager, EnvironmentManagerProxy>();
        ((EnvironmentManagerProxy)(object)proxy).OnExecute = onExecute;
        return proxy;
    }

    /// <summary>Answers the n-th query with the n-th payload (then repeats the last).</summary>
    public static IEnvironmentSessionManager CreateSequence(params string[] payloads)
    {
        var next = 0;
        return Create(_ => QueryResult.Success(payloads[Math.Min(next++, payloads.Length - 1)], ""));
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == nameof(IEnvironmentSessionManager.ExecuteAsync))
            return Task.FromResult(OnExecute((string)args![1]!));

        var type = targetMethod.ReturnType;
        if (type == typeof(void)) return null;
        if (type == typeof(Task)) return Task.CompletedTask;
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

/// <summary>Wraps a real reader and fails on demand, to simulate a command whose later statement errors.</summary>
public class FailingReaderProxy : DispatchProxy
{
    private IDataReader _inner = null!;
    private int _failOnNextResult = int.MaxValue;
    private int _failOnRead = int.MaxValue;
    private int _nextResults;
    private int _reads;

    /// <param name="failOnNextResult">Throw on the n-th call to NextResult (1-based).</param>
    /// <param name="failOnRead">Throw on the n-th call to Read (1-based, counted across result sets).</param>
    public static IDataReader Wrap(IDataReader inner, int failOnNextResult = int.MaxValue, int failOnRead = int.MaxValue)
    {
        var proxy = DispatchProxy.Create<IDataReader, FailingReaderProxy>();
        var self = (FailingReaderProxy)(object)proxy;
        self._inner = inner;
        self._failOnNextResult = failOnNextResult;
        self._failOnRead = failOnRead;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == nameof(IDataReader.NextResult) && ++_nextResults == _failOnNextResult)
            throw new InvalidOperationException("Divide by zero error encountered.");
        if (targetMethod.Name == nameof(IDataReader.Read) && ++_reads == _failOnRead)
            throw new InvalidOperationException("Arithmetic overflow error.");

        try
        {
            return targetMethod.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}

/// <summary>An IDataRecord that only knows its column names.</summary>
public class NamesRecordProxy : DispatchProxy
{
    private string[] _names = Array.Empty<string>();

    public static IDataRecord Create(string[] names)
    {
        var proxy = DispatchProxy.Create<IDataRecord, NamesRecordProxy>();
        ((NamesRecordProxy)(object)proxy)._names = names;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod!.Name == nameof(IDataRecord.GetName) ? _names[(int)args![0]!] : null;
}

public static class Payloads
{
    public static string Base64(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    /// <summary>A version-2 payload with one result set of <paramref name="rows"/> rows in column <paramref name="column"/>.</summary>
    public static string Set(string column, int rows) =>
        "{\"version\":2,\"resultSets\":[{\"columns\":[\"" + column + "\"],\"rows\":[" +
        string.Join(",", Enumerable.Range(1, rows).Select(i => "{\"" + column + "\":" + i + "}")) + "]}]}";
}
