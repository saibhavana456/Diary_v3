using Scriban;

namespace RequestPortal.Core.Services;

public sealed class ScribanRenderer : ITemplateRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, object?> model)
    {
        var t = Template.Parse(template);
        return t.Render(model);
    }
}
