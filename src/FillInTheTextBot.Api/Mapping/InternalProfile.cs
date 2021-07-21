﻿using AutoMapper;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Api.Mapping
{
    public class InternalProfile : Profile
    {
        public InternalProfile()
        {
            CreateMap<Request, Response>()
                .ForMember(d => d.ChatHash, m => m.MapFrom(s => s.ChatHash))
                .ForMember(d => d.UserHash, m => m.MapFrom(s => s.UserHash))
                .ForMember(d => d.Text, m => m.Ignore())
                .ForMember(d => d.AlternativeText, m => m.Ignore())
                .ForMember(d => d.Finished, m => m.Ignore())
                .ForMember(d => d.Buttons, m => m.Ignore())
                .ForMember(d => d.ScopeKey, m => m.Ignore())
                .ForMember(d => d.Emotions, m => m.Ignore());
        }
    }
}
