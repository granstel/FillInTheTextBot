﻿using System.Threading.Tasks;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Services
{
    public interface IDialogflowService
    {
        Task<Dialog> GetResponseAsync(Request request);

        Task<Dialog> GetResponseAsync(string text, string sessionId, string requiredContext = null);

        Task DeleteAllContexts(Request request);

        Task SetContext(string sessionId, string contextName, int lifeSpan = 1);
    }
}