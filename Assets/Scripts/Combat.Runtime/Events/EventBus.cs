using System;
using System.Collections.Generic;

namespace Combat.Runtime.Events
{
    public sealed class EventBus
    {
        private readonly Dictionary<Type, object> _handlerListsByEventType = new Dictionary<Type, object>(32);

        public void Subscribe<T>(Action<T> handler) where T : struct, ICombatEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(T);
            if (!_handlerListsByEventType.TryGetValue(eventType, out var listObj))
            {
                var list = new HandlerList<T>();
                list.Add(handler);
                _handlerListsByEventType.Add(eventType, list);
                return;
            }

            ((HandlerList<T>)listObj).Add(handler);
        }

        public void Publish<T>(T evt) where T : struct, ICombatEvent
        {
            if (_handlerListsByEventType.TryGetValue(typeof(T), out var listObj))
            {
                ((HandlerList<T>)listObj).Publish(evt);
            }
        }

        private sealed class HandlerList<T> where T : struct, ICombatEvent
        {
            private Action<T>[] _handlers = new Action<T>[4];
            private int _count;

            public void Add(Action<T> handler)
            {
                if (_count == _handlers.Length)
                {
                    int newSize = _handlers.Length * 2;
                    var newHandlers = new Action<T>[newSize];
                    Array.Copy(_handlers, newHandlers, _handlers.Length);
                    _handlers = newHandlers;
                }

                _handlers[_count] = handler;
                _count++;
            }

            public void Publish(T evt)
            {
                for (int i = 0; i < _count; i++)
                {
                    _handlers[i](evt);
                }
            }
        }
    }
}

