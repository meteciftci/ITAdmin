namespace ITAdmin.Application.Abstractions.Notifications;

public interface INotificationTemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, object?> variables);
    IReadOnlyList<string> ExtractVariables(string template);
}
