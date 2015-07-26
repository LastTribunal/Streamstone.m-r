using System;
using System.Collections.Generic;
using System.Linq;

using Streamstone;

namespace SimpleCQRS
{
    public class FakeBus : ICommandSender, IProjectionPublisher
    {
        private readonly Dictionary<Type, Action<Message>> _commandHandlers = 
                     new Dictionary<Type, Action<Message>>();

        private readonly Dictionary<Type, List<Func<Event, IEnumerable<Include>>>> _projectionHandlers =
                     new Dictionary<Type, List<Func<Event, IEnumerable<Include>>>>();

        public void RegisterCommandHandler<T>(Action<T> handler) where T : Command
        {
            if (_commandHandlers.ContainsKey(typeof(T))) 
                throw new InvalidOperationException("cannot register more than one handler for command " + typeof(T));

            _commandHandlers.Add(typeof(T), x => handler((T)x));
        }

        public void RegisterProjectionHandler<T>(Func<T, IEnumerable<Include>> handler) where T : Event
        {
            List<Func<Event, IEnumerable<Include>>> handlers;

            if (!_projectionHandlers.TryGetValue(typeof(T), out handlers))
            {
                handlers = new List<Func<Event, IEnumerable<Include>>>();
                _projectionHandlers.Add(typeof(T), handlers);
            }

            handlers.Add((x => handler((T)x)));
        }

        public void Send<T>(T command) where T : Command
        {
            Action<Message> handler;

            if (!_commandHandlers.TryGetValue(typeof(T), out handler))
                throw new InvalidOperationException("no handler registered");
            
            handler(command);
        }

        public IEnumerable<Include> Publish<T>(T @event) where T : Event
        {
            List<Func<Event, IEnumerable<Include>>> handlers;

            if (!_projectionHandlers.TryGetValue(@event.GetType(), out handlers))
                return EventIncludes.None;

            return handlers.Select(handler => handler(@event).ToArray()).SelectMany(x => x);
        }
    }

    public interface Handles<in T> where T : Command
    {
        void Handle(T command);
    }

    public interface ICommandSender
    {
        void Send<T>(T command) where T : Command;
    }

    public interface Projects<in T> where T : Event
    {
        IEnumerable<Include> Project(T @event);
    }

    public interface IProjectionPublisher
    {
        IEnumerable<Include> Publish<T>(T @event) where T : Event;
    }
}
