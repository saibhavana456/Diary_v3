using System;

namespace MyDiary.Web.Features.Shared.Services
{
    public interface IInlineComponentResolver
    {
        /// <summary>
        /// Returns true when the path is a component reference like "component://{key}".
        /// </summary>
        bool IsComponentRef(string? path);

        /// <summary>
        /// Resolves a component Type from a path like "component://{key}".
        /// Returns null if not a component path or the key is unknown.
        /// </summary>
        Type? ResolveFromPath(string? path);

        /// <summary>
        /// Optional: resolve directly from a key (without "component://" prefix).
        /// </summary>
        Type? ResolveFromKey(string key);
    }
}