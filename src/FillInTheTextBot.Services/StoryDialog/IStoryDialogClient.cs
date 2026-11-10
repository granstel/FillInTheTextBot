using System.Threading;
using System.Threading.Tasks;
using StoryDialog.Contracts.V1;

namespace FillInTheTextBot.Services.StoryDialog;

public interface IStoryDialogClient
{
    Task<ListStoriesResponse> ListStoriesAsync(CancellationToken cancellationToken);

    Task<Reply> TurnAsync(TurnRequest request, CancellationToken cancellationToken);

    Task<Reply> GetConversationAsync(string conversationId, CancellationToken cancellationToken);
}
