using System;
using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist;
using Jellyfin.Plugin.SmarterPlaylist.Api;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    /// <summary>
    /// Pins the order vocabulary to one source, the way <see cref="OperatorRegistryTest"/> does for
    /// operators.
    /// </summary>
    public class OrderRegistryTest
    {
        // The failure this registry exists to prevent. An order class with no switch arm used to
        // resolve to NoOrder while still being offered by the page, so choosing it silently left items
        // in library order -- looking like a broken sort rather than a missing wire-up.
        [Fact]
        public void EveryRegisteredNameResolvesToAnOrderClaimingThatName()
        {
            foreach (var name in OrderRegistry.Names)
            {
                var order = OrderRegistry.Resolve(name);

                Assert.Equal(name, order.Name);
            }
        }

        // Catches the same gap from the other side: a name that resolves to the fallback is either
        // unregistered or unwired, and only NoOrder is allowed to be NoOrder.
        [Fact]
        public void OnlyNoOrderResolvesToTheFallbackType()
        {
            foreach (var name in OrderRegistry.Names.Where(n => !string.Equals(n, NoOrder.OrderName, StringComparison.Ordinal)))
            {
                Assert.IsNotType<NoOrder>(OrderRegistry.Resolve(name));
            }
        }

        [Fact]
        public void TheSchemaOffersExactlyTheRegisteredOrders()
        {
            Assert.Equal(OrderRegistry.Names, SchemaBuilder.Build().Orders);
        }

        [Fact]
        public void AnUnknownNameFallsBackToNoOrder()
        {
            Assert.IsType<NoOrder>(OrderRegistry.Resolve("Nonsense"));
            Assert.IsType<NoOrder>(OrderRegistry.Resolve(null));
            Assert.IsType<NoOrder>(OrderRegistry.Resolve(string.Empty));
        }

        // Names are compared case-sensitively, matching the switch this replaced and every other name
        // comparison in the plugin.
        [Fact]
        public void NamesAreCaseSensitive()
        {
            Assert.IsType<NoOrder>(OrderRegistry.Resolve("noorder"));
            Assert.False(OrderRegistry.IsKnown("release date ascending"));
            Assert.True(OrderRegistry.IsKnown(PremiereDateOrder.OrderName));
        }

        // Preserves the previous behaviour of one Order per playlist. Today's orders are stateless, so
        // sharing them would work; requiring that would make a future stateful order a race between
        // concurrent refreshes instead of a compile error.
        [Fact]
        public void ResolveReturnsAFreshInstanceEachTime()
        {
            Assert.NotSame(
                OrderRegistry.Resolve(PremiereDateOrder.OrderName),
                OrderRegistry.Resolve(PremiereDateOrder.OrderName));
        }

        [Fact]
        public void NoOrderIsOfferedFirstSoItReadsAsTheDefault()
        {
            Assert.Equal(NoOrder.OrderName, OrderRegistry.Names[0]);
            Assert.Equal(NoOrder.OrderName, new OrderDto().Name);
        }

        [Fact]
        public void EveryRegisteredNameIsDistinct()
        {
            Assert.Equal(OrderRegistry.Names.Count, OrderRegistry.Names.Distinct(StringComparer.Ordinal).Count());
        }

        // The registry and the definition path must agree: what SmarterPlaylist builds from a DTO is
        // what the registry resolves for that same name.
        [Fact]
        public void SmarterPlaylistBuildsTheOrderTheRegistryResolves()
        {
            foreach (var name in OrderRegistry.Names)
            {
                var dto = new SmarterPlaylistDto { Order = new OrderDto { Name = name } };

                Assert.IsType(OrderRegistry.Resolve(name).GetType(), new SmarterPlaylist(dto).Order);
            }
        }

        // Validation warns on an unrecognised order rather than erroring, because the refresh still
        // succeeds -- the items just come back in library order.
        [Fact]
        public void AnUnknownOrderIsAWarningNotAnError()
        {
            var dto = new SmarterPlaylistDto { Name = "T", User = "rob", Order = new OrderDto { Name = "Nonsense" } };
            var set = new ExpressionSet();
            set.Expressions.Add(new QueryEngine.Expression("Name", "Contains", "x"));
            dto.ExpressionSets.Add(set);

            var diagnostics = DefinitionValidator.Validate(dto, SchemaBuilder.Build());

            Assert.Contains(diagnostics, d => d.Code == "W01" && d.Severity == DiagnosticSeverity.Warning);
            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }
    }
}
