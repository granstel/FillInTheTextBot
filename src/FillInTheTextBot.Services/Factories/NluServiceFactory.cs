using System;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Services.Factories;

public interface INluServiceFactory
{
    INluService CreateService();
}

public class NluServiceFactory : INluServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NluConfiguration _nluConfiguration;

    public NluServiceFactory(IServiceProvider serviceProvider, NluConfiguration nluConfiguration)
    {
        _serviceProvider = serviceProvider;
        _nluConfiguration = nluConfiguration ?? new NluConfiguration();
    }

    public INluService CreateService()
    {
        return _nluConfiguration.Provider switch
        {
            NluProvider.Dialogflow => _serviceProvider.GetRequiredService<DialogflowService>(),
            NluProvider.Rasa => _serviceProvider.GetRequiredService<RasaService>(),
            _ => throw new ArgumentOutOfRangeException(nameof(_nluConfiguration.Provider), 
                $"Unsupported NLU provider: {_nluConfiguration.Provider}")
        };
    }
}