using System.Diagnostics;
using EventStore.Client;
using Eventuous.Diagnostics;
using Eventuous.EventStore;
using Eventuous.TestHelpers;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Fixtures;

public class StoreFixture : StoreFixtureBase<EventStoreDbContainer> {
    public EventStoreClient Client { get; private set; } = null!;

    readonly ActivityListener _listener = DummyActivityListener.Create();

    static StoreFixture() {
        AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http2FlowControl.DisableDynamicWindowSizing", true);
    }

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(TestPrimitives.DefaultOptions);

    public StoreFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        ActivitySource.AddActivityListener(_listener);
    }

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventStoreClient(Container.GetConnectionString());
        services.AddAggregateStore<EsdbEventStore>();
    }

    protected override EventStoreDbContainer CreateContainer() => EsdbContainer.Create();

    protected override void GetDependencies(IServiceProvider provider) {
        Client = provider.GetRequiredService<EventStoreClient>();
        provider.AddEventuousLogs();
    }
}
