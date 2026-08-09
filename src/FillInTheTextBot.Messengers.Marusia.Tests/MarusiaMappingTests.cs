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

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ToRequest_Empty_MarusiaSourceAndNoOfficialAppeal()
        {
            var result = new InputModel().ToRequest();

            Assert.That(result.Source, Is.EqualTo(InternalModels.Source.Marusia));
            Assert.That(result.Appeal, Is.EqualTo(InternalModels.Appeal.NoOfficial));
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


            Assert.That(result.ChatHash, Is.EqualTo(source.Session.SkillId));
            Assert.That(result.UserHash, Is.EqualTo(source.Session.UserId));
            Assert.That(result.SessionId, Is.EqualTo(source.Session.SessionId));
            Assert.That(result.NewSession, Is.True);
        }

        [Test]
        public void ToRequest_OriginalUtterance_Text()
        {
            var text = _fixture.Create<string>();

            var source = new InputModel { Request = new Request { OriginalUtterance = text } };


            var result = source.ToRequest();


            Assert.That(result.Text, Is.EqualTo(text));
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


            Assert.That(result.Language, Is.EqualTo(source.Meta.Locale));
            Assert.That(result.ClientId, Is.EqualTo(source.Meta.ClientId));
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


            Assert.That(result.HasScreen, Is.True);
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


            Assert.That(result.HasScreen, Is.False);
        }

        #endregion ToRequest

        #region ToResponse

        [Test]
        public void ToResponse_Null_Null()
        {
            InternalModels.Response source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToResponse();

            Assert.That(result, Is.Null);
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


            Assert.That(result.Text, Is.EqualTo("Текст"));
            Assert.That(result.Tts, Is.EqualTo("Озвучка"));
            Assert.That(result.EndSession, Is.True);
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


            Assert.That(result.Text, Is.EqualTo("Первая\nВторая"));
            Assert.That(result.Tts, Is.EqualTo("Первая\nВторая"));
        }

        [Test]
        public void ToResponse_NullButtons_NullButtons()
        {
            var source = new InternalModels.Response { Text = _fixture.Create<string>() };


            var result = source.ToResponse();


            Assert.That(result.Buttons, Is.Null);
        }

        #endregion ToResponse

        #region ToResponseButtons

        [Test]
        public void ToResponseButtons_Null_Null()
        {
            ICollection<InternalModels.Button> source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToResponseButtons();

            Assert.That(result, Is.Null);
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

            Assert.That(button.Title, Is.EqualTo("Быстрый"));
            Assert.That(button.Hide, Is.True);
            Assert.That(button.Url, Is.Null);
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


            Assert.That(result.Single().Url, Is.EqualTo(url));
            Assert.That(result.Single().Hide, Is.False);
        }

        [Test]
        public void ToResponseButtons_EmptyUrl_NullUrl()
        {
            var source = new List<InternalModels.Button>
            {
                new InternalModels.Button { Text = "Без ссылки", Url = string.Empty }
            };


            var result = source.ToResponseButtons();


            Assert.That(result.Single().Url, Is.Null);
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


            Assert.That(result.Response.Text, Is.EqualTo(source.Text));
            Assert.That(result.Session.UserId, Is.EqualTo(source.UserHash));
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


            Assert.That(result.Session.SessionId, Is.EqualTo(source.Session.SessionId));
            Assert.That(result.Version, Is.EqualTo(source.Version));
        }

        [Test]
        public void FillOutput_NullDestination_Null()
        {
            var source = new InputModel();


            var result = source.FillOutput(null);


            Assert.That(result, Is.Null);
        }

        [Test]
        public void FillOutput_NullSource_Null()
        {
            InputModel source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.FillOutput(new OutputModel());

            Assert.That(result, Is.Null);
        }

        #endregion ToOutput и FillOutput
    }
}
