using System.Collections.Generic;
using System.Threading.Tasks;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Interfaces;

namespace FillInTheTextBot.Services;

/// <summary>
/// Интерфейс для Dialogflow сервиса (совместимость)
/// </summary>
public interface IDialogflowService : INluService
{
}