namespace FillInTheTextBot.Services.Configuration;

public abstract class MessengerConfiguration : Configuration
{
    private string _incomingToken;

    private string _token;

    public virtual string IncomingToken
    {
        get => _incomingToken;
        set => _incomingToken = ExpandVariable(value);
    }

    public virtual string Token
    {
        get => _token;
        set => _token = ExpandVariable(value);
    }
}