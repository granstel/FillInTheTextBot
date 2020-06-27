using AutoMapper;
using FillInTheTextBot.Services.Mapping;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.MappingProfiles
{
    [TestFixture]
    public class InternalProfileTests
    {
        private IMapper _target;

        [SetUp]
        public void InitTest()
        {
            _target = new Mapper(new MapperConfiguration(c => c.AddProfile<InternalProfile>()));
        }

        [Test]
        public void ValidateConfiguration_Success()
        {
            _target.ConfigurationProvider.AssertConfigurationIsValid();
        }
    }
}
