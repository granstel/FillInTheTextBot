using FillInTheTextBot.Services.Mapping;
using Google.Cloud.Dialogflow.V2;
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
        }
    }
}
