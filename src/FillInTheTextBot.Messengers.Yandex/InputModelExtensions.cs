using Yandex.Dialogs.Models.Input;

namespace FillInTheTextBot.Messengers.Yandex
{
    public static class InputModelExtensions
    {
        public static bool IsNavigator(this InputModel source)
        {
            var result = source?.Meta?.ClientId?.Contains("yandexnavi") == true;

            return result;
        }

        public static bool IsCanShowAdvertising(this InputModel source)
        {
            var result = source?.Meta?.Interfaces?.Screen != null && 
                (source?.Meta?.ClientId?.Contains("searchplugin") == true || source?.Meta?.ClientId?.Contains("browser") == true) &&
                source?.Meta?.ClientId?.Contains("android") == true;

            return result;
        }
    }
}
