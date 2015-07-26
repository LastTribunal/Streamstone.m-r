using System;
using System.Linq;
using System.Collections.Generic;

using Streamstone;
using Streamstone.Utility;

using Microsoft.WindowsAzure.Storage.Table;

namespace SimpleCQRS
{
    public interface IReadModelFacade
    {
        IEnumerable<InventoryItemListDto> GetInventoryItems();
        InventoryItemDetailsDto GetInventoryItemDetails(Guid id);
    }

    public class InventoryItemDetailsDto
    {
        public readonly Guid Id;
        public readonly string Name;
        public readonly int CurrentCount;
        public readonly int Version;

        public InventoryItemDetailsDto(Guid id, string name, int currentCount, int version)
        {
            Id = id;
            Name = name;
            CurrentCount = currentCount;
            Version = version;
        }
    }

    public class InventoryItemListDto
    {
        public readonly Guid Id;
        public readonly string Name;

        public InventoryItemListDto(Guid id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    public class InventoryItemProjection : 
        Projects<InventoryItemCreated>, 
        Projects<InventoryItemDeactivated>, 
        Projects<InventoryItemRenamed>, 
        Projects<ItemsRemovedFromInventory>, 
        Projects<ItemsCheckedInToInventory>
    {
        readonly CloudTable table;
        readonly string partition;

        public InventoryItemProjection(CloudTable table, string partition)
        {
            this.table = table;
            this.partition = partition;
        }

        public IEnumerable<Include> Project(InventoryItemCreated @event)
        {
            yield return Include.Insert(new InventoryItemEntity(@event.Id)
            {
                Name = @event.Name,
                Version = @event.Version
            });
        }

        public IEnumerable<Include> Project(InventoryItemRenamed @event)
        {
            var entity = GetItemEntity(@event.Id);

            yield return Include.Replace(new InventoryItemEntity(entity)
            {
                Name = @event.NewName,
                Version = @event.Version
            });
        }

        public IEnumerable<Include> Project(ItemsRemovedFromInventory @event)
        {
            var entity = GetItemEntity(@event.Id);

            yield return Include.Replace(new InventoryItemEntity(entity)
            {
                CurrentCount = entity.CurrentCount - @event.Count,
                Version = @event.Version
            });
        }

        public IEnumerable<Include> Project(ItemsCheckedInToInventory @event)
        {
            var entity = GetItemEntity(@event.Id);

            yield return Include.Replace(new InventoryItemEntity(entity)
            {
                CurrentCount = entity.CurrentCount + @event.Count,
                Version = @event.Version
            });
        }

        public IEnumerable<Include> Project(InventoryItemDeactivated @event)        
        {
            var entity = GetItemEntity(@event.Id);
            yield return Include.Delete(new InventoryItemEntity(entity));
        }

        private InventoryItemEntity GetItemEntity(Guid id)
        {
            var entities = table.CreateQuery<InventoryItemEntity>()
                 .Where(x => x.PartitionKey == partition)
                 .Where(x => x.RowKey == InventoryItemEntity.EntityRowKey(id))
                 .ToList();

            if (entities.Count == 0)
                throw new InvalidOperationException("did not find the original inventory this shouldnt happen");

            return entities[0];
        }
    }

    class InventoryItemEntity : TableEntity
    {
        public const string Prefix = "item|";

        public InventoryItemEntity()
        {}

        public InventoryItemEntity(Guid id)
        {
            Id = id;
            RowKey = EntityRowKey(id);
        }

        public InventoryItemEntity(InventoryItemEntity from) 
            : this(from.Id)
        {
            Name = from.Name;
            CurrentCount = from.CurrentCount;
            Version = from.Version;
            ETag = from.ETag;
        }

        public static string EntityRowKey(Guid id)
        {
            return Prefix + id.ToString("D");
        }

        public Guid Id          { get; set; }
        public string Name      { get; set; }
        public int CurrentCount { get; set; }
        public int Version      { get; set; }
    }

    public class ReadModelFacade : IReadModelFacade
    {
        readonly CloudTable table;
        readonly string partition;

        public ReadModelFacade(CloudTable table, string partition)
        {
            this.table = table;
            this.partition = partition;
        }

        public IEnumerable<InventoryItemListDto> GetInventoryItems()
        {
            return table.CreateQuery<InventoryItemEntity>()
                        .Where(x => x.PartitionKey == partition)
                        .WhereRowKeyPrefix(InventoryItemEntity.Prefix).ToList()
                        .Select(item => new InventoryItemListDto(item.Id, item.Name));
        }

        public InventoryItemDetailsDto GetInventoryItemDetails(Guid id)
        {
            return table.CreateQuery<InventoryItemEntity>()
                    .Where(x => x.PartitionKey == partition)
                    .Where(x => x.RowKey == InventoryItemEntity.EntityRowKey(id)).ToList()
                    .Select(item => new InventoryItemDetailsDto(item.Id, item.Name, item.CurrentCount, item.Version))
                    .SingleOrDefault();

        }
    }
}
