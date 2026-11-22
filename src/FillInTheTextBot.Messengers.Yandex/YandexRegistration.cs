using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FillInTheTextBot.Messengers.Yandex;

public static class YandexRegistration
{
    public static void AddYandex(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => configuration.GetRequiredSection("Yandex").Get<YandexConfiguration>());
        services.AddTransient<IYandexService, YandexService>();
        services.ConfigureHttpJsonOptions(options =>
        {
            var json = options.SerializerOptions;
            json.PropertyNamingPolicy = null;
            json.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            json.AllowTrailingCommas = true;
            json.ReadCommentHandling = JsonCommentHandling.Skip;
            json.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            json.MaxDepth = 32;
        });
    }
}
