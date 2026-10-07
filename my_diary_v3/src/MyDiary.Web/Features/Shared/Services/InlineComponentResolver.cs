using System;
using System.Collections.Generic;
// Add usings for any components you want to expose here:
using MyDiary.Web.Features.RedirectingPages; // e.g., AuditAwareness

namespace MyDiary.Web.Features.Shared.Services
{
    public sealed class InlineComponentResolver : IInlineComponentResolver
    {
        // A simple, centralized registry of key -> component Type
        // Thread-safe for reads (dictionary never mutated after ctor).
        private readonly IReadOnlyDictionary<string, Type> _registry;
        private const string Scheme = "component://";

        public InlineComponentResolver()
        {
            // Register all inline components here once.
            // Add more as your app grows (policies, circulars, etc.).
            _registry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["audit-awareness"] = typeof(AuditAwareness),
            };
        }

        public bool IsComponentRef(string? path)
            => !string.IsNullOrWhiteSpace(path) && path.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);

        public Type? ResolveFromPath(string? path)
        {
            if (!IsComponentRef(path)) return null;
            var key = path!.Substring(Scheme.Length);
            return ResolveFromKey(key);
        }

        public Type? ResolveFromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _registry.TryGetValue(key, out var type) ? type : null;
        }
    }
}