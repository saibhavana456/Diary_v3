using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using System.Security.Cryptography;

namespace MyDiary.Web.Core.Services.Storage
{
    /// <summary>
    /// Encrypted session storage using ASP.NET Core Data Protection — values are not readable in browser DevTools.
    /// Gracefully handles prerender and circuit reconnection scenarios.
    /// </summary>
    public class ProtectedSessionStorageService : ISessionStorageService
    {
        private readonly ProtectedSessionStorage _protectedSessionStorage;

        public ProtectedSessionStorageService(ProtectedSessionStorage protectedSessionStorage)
        {
            _protectedSessionStorage = protectedSessionStorage;
        }

        public async Task SetAsync<T>(string key, T value)
        {
            try
            {
                await _protectedSessionStorage.SetAsync(key, value!);
            }
            catch (InvalidOperationException) { }
            catch (JSDisconnectedException) { }
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var result = await _protectedSessionStorage.GetAsync<T>(key);
                return result.Success ? result.Value : default;
            }
            catch (InvalidOperationException) { return default; }
            catch (JSDisconnectedException) { return default; }
            catch (CryptographicException) { return default; }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _protectedSessionStorage.DeleteAsync(key);
            }
            catch (InvalidOperationException) { }
            catch (JSDisconnectedException) { }
        }
    }
}
