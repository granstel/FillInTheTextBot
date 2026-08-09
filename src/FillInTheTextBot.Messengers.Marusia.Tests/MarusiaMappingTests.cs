using System;
using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using MailRu.Marusia.Models;
using MailRu.Marusia.Models.Input;
using NUnit.Framework;
using InternalModels = FillInTheTextBot.Models;

namespace FillInTheTextBot.Messengers.Marusia.Tests
{
    /// <summary>
    /// Характеризующие тесты: фиксируют текущее поведение <see cref="MarusiaMapping"/>
    /// перед обновлением фреймворка и пакетов.
    /// </summary>
    [TestFixture]
    public class MarusiaMappingTests
    {
        private Fixture _fixture;

        [SetUp]
        public void InitTest()
        {
            _fixture = new Fixture { OmitAutoProperties = true };
        }

        #region ToRequest

        [Test]
        public void ToRequest_Null_Null()
        {
            InputModel source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToRequest();

            Assert.Null(result);
        }

        [Test]
        public void ToRequest_Empty_MarusiaSourceAndNoOfficialAppeal()
        {
            var result = new InputModel().ToRequest();

            Assert.AreEqual(InternalModels.Source.Marusia, result.Source);
            Assert.AreEqual(InternalModels.Appeal.NoOfficial, result.Appeal);
        }

        [Test]
        public void ToRequest_Session_HashesAndSessionId()
        {
            var source = new InputModel
            {
                Session = new InputSession
                {
                    SkillId = _fixture.Create<string>(),
                    UserId = _fixture.Create<string>(),
                    SessionId = _fixture.Create<string>(),
                    New = true
                }
            };


            var result = source.ToRequest();


            Assert.AreEqual(source.Session.SkillId, result.ChatHash);
            Assert.AreEqual(source.Session.UserId, result.UserHash);
            Assert.AreEqual(source.Session.SessionId, result.SessionId);
            Assert.True(result.NewSession);
        }

        [Test]
        public void ToRequest_OriginalUtterance_Text()
        {
            var text = _fixture.Create<string>();

            var source = new InputModel { Request = new Request { OriginalUtterance = text } };


            var result = source.ToRequest();


            Assert.AreEqual(text, result.Text);
        }

        [Test]
        public void ToRequest_Meta_LanguageAndClientId()
        {
            var source = new InputModel
            {
                Meta = new MetaModel
                {
                    Locale = _fixture.Create<string>(),
                    ClientId = _fixture.Create<string>()
                }
            };


            var result = source.ToRequest();


            Assert.AreEqual(source.Meta.Locale, result.Language);
            Assert.AreEqual(source.Meta.ClientId, result.ClientId);
        }

        [Test]
        public void ToRequest_MobileApplication_HasScreen()
        {
            var source = new InputModel
            {
                Session = new InputSession
                {
                    Application = new Application { ApplicationType = ApplicationTypes.Mobile }
                }
            };


            var result = source.ToRequest();


            Assert.True(result.HasScreen);
        }

        [Test]
        public void ToRequest_OtherApplication_NoScreen()
        {
            var source = new InputModel
            {
                Session = new InputSession
                {
                    Application = new Application { ApplicationType = _fixture.Create<string>() }
                }
            };


            var result = source.ToRequest();


            Assert.False(result.HasScreen);
        }

        #endregion ToRequest

        #region ToResponse

        [Test]
        public void ToResponse_Null_Null()
        {
            InternalModels.Response source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToResponse();

            Assert.Null(result);
        }

        [Test]
        public void ToResponse_TextAndAlternativeText_TextAndTts()
        {
            var source = new InternalModels.Response
            {
                Text = "Текст",
                AlternativeText = "Озвучка",
                Finished = true
            };


            var result = source.ToResponse();


            Assert.AreEqual("Текст", result.Text);
            Assert.AreEqual("Озвучка", result.Tts);
            Assert.True(result.EndSession);
        }

        [Test]
        public void ToResponse_WindowsNewLine_ReplacedWithUnixNewLine()
        {
            var source = new InternalModels.Response
            {
                Text = $"Первая{Environment.NewLine}Вторая",
                AlternativeText = $"Первая{Environment.NewLine}Вторая"
            };


            var result = source.ToResponse();


            Assert.AreEqual("Первая\nВторая", result.Text);
            Assert.AreEqual("Первая\nВторая", result.Tts);
        }

        [Test]
        public void ToResponse_NullButtons_NullButtons()
        {
            var source = new InternalModels.Response { Text = _fixture.Create<string>() };


            var result = source.ToResponse();


            Assert.Null(result.Buttons);
        }

        #endregion ToResponse

        #region ToResponseButtons

        [Test]
        public void ToResponseButtons_Null_Null()
        {
            ICollection<InternalModels.Button> source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToResponseButtons();

            Assert.Null(result);
        }

        [Test]
        public void ToResponseButtons_QuickReply_Hidden()
        {
            var source = new List<InternalModels.Button>
            {
                new InternalModels.Button { Text = "Быстрый", IsQuickReply = true }
            };


            var result = source.ToResponseButtons();


            var button = result.Single();

            Assert.AreEqual("Быстрый", button.Title);
            Assert.True(button.Hide);
            Assert.Null(button.Url);
        }

        [Test]
        public void ToResponseButtons_WithUrl_UrlFilled()
        {
            var url = _fixture.Create<string>();

            var source = new List<InternalModels.Button>
            {
                new InternalModels.Button { Text = "Ссылка", Url = url }
            };


            var result = source.ToResponseButtons();


            Assert.AreEqual(url, result.Single().Url);
            Assert.False(result.Single().Hide);
        }

        [Test]
        public void ToResponseButtons_EmptyUrl_NullUrl()
        {
            var source = new List<InternalModels.Button>
            {
                new InternalModels.Button { Text = "Без ссылки", Url = string.Empty }
            };


            var result = source.ToResponseButtons();


            Assert.Null(result.Single().Url);
        }

        #endregion ToResponseButtons

        #region ToOutput и FillOutput

        [Test]
        public void ToOutput_Response_ResponseAndSessionFilled()
        {
            var source = new InternalModels.Response
            {
                Text = _fixture.Create<string>(),
                UserHash = _fixture.Create<string>()
            };


            var result = source.ToOutput();


            Assert.AreEqual(source.Text, result.Response.Text);
            Assert.AreEqual(source.UserHash, result.Session.UserId);
        }

        [Test]
        public void FillOutput_Session_CopiedFromInput()
        {
            var source = new InputModel
            {
                Session = new InputSession { SessionId = _fixture.Create<string>() },
                Version = _fixture.Create<string>()
            };

            var destination = new OutputModel();


            var result = source.FillOutput(destination);


            Assert.AreEqual(source.Session.SessionId, result.Session.SessionId);
            Assert.AreEqual(source.Version, result.Version);
        }

        [Test]
        public void FillOutput_NullDestination_Null()
        {
            var source = new InputModel();


            var result = source.FillOutput(null);


            Assert.Null(result);
        }

        [Test]
        public void FillOutput_NullSource_Null()
        {
            InputModel source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.FillOutput(new OutputModel());

            Assert.Null(result);
        }

        #endregion ToOutput и FillOutput
    }
}
