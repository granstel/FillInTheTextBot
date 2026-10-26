namespace FillInTheTextBot.Services.Rotation
{
    /// <param name="IsModified">false — содержимое не менялось с переданного ETag.</param>
    public sealed record RotationSourceResult<T>(bool IsModified, T Value, string ETag)
    {
        public static RotationSourceResult<T> NotModified { get; } = new(false, default, null);
    }
}
