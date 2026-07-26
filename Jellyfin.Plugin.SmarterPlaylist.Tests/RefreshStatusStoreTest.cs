using System;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class RefreshStatusStoreTest
    {
        private static RefreshStatus Status(string fileName, RefreshOutcome outcome = RefreshOutcome.Succeeded) =>
            new(fileName, DateTime.UtcNow, DateTime.UtcNow, outcome, 10, 10, null, null);

        [Fact]
        public void RecordedOutcomesAreReadBackByName()
        {
            var store = new RefreshStatusStore();
            store.Record(Status("nightly"));

            Assert.Equal(RefreshOutcome.Succeeded, store.Get("nightly")!.Outcome);
        }

        // Statuses are keyed by file name, and file names get reused: delete a broken definition,
        // create a new one under the same name, and without this the new one would report the old
        // one's failure until the next scheduled run.
        [Fact]
        public void ForgettingADeletedDefinitionStopsItsOutcomeHauntingTheNextOneOfThatName()
        {
            var store = new RefreshStatusStore();
            store.Record(Status("recycled", RefreshOutcome.Failed));

            store.Forget("recycled");

            Assert.Null(store.Get("recycled"));
            Assert.DoesNotContain("recycled", store.GetAll().Keys, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void ForgettingSomethingNeverRecordedIsNotAnError()
        {
            var store = new RefreshStatusStore();
            store.Record(Status("kept"));

            store.Forget("never-ran");

            Assert.NotNull(store.Get("kept"));
        }

        [Fact]
        public void ForgettingOneDefinitionLeavesTheOthersAlone()
        {
            var store = new RefreshStatusStore();
            store.Record(Status("one"));
            store.Record(Status("two"));

            store.Forget("one");

            Assert.Null(store.Get("one"));
            Assert.NotNull(store.Get("two"));
        }
    }
}
