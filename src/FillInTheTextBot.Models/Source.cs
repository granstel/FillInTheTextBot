namespace FillInTheTextBot.Models
{
    /// <summary>
    /// Источник запроса, мессенджер
    /// </summary>
    public enum Source
    {
        Default,
        Yandex,
        // Старые ключи ещё встречаются в payload Dialogflow.
        Sber,
        Marusia
    }
}
