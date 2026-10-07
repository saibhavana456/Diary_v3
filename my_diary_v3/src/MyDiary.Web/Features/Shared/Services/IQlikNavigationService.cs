using MyDiary.Web.Features.Reports.Models;

namespace MyDiary.Web.Features.Shared.Services
{
    /// <summary>
    /// Handles Qlik Sense auto sign-in: fetches or reuses a valid ticket,
    /// builds the final URL, and opens it in a new browser tab.
    /// </summary>
    public interface IQlikNavigationService
    {
        /// <summary>
        /// Fetches a Qlik ticket for <paramref name="userId"/> and opens
        /// <paramref name="qlikPath"/> (relative path appended to hubUrl) in a new tab.
        /// If <paramref name="qlikPath"/> is already an absolute URL the hubUrl prefix is skipped.
        /// </summary>
        Task NavigateAsync(string userId, string qlikPath);
    }
}
