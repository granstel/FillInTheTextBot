namespace FillInTheTextBot.Services;

public interface IScopeBindingStorage
{
    bool TryGet(string invocationKey, out string scopeKey);
    void Add(string invocationKey, string scopeId);
}