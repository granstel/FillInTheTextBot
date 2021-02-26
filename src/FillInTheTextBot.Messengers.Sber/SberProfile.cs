using AutoMapper;
using Sber.SmartApp.Models;

namespace FillInTheTextBot.Messengers.Sber
{
    /// <summary>
    /// Probably, registered at MappingModule of "Services" project
    /// </summary>
    public class SberProfile : Profile
    {
        public SberProfile()
        {
            CreateMap<Request, Models.Request>()
                .ForMember(d => d.ChatHash, m => m.ResolveUsing(s => s?.Payload?.AppInfo?.ProjectId.ToString()))
                .ForMember(d => d.UserHash, m => m.ResolveUsing(s => s?.Uuid?.Sub ?? s?.Uuid?.UserId))
                .ForMember(d => d.Text, m => m.Ignore())
                .ForMember(d => d.SessionId, m => m.Ignore())
                .ForMember(d => d.NewSession, m => m.Ignore())
                .ForMember(d => d.Language, m => m.Ignore())
                .ForMember(d => d.HasScreen, m => m.Ignore())
                .ForMember(d => d.ClientId, m => m.Ignore())
                .ForMember(d => d.Source, m => m.UseValue(Models.Source.Sber))
                .ForMember(d => d.RequiredContext, m => m.Ignore())
                .ForMember(d => d.ClearContexts, m => m.Ignore())
                .ForMember(d => d.IsOldUser, m => m.Ignore())
                .ForMember(d => d.NextTextIndex, m => m.Ignore())
                .ForMember(d => d.ScopeKey, m => m.Ignore());

            CreateMap<Models.Response, Response>();

            CreateMap<Request, Response>();

            CreateMap<RequestPayload, ResponsePayload>();
        }
    }
}
