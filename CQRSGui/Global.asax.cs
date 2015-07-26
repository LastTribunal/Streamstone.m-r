using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

using SimpleCQRS;
using Microsoft.WindowsAzure.Storage;

namespace CQRSGui
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );

        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RegisterRoutes(RouteTable.Routes);

            var bus = new FakeBus();
            ServiceLocator.Bus = bus;

            var client = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudTableClient();
            var table = client.GetTableReference("Streams");
            table.CreateIfNotExists();

            var storage = new EventStore(table, bus);
            var rep = new Repository<InventoryItem>(storage);
            
            var commands = new InventoryCommandHandlers(rep);
            bus.RegisterCommandHandler<CheckInItemsToInventory>(commands.Handle);
            bus.RegisterCommandHandler<CreateInventoryItem>(commands.Handle);
            bus.RegisterCommandHandler<DeactivateInventoryItem>(commands.Handle);
            bus.RegisterCommandHandler<RemoveItemsFromInventory>(commands.Handle);
            bus.RegisterCommandHandler<RenameInventoryItem>(commands.Handle);

            var detail = new InventoryItemProjection(table, "Items");
            bus.RegisterProjectionHandler<InventoryItemCreated>(detail.Project);
            bus.RegisterProjectionHandler<InventoryItemDeactivated>(detail.Project);
            bus.RegisterProjectionHandler<InventoryItemRenamed>(detail.Project);
            bus.RegisterProjectionHandler<ItemsCheckedInToInventory>(detail.Project);
            bus.RegisterProjectionHandler<ItemsRemovedFromInventory>(detail.Project);
        }
    }
}