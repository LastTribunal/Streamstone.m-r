using System;
using System.Collections.Generic;
using System.Linq;

using Streamstone;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;

namespace SimpleCQRS
{
    public interface IEventStore
    {
        void SaveEvents(Guid aggregateId, Event[] events, int expectedVersion);
        List<Event> GetEventsForAggregate(Guid aggregateId);
    }

    public class EventStore : IEventStore
    {
        private readonly CloudTable _table;
        private readonly IProjectionPublisher _publisher;

        public EventStore(CloudTable table, IProjectionPublisher publisher)
        {
            _publisher = publisher;
            _table = table;
        }

        public void SaveEvents(Guid aggregateId, Event[] events, int expectedVersion)
        {
            var i = expectedVersion;

            // iterate through current aggregate events increasing version with each processed event
            foreach (var @event in events)
            {
                i++;
                @event.Version = i;
            }

            var partition = Partition(aggregateId);
  
            var existent = Stream.TryOpen(partition);
            var stream = existent.Found
                ? existent.Stream
                : new Stream(partition);

            if (stream.Version != expectedVersion)
                throw new ConcurrencyException();

            try
            {
                var projections = events.Select(e => _publisher.Publish(e));
                Stream.Write(stream, events.Zip(projections, ToEventData).ToArray());
            }
            catch (ConcurrencyConflictException)
            {
                throw new ConcurrencyException();
            }
        }

        // collect all processed events for given aggregate and return them as a list
        // used to build up an aggregate from its history (Domain.LoadsFromHistory)
        public List<Event> GetEventsForAggregate(Guid aggregateId)
        {
            var partition = Partition(aggregateId);
            
            if (!Stream.Exists(partition))
            {
                throw new AggregateNotFoundException();
            }

            return Stream.Read<EventEntity>(partition).Events.Select(ToEvent).ToList();
        }

        Partition Partition(Guid aggregateId)
        {
            return new Partition(_table, "Items|" + aggregateId.ToString("D"));
        }

        static Event ToEvent(EventEntity e)
        {
            return (Event) JsonConvert.DeserializeObject(e.Data, Type.GetType(e.Type));
        }

        static EventData ToEventData(Event e, IEnumerable<Include> includes)
        {
            var id = Guid.NewGuid();

            var properties = new
            {
                Id = id,
                Type = e.GetType().FullName,
                Data = JsonConvert.SerializeObject(e)
            };

            return new EventData(
                EventId.From(id), 
                EventProperties.From(properties), 
                EventIncludes.From(includes)
            );
        }

        class EventEntity : TableEntity
        {
            public string Type { get; set; }
            public string Data { get; set; }
        }
    }

    public class AggregateNotFoundException : Exception
    {
    }

    public class ConcurrencyException : Exception
    {
    }
}
