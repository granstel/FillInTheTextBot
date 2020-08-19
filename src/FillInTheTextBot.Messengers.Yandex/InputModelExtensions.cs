using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Services.Extensions
{
    public static class InputModelExtensions
    {
        public static bool IsNavigator(this InputModel source)
        {
            var result = source?.Meta?.ClientId.Contains("yandexnavi") == true;

            return result;
        }

        public static bool IsCanShowAdvertising(this InputModel source)
        {
            var result = source?.Meta?.Interfaces?.Screen != null && 
                (source?.Meta?.ClientId?.Contains("searchplugin") == true || source?.Meta?.ClientId?.Contains("browser") == true);

            return result;
        }
    }
}
