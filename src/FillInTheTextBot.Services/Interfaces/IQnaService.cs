using System.Threading.Tasks;

namespace FillInTheTextBot.Services
{
    public interface IQnaService
    {
        Task<string> GetAnswerAsync(string question);
    }
}