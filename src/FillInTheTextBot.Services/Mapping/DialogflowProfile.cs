using AutoMapper;
using FillInTheTextBot.Models;
using Google.Cloud.Dialogflow.V2;

namespace FillInTheTextBot.Services.Mapping
{
    public class DialogflowProfile : Profile
    {
        public DialogflowProfile()
        {
            CreateMap<QueryResult, Dialog>()
                .ConvertUsing((s, d) => s.ToDialog(d));
        }
    }
}
