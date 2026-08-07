using System;
using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using MailRu.Marusia.Models;
using MailRu.Marusia.Models.Input;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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

            ClassicAssert.Null(result);
        }

        [Test]
        public void ToRequest_Empty_MarusiaSourceAndNoOfficialAppeal()
        {
            var result = new InputModel().ToRequest();

            ClassicAssert.AreEqual(InternalModels.Source.Marusia, result.Source);
            ClassicAssert.AreEqual(InternalModels.Appeal.NoOfficial, result.Appeal);
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


            ClassicAssert.AreEqual(source.Session.SkillId, result.ChatHash);
            ClassicAssert.AreEqual(source.Session.UserId, result.UserHash);
            ClassicAssert.AreEqual(source.Session.SessionId, result.SessionId);
            ClassicAssert.True(result.NewSession);
        }

        [Test]
        public void ToRequest_OriginalUtterance_Text()
        {
            var text = _fixture.Create<string>();

            var source = new InputModel { Request = new Request { OriginalUtterance = text } };


            var result = source.ToRequest();


            ClassicAssert.AreEqual(text, result.Text);
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


            ClassicAssert.AreEqual(source.Meta.Locale, result.Language);
            ClassicAssert.AreEqual(source.Meta.ClientId, result.ClientId);
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


            ClassicAssert.True(result.HasScreen);
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


            ClassicAssert.False(result.HasScreen);
        }

        #endregion ToRequest

        #region ToResponse

        [Test]
        public void ToResponse_Null_Null()
        {
            InternalModels.Response source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToResponse();

            ClassicAssert.Null(result);
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


            ClassicAssert.AreEqual("Текст", result.Text);
            ClassicAssert.AreEqual("Озвучка", result.Tts);
            ClassicAssert.True(result.EndSession);
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


            ClassicAssert.AreEqual("Первая\nВторая", result.Text);
            ClassicAssert.AreEqual("Первая\nВторая", result.Tts);
        }

        [Test]
        public void ToResponse_NullButtons_NullButtons()
        {
            var source = new InternalModels.Response { Text = _fixture.Create<string>() };


            var result = source.ToResponse();


            ClassicAssert.Null(result.Buttons);
        }

        #endregion ToResponse

        #region ToResponseButtons

        [Test]
        public void ToResponseButtons_Null_Null()
        {
            ICollection<InternalModels.Button> source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.ToResponseButtons();

            ClassicAssert.Null(result);
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

            ClassicAssert.AreEqual("Быстрый", button.Title);
            ClassicAssert.True(button.Hide);
            ClassicAssert.Null(button.Url);
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


            ClassicAssert.AreEqual(url, result.Single().Url);
            ClassicAssert.False(result.Single().Hide);
        }

        [Test]
        public void ToResponseButtons_EmptyUrl_NullUrl()
        {
            var source = new List<InternalModels.Button>
            {
                new InternalModels.Button { Text = "Без ссылки", Url = string.Empty }
            };


            var result = source.ToResponseButtons();


            ClassicAssert.Null(result.Single().Url);
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


            ClassicAssert.AreEqual(source.Text, result.Response.Text);
            ClassicAssert.AreEqual(source.UserHash, result.Session.UserId);
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


            ClassicAssert.AreEqual(source.Session.SessionId, result.Session.SessionId);
            ClassicAssert.AreEqual(source.Version, result.Version);
        }

        [Test]
        public void FillOutput_NullDestination_Null()
        {
            var source = new InputModel();


            var result = source.FillOutput(null);


            ClassicAssert.Null(result);
        }

        [Test]
        public void FillOutput_NullSource_Null()
        {
            InputModel source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var result = source.FillOutput(new OutputModel());

            ClassicAssert.Null(result);
        }

        #endregion ToOutput и FillOutput
    }
}
