using FillInTheTextBot.Services.Mapping;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.MappingProfiles
{
    internal class DialogflowMappingTests
    {
        [Test]
        public void ToDialog_NullParameters_EmptyDictionary()
        {
            var source = new QueryResult();

            var dialog = source.ToDialog();

            Assert.IsEmpty(dialog.Parameters);
        }

        [Test]
        public void ToDialog_Parameters_EmptyDictionary()
        {
            var source = new QueryResult();
            source.Parameters = new Struct();
            source.Parameters.Fields.Add<string, StringValue>("test", new StringValue());


            var dialog = source.ToDialog();

            Assert.IsEmpty(dialog.Parameters);
        }
    }
}
