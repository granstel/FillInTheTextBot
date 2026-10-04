using System;

namespace FillInTheTextBot.Services.Rotation
{
    /// <param name="Error">null — проверка прошла успешно.</param>
    public sealed record RotationCheck(DateTimeOffset CheckedAt, string Error);
}
