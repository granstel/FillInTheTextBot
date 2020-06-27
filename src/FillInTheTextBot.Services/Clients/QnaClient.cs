﻿using System;
using System.Threading.Tasks;
using FillInTheTextBot.Models.Qna;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Extensions;
using FillInTheTextBot.Services.Serialization;
using NLog;
using RestSharp;

namespace FillInTheTextBot.Services.Clients
{
    public class QnaClient : IQnaClient
    {
        private readonly IRestClient _webClient;
        private readonly QnaConfiguration _configuration;
        private readonly CustomJsonSerializer _serializer;

        private readonly Logger _log = LogManager.GetLogger(nameof(QnaClient));

        public QnaClient(IRestClient webClient, QnaConfiguration configuration, CustomJsonSerializer serializer)
        {
            _webClient = webClient;
            _configuration = configuration;
            _serializer = serializer;
        }

        public async Task<Response> GetAnswerAsync(string knowledgeBase, string question)
        {
            var url = string.Format(_configuration.UrlFormat, knowledgeBase);

            _webClient.BaseUrl = new Uri(url);

            var restRequest = new RestRequest(Method.POST);
            restRequest.AddHeader("Ocp-Apim-Subscription-Key", _configuration.Token);

            var request = new Request(question);

            restRequest.JsonSerializer = _serializer;

            restRequest.AddJsonBody(request);

            var qnaResponse = default(Response);

            try
            {
                var response = await _webClient.ExecuteTaskAsync(restRequest);

                qnaResponse = response?.Content.Deserialize<Response>();
            }
            catch (Exception e)
            {
                _log.Error(e, "При получении ответа возникла ошибка");
            }

            return qnaResponse;
        }
    }
}
