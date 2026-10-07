using MyDiary.Core;
using MyDiary.Core.Models;
using MyDiary.Core.Abstractions;
using System.Data;

namespace MyDiary.Data.Extensions;

/// <summary>
/// Shared Dapper helper utilities for Oracle and SQL Server repositories.
/// All helpers enforce parameterized queries — never string concatenation.
/// </summary>
public static class DapperExtensions
{
    /// <summary>Safely reads a nullable string from a DataReader value.</summary>
    public static string? NullableString(object? value) =>
        value == null || value == DBNull.Value ? null : value.ToString();

    /// <summary>Safely reads a nullable DateTime from a DataReader value.</summary>
    public static DateTime? NullableDateTime(object? value) =>
        value == null || value == DBNull.Value ? null : Convert.ToDateTime(value);

    /// <summary>Safely reads a nullable int from a DataReader value.</summary>
    public static int? NullableInt(object? value) =>
        value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
}
