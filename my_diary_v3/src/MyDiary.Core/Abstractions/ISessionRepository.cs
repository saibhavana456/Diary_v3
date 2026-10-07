namespace MyDiary.Core.Abstractions;

/// <summary>
/// Manages user session records in SQL Server (session_dtls table).
/// Fixed from Project B: uses parameterized queries instead of string interpolation.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Returns true when no active session exists for the user (login is allowed).
    /// Active = session created within the last 20 minutes.
    /// </summary>
    Task<bool> CanLoginAsync(string userId, CancellationToken ct = default);

    Task AddSessionAsync(string userId, string ipAddress, CancellationToken ct = default);

    Task RemoveSessionAsync(string userId, CancellationToken ct = default);
}
