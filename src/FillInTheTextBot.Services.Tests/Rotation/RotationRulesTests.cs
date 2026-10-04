using System.Collections.Generic;
using FillInTheTextBot.Services.Rotation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    [TestFixture]
    public class RotationRulesTests
    {
        private StoryCatalog _catalog;

        [SetUp]
        public void InitTest()
        {
            _catalog = new StoryCatalog(new Dictionary<int, int> { [1] = 0, [2] = 0, [33] = 0, [34] = 2, [40] = 0 });
        }

        [Test]
        public void Parse_ValidJson_ReadsLists()
        {
            const string json = """
                                {
                                  "firstCycle": ["text-33", "text-34-1"],
                                  "chains": [["text-33", "text-34-1"]],
                                  "hidden": ["text-2"]
                                }
                                """;


            var rules = RotationRules.Parse(json);


            Assert.That(rules.FirstCycle, Is.EqualTo(new[] { "text-33", "text-34-1" }));
            Assert.That(rules.Chains, Has.Length.EqualTo(1));
            Assert.That(rules.Hidden, Is.EqualTo(new[] { "text-2" }));
        }

        [Test]
        public void Parse_UnknownField_Throws()
        {
            Assert.Throws<JsonSerializationException>(() => RotationRules.Parse("""{ "hiden": [] }"""));
        }

        [Test]
        public void Parse_MissingLists_EmptyLists()
        {
            var rules = RotationRules.Parse("{}");


            Assert.That(rules.Validate(_catalog), Is.Empty);
        }

        [Test]
        public void Validate_ValidRules_NoErrors()
        {
            var rules = new RotationRules
            {
                FirstCycle = ["text-33", "text-34-1"],
                Chains = [["text-33", "text-34-1"]],
                Hidden = ["text-2"]
            };


            var errors = rules.Validate(_catalog);


            Assert.That(errors, Is.Empty);
        }

        [TestCase("text-99")]
        [TestCase("text-34")]
        [TestCase("text-34-2")]
        [TestCase("texts-over")]
        public void Validate_NotEntryEvent_Error(string eventName)
        {
            var rules = new RotationRules { FirstCycle = [eventName] };


            var errors = rules.Validate(_catalog);


            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain(eventName));
        }

        [Test]
        public void Validate_Duplicate_Error()
        {
            var rules = new RotationRules { Hidden = ["text-1", "text-1"] };


            var errors = rules.Validate(_catalog);


            Assert.That(errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void Validate_StoryInTwoChains_Error()
        {
            var rules = new RotationRules { Chains = [["text-1", "text-2"], ["text-2", "text-33"]] };


            var errors = rules.Validate(_catalog);


            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain("text-2"));
        }

        [Test]
        public void Validate_ShortChain_Error()
        {
            var rules = new RotationRules { Chains = [["text-1"]] };


            var errors = rules.Validate(_catalog);


            Assert.That(errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void Validate_NullList_Error()
        {
            var rules = RotationRules.Parse("""{ "hidden": null }""");


            var errors = rules.Validate(_catalog);


            Assert.That(errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void Parse_Campaign_ReadsFields()
        {
            const string json = """
                                {
                                  "campaigns": [
                                    { "id": "new-year", "from": "12-20", "to": "01-10", "shuffle": true, "hideOutside": true,
                                      "texts": ["text-1", "text-2"] }
                                  ]
                                }
                                """;


            var rules = RotationRules.Parse(json);


            var campaign = rules.Campaigns[0];

            Assert.That(campaign.Id, Is.EqualTo("new-year"));
            Assert.That(campaign.From, Is.EqualTo("12-20"));
            Assert.That(campaign.To, Is.EqualTo("01-10"));
            Assert.That(campaign.Shuffle, Is.True);
            Assert.That(campaign.HideOutside, Is.True);
            Assert.That(campaign.Texts, Is.EqualTo(new[] { "text-1", "text-2" }));
            Assert.That(rules.Validate(_catalog), Is.Empty);
        }

        [TestCase("", "03-01", "03-14", "text-1")]
        [TestCase("promo", "03-01", "2027-03-14", "text-1")]
        [TestCase("promo", "2027-03-14", "2027-03-01", "text-1")]
        [TestCase("promo", "3-1", "03-14", "text-1")]
        [TestCase("promo", "03-01", "03-14", "text-99")]
        [TestCase("promo", "03-01", "03-14", null)]
        public void Validate_InvalidCampaign_Error(string id, string from, string to, string eventName)
        {
            var rules = new RotationRules
            {
                Campaigns = [new RotationCampaign { Id = id, From = from, To = to, Texts = eventName is null ? [] : [eventName] }]
            };


            var errors = rules.Validate(_catalog);


            Assert.That(errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void Validate_DuplicateCampaignId_Error()
        {
            var campaign = new RotationCampaign { Id = "promo", From = "03-01", To = "03-14", Texts = ["text-1"] };
            var rules = new RotationRules { Campaigns = [campaign, campaign] };


            var errors = rules.Validate(_catalog);


            Assert.That(errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void GetFirstCycle_PartialList_ListedFirstThenRestByNumberWithoutHidden()
        {
            var rules = new RotationRules
            {
                FirstCycle = ["text-40", "text-33", "text-34-1"],
                Hidden = ["text-2"]
            };


            var firstCycle = rules.GetFirstCycle(_catalog);


            Assert.That(firstCycle, Is.EqualTo(new[] { 40, 33, 34, 1 }));
        }
    }
}
