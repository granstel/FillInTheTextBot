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

            Assert.IsNotEmpty(dialog.Parameters, "Parameters should not be empty");
            Assert.True(dialog.Parameters.ContainsKey(key));
            Assert.True(dialog.Parameters.Values.Contains(value));
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
        public void ToDialog_Payload_CorrectValue()
        {
            var buttonsStruct = new Struct();
            var buttonText = _fixture.Create<string>();
            buttonsStruct.Fields.Add("Text", new Value
            {
                StringValue = buttonText
            });

            var listValue = new ListValue();
            listValue.Values.Add(new Value
            {
                StructValue = buttonsStruct
            });

            var message = new Intent.Types.Message
            {
                Payload = new Struct()
            };
            message.Payload.Fields.Add("Buttons", new Value
            {
                ListValue = listValue
            });

            var source = new QueryResult();
            source.FulfillmentMessages.Add(message);

            var dialog = source.ToDialog();

            Assert.IsNotEmpty(dialog.Payload.Buttons, "Payload should not be empty");
            Assert.AreEqual(buttonText, dialog.Payload.Buttons.Select(b => b.Text).FirstOrDefault(), "Text shold be equal expected buttonText");
        }
    }
}
