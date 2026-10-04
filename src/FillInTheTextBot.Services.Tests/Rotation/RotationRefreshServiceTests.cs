using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Rotation;
using GranSteL.Helpers.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    [TestFixture]
    public class RotationRefreshServiceTests
    {
        private static readonly string[] StoryFiles = ["1-A-text.json", "2-B-text1.json", "2-B-text2.json"];

        private Mock<IRotationSource> _source;
        private Mock<IRedisCacheService> _cache;
        private RotationStore _store;

        private RotationRefreshService _target;

        [SetUp]
        public void InitTest()
        {
            _source = new Mock<IRotationSource>();
            _cache = new Mock<IRedisCacheService>();
            _store = new RotationStore();

            _target = new RotationRefreshService(_source.Object, _cache.Object, _store, new RotationConfiguration(),
                NullLogger<RotationRefreshService>.Instance);
        }

        [TearDown]
        public void CleanUp()
        {
            _target.Dispose();
        }

        private void SetupSource(RotationSourceResult<string[]> stories, RotationSourceResult<string> rules)
        {
            _source.Setup(s => s.GetStoryFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(stories);
            _source.Setup(s => s.GetRulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(rules);
        }

        [Test]
        public async Task RefreshAsync_ValidSources_AppliesAndCaches()
        {
            SetupSource(new(true, StoryFiles, "s1"), new(true, """{ "firstCycle": ["text-2-1"] }""", "r1"));


            await _target.RefreshAsync(CancellationToken.None);


            Assert.That(_store.Snapshot.Catalog.Numbers, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(_store.Snapshot.Rules.FirstCycle, Is.EqualTo(new[] { "text-2-1" }));
            Assert.That(_store.LastCheck.Error, Is.Null);
            _cache.Verify(c => c.AddAsync("rotation:sources",
                It.Is<RotationSources>(s => s.StoriesETag == "s1" && s.RulesETag == "r1"), null), Times.Once);
        }

        [Test]
        public async Task RefreshAsync_NoRulesFile_EmptyRules()
        {
            SetupSource(new(true, StoryFiles, "s1"), new(true, null, null));


            await _target.RefreshAsync(CancellationToken.None);


            Assert.That(_store.Snapshot.Rules.FirstCycle, Is.Empty);
        }

        [Test]
        public async Task RefreshAsync_InvalidRules_KeepsPrevious()
        {
            SetupSource(new(true, StoryFiles, "s1"), new(true, "{}", "r1"));
            await _target.RefreshAsync(CancellationToken.None);
            var previous = _store.Snapshot;

            SetupSource(RotationSourceResult<string[]>.NotModified, new(true, """{ "hidden": ["text-3"] }""", "r2"));


            await _target.RefreshAsync(CancellationToken.None);


            Assert.That(_store.Snapshot, Is.SameAs(previous));
            Assert.That(_store.LastCheck.Error, Does.Contain("text-3"));
            _cache.Verify(c => c.AddAsync(It.IsAny<string>(), It.IsAny<object>(), null), Times.Once);
        }

        [Test]
        public async Task RefreshAsync_NotModified_ConditionalRequestsKeepSnapshot()
        {
            SetupSource(new(true, StoryFiles, "s1"), new(true, "{}", "r1"));
            await _target.RefreshAsync(CancellationToken.None);
            var previous = _store.Snapshot;

            SetupSource(RotationSourceResult<string[]>.NotModified, RotationSourceResult<string>.NotModified);


            await _target.RefreshAsync(CancellationToken.None);


            Assert.That(_store.Snapshot, Is.SameAs(previous));
            _source.Verify(s => s.GetStoryFilesAsync("s1", It.IsAny<CancellationToken>()));
            _source.Verify(s => s.GetRulesAsync("r1", It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task RefreshAsync_OnlyRulesModified_KeepsStories()
        {
            SetupSource(new(true, StoryFiles, "s1"), new(true, "{}", "r1"));
            await _target.RefreshAsync(CancellationToken.None);

            SetupSource(RotationSourceResult<string[]>.NotModified, new(true, """{ "hidden": ["text-1"] }""", "r2"));


            await _target.RefreshAsync(CancellationToken.None);


            Assert.That(_store.Snapshot.Catalog.Numbers, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(_store.Snapshot.Rules.Hidden, Is.EqualTo(new[] { "text-1" }));
            Assert.That(_store.Snapshot.Sources.StoriesETag, Is.EqualTo("s1"));
        }

        [Test]
        public async Task RefreshAsync_SourceFails_ReportsErrorKeepsSnapshot()
        {
            SetupSource(new(true, StoryFiles, "s1"), new(true, "{}", "r1"));
            await _target.RefreshAsync(CancellationToken.None);
            var previous = _store.Snapshot;

            _source.Setup(s => s.GetStoryFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("недоступен"));


            await _target.RefreshAsync(CancellationToken.None);


            Assert.That(_store.Snapshot, Is.SameAs(previous));
            Assert.That(_store.LastCheck.Error, Is.EqualTo("недоступен"));
        }

        [Test]
        public void LoadFromCache_CachedSources_Applies()
        {
            var cached = new RotationSources { StoryFiles = StoryFiles, StoriesETag = "s1" };
            _cache.Setup(c => c.TryGet("rotation:sources", out cached, It.IsAny<bool>())).Returns(true);


            _target.LoadFromCache();


            Assert.That(_store.Snapshot.Sources.StoriesETag, Is.EqualTo("s1"));
        }

        [Test]
        public void LoadFromCache_CacheFails_NoSnapshot()
        {
            var cached = default(RotationSources);
            _cache.Setup(c => c.TryGet("rotation:sources", out cached, It.IsAny<bool>())).Throws(new TimeoutException());


            _target.LoadFromCache();


            Assert.That(_store.Snapshot, Is.Null);
        }
    }
}
