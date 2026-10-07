using Microsoft.JSInterop;
using System.Text.Json;

namespace MyDiary.Web.Core.Services.Storage
{
    /// <summary>
    /// Plain-text session storage for load testing — values are visible in browser DevTools.
    /// Gracefully handles prerender and circuit reconnection scenarios.
    /// </summary>
    public class PlainSessionStorageService : ISessionStorageService
    {
        private readonly IJSRuntime _jsRuntime;

        public PlainSessionStorageService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task SetAsync<T>(string key, T value)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, json);
            }
            catch (InvalidOperationException)
            {
                // JS interop not available during prerender or circuit reconnection — safe to ignore
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected — safe to ignore
            }
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", key);
                if (string.IsNullOrEmpty(json))
                    return default;

                return JsonSerializer.Deserialize<T>(json);
            }
            catch (InvalidOperationException)
            {
                // JS interop not available during prerender or circuit reconnection
                return default;
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected
                return default;
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key);
            }
            catch (InvalidOperationException)
            {
                // JS interop not available during prerender or circuit reconnection — safe to ignore
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected — safe to ignore
            }
        }
    }
}
