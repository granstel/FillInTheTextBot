using System.Threading;
using System.Threading.Tasks;

namespace FillInTheTextBot.Services.Rotation
{
    public interface IRotationSource
    {
        Task<RotationSourceResult<string[]>> GetStoryFilesAsync(string etag, CancellationToken cancellationToken);

        /// <summary>Value = null, если файла правил нет.</summary>
        Task<RotationSourceResult<string>> GetRulesAsync(string etag, CancellationToken cancellationToken);
    }
}
