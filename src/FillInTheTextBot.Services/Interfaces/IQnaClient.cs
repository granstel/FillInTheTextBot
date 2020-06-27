using System.Threading.Tasks;
using FillInTheTextBot.Models.Qna;

namespace FillInTheTextBot.Services
{
    public interface IQnaClient
    {
        Task<Response> GetAnswerAsync(string knowledgeBase, string question);
    }
}