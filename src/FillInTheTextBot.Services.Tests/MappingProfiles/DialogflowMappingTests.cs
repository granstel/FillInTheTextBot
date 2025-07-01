using AutoFixture;
using FillInTheTextBot.Services.Mapping;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using System.Linq;

namespace FillInTheTextBot.Services.Tests.MappingProfiles
{
    public class DialogflowMappingTests
    {
        private Fixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new Fixture { OmitAutoProperties = true };
        }

        [Test]
        public void ToDialog_NullSource_DefaultValues()
        {
            QueryResult source = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            var dialog = source.ToDialog();

            Assert.That(dialog.Parameters, Is.Empty);
            Assert.That(dialog.EndConversation, Is.False);
            Assert.That(dialog.ParametersIncomplete, Is.True);
            Assert.That(dialog.Response, Is.Null);
            Assert.That(dialog.Action, Is.Null);
            Assert.That(dialog.Buttons, Is.Empty);
            Assert.That(dialog.Payload, Is.Null);
        }

        [Test]
        public void ToDialog_NullParameters_EmptyDictionary()
        {
            var source = new QueryResult();

            var dialog = source.ToDialog();

            Assert.IsEmpty(dialog.Parameters);
        }

        [Test]
        public void ToDialog_ParametersWithStringValue_ContainsKeyValuePair()
        {
            var key = _fixture.Create<string>();
            var value = _fixture.Create<string>();

            var source = new QueryResult
            {
                Parameters = new Struct()
            };
            source.Parameters.Fields.Add(key, new Value
            {
                StringValue = value
            });

            var dialog = source.ToDialog();

            Assert.That(dialog.Parameters, Is.Not.Empty, "Parameters should not be empty");
            Assert.That(dialog.Parameters.ContainsKey(key), Is.True);
            Assert.That(dialog.Parameters.Values, Does.Contain(value));
        }

        [Test]
        public void ToDialog_ParametersWithStructValue_ContainsKeyValuePairs()
        {
            var key = _fixture.Create<string>();

            var structValue = new Struct();
            var stringValue = _fixture.Create<string>();
            var anotherStringValue = _fixture.Create<string>();

            structValue.Fields.Add(_fixture.Create<string>(), new Value
            {
                StringValue = stringValue
            });
            structValue.Fields.Add(_fixture.Create<string>(), new Value
            {
                StringValue = anotherStringValue
            });

            var source = new QueryResult
            {
                Parameters = new Struct()
            };
            source.Parameters.Fields.Add(key, new Value
            {
                StructValue = structValue
            });

            var dialog = source.ToDialog();

            Assert.IsNotEmpty(dialog.Parameters, "Parameters should not be empty");
            Assert.True(dialog.Parameters.ContainsKey(key));
            Assert.True(dialog.Parameters.Values.Contains(string.Join("/", stringValue, anotherStringValue)));
        }

        [Test]
        public void ToDialog_Response_EqualsFulfillmentText()
        {
            var fulfillmentText = _fixture.Create<string>();

            var source = new QueryResult
            {
                FulfillmentText = fulfillmentText
            };

            var dialog = source.ToDialog();

            Assert.AreEqual(fulfillmentText, dialog.Response);
        }

        [Test]
        public void ToDialog_QuickReplies_ConvertedToButtons()
        {
            var quickReplyText = _fixture.Create<string>();

            var message = new Intent.Types.Message
            {
                QuickReplies = new Intent.Types.Message.Types.QuickReplies()
            };
            message.QuickReplies.QuickReplies_.Add(quickReplyText);

            var source = new QueryResult();
            source.FulfillmentMessages.Add(message);

            var dialog = source.ToDialog();

            var result = dialog?.Buttons.FirstOrDefault();

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(dialog.Buttons, "Buttons should not be empty");
            Assert.AreEqual(quickReplyText, result.Text);
            Assert.True(result.IsQuickReply);
        }

        [Test]
        public void ToDialog_Cards_ConvertedToButtons()
        {
            var buttonText = _fixture.Create<string>();

            var message = new Intent.Types.Message
            {
                Card = new Intent.Types.Message.Types.Card()
            };
            message.Card.Buttons.Add(new Intent.Types.Message.Types.Card.Types.Button
            {
                Text = buttonText
            });

            var source = new QueryResult();
            source.FulfillmentMessages.Add(message);

            var dialog = source.ToDialog();

            var result = dialog.Buttons.FirstOrDefault();

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(dialog.Buttons, "Buttons should not be empty");
            Assert.AreEqual(buttonText, result.Text);
            Assert.False(result.IsQuickReply);
        }

        [Test]
        public void ToDialog_QuickRepliesAndCards_AllConvertedToButtons()
        {
            var buttonText = _fixture.Create<string>();
            var cardMessage = new Intent.Types.Message
            {
                Card = new Intent.Types.Message.Types.Card()
            };
            cardMessage.Card.Buttons.Add(new Intent.Types.Message.Types.Card.Types.Button
            {
                Text = buttonText
            });

            var quickReplyText = _fixture.Create<string>();
            var quickReplyMessage = new Intent.Types.Message
            {
                QuickReplies = new Intent.Types.Message.Types.QuickReplies()
            };
            quickReplyMessage.QuickReplies.QuickReplies_.Add(quickReplyText);

            var source = new QueryResult();
            source.FulfillmentMessages.Add(quickReplyMessage);
            source.FulfillmentMessages.Add(cardMessage);

            var dialog = source.ToDialog();

            Assert.That(dialog.Buttons, Is.Not.Empty, "Buttons should not be empty");

            var expectedValues = new[] { quickReplyText, buttonText };
            Assert.That(dialog.Buttons.Select(b => b.Text).All(t => expectedValues.Contains(t)), Is.True);
        }

        [Test]
        public void ToDialog_ParametersIncomplete_NotEqualsAllRequiredParamsPresent()
        {
            var source = new QueryResult
            {
                AllRequiredParamsPresent = _fixture.Create<bool>()
            };

            var dialog = source.ToDialog();

            Assert.AreEqual(!source.AllRequiredParamsPresent, dialog.ParametersIncomplete);
        }

        [Test]
        public void ToDialog_Action_Equals()
        {
            var source = new QueryResult
            {
                Action = _fixture.Create<string>()
            };

            var dialog = source.ToDialog();

            Assert.AreEqual(source.Action, dialog.Action);
        }

        [Test]
        public void ToDialog_ActionIsEndConversation_EndConversationIsTrue()
        {
            var source = new QueryResult
            {
                Action = "endConversation"
            };

            var dialog = source.ToDialog();

            Assert.True(dialog.EndConversation);
        }
    }
}
