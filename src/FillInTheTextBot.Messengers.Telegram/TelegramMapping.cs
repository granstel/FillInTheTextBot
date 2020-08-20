using AutoMapper;
using FillInTheTextBot.Models;
using Telegram.Bot.Types;

namespace FillInTheTextBot.Messengers.Telegram
{
    public class TelegramMapping : Profile
    {
        public TelegramMapping()
        {

            CreateMap<Update, Request>()
            .ForMember(d => d.ChatHash, m => m.ResolveUsing(s => (s.Message?.Chat?.Id).GetValueOrDefault()))
            .ForMember(d => d.UserHash, m => m.ResolveUsing(s => (s.Message?.From?.Id).GetValueOrDefault()))
            .ForMember(d => d.SessionId, m => m.ResolveUsing(s => (s.Message?.From?.Id).GetValueOrDefault()))
            .ForMember(d => d.Text, m => m.ResolveUsing(s => s.Message?.Text))
            .ForMember(d => d.Source, m => m.UseValue(Source.Telegram))
            .ForMember(d => d.Language, m => m.Ignore())
            .ForMember(d => d.NewSession, m => m.Ignore())
            .ForMember(d => d.RequiredContext, m => m.Ignore())
            .ForMember(d => d.ClearContexts, m => m.Ignore())
            .ForMember(d => d.IsOldUser, m => m.Ignore())
            .ForMember(d => d.HasScreen, m => m.Ignore());
        }
    }
}
