using Microsoft.JSInterop;
using MyDiary.Web.Features.MIS.Services;

namespace MyDiary.Web.Features.Shared.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IQlikNavigationService"/>.
    /// Delegates ticket retrieval to <see cref="IMISService.GetTicketAsync"/> so that
    /// the caching / DB-insert logic lives in one place.
    /// </summary>
    public class QlikNavigationService : IQlikNavigationService
    {
        private readonly IMISService _misService;
        private readonly IJSRuntime _js;

        public QlikNavigationService(IMISService misService, IJSRuntime js)
        {
            _misService = misService;
            _js = js;
        }

        /// <inheritdoc />
        public async Task NavigateAsync(string userId, string qlikPath)
        {
            if (string.IsNullOrWhiteSpace(qlikPath)) return;

            var resp = await _misService.GetTicketAsync(userId);

            string finalUrl;
            if (resp != null && !string.IsNullOrEmpty(resp.ticket))
            {
                // If qlikPath is already absolute (e.g. from QlikLinks config) use it directly;
                // otherwise prepend the hub base URL returned by the ticket API.
                bool isAbsolute = Uri.IsWellFormedUriString(qlikPath, UriKind.Absolute);
                string baseUrl = isAbsolute ? qlikPath : $"{resp.hubUrl}{qlikPath}";
                finalUrl = $"{baseUrl}?qlikTicket={resp.ticket}";
            }
            else
            {
                // Fallback: open the raw path without a ticket
                finalUrl = qlikPath;
            }

            if (Uri.IsWellFormedUriString(finalUrl, UriKind.Absolute))
            {
                await _js.InvokeVoidAsync("open", finalUrl, "_blank", "noopener,noreferrer");
            }
        }
    }
}
