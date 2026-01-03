using System;
using Combat.Runtime.Events;

namespace Combat.Runtime
{
    /// <summary>
    /// Event queue using a ring buffer (circular array) for efficient FIFO operations.
    /// Enqueues EventEnvelope (which boxes ICombatEvent once) and dequeues in order.
    /// Auto-resizes with doubling strategy when full.
    /// </summary>
    public class EventQueue
    {
        private EventEnvelope[] _buffer;
        private int _head;
        private int _tail;
        private int _count;

        /// <summary>
        /// Create an event queue with the specified initial capacity.
        /// </summary>
        public EventQueue(int initialCapacity = 64)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be positive");
            }

            _buffer = new EventEnvelope[initialCapacity];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// Number of events currently in the queue.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Returns true if there are pending events to process.
        /// </summary>
        public bool HasPendingEvents => _count > 0;

        /// <summary>
        /// Enqueue an event with metadata. If the queue is full, it will resize (double capacity).
        /// </summary>
        public void Enqueue(ICombatEvent evt, int rootEventId, int triggerDepth, uint seed)
        {
            if (_count == _buffer.Length)
            {
                Resize(_buffer.Length * 2);
            }

            _buffer[_tail] = new EventEnvelope(rootEventId, triggerDepth, seed, evt);
            _tail = (_tail + 1) % _buffer.Length;
            _count++;
        }

        /// <summary>
        /// Try to dequeue the next event. Returns false if the queue is empty.
        /// </summary>
        public bool TryDequeue(out EventEnvelope envelope)
        {
            if (_count == 0)
            {
                envelope = default;
                return false;
            }

            envelope = _buffer[_head];
            _buffer[_head] = default; // Clear reference to help GC
            _head = (_head + 1) % _buffer.Length;
            _count--;
            return true;
        }

        /// <summary>
        /// Resize the internal buffer to the new capacity.
        /// Preserves the circular buffer invariant by copying elements in order.
        /// </summary>
        private void Resize(int newCapacity)
        {
            var newBuffer = new EventEnvelope[newCapacity];

            // Copy elements from head to end of old buffer
            if (_head < _tail)
            {
                // Simple case: no wraparound
                Array.Copy(_buffer, _head, newBuffer, 0, _count);
            }
            else
            {
                // Wraparound case: copy head->end, then start->tail
                int firstPartLength = _buffer.Length - _head;
                Array.Copy(_buffer, _head, newBuffer, 0, firstPartLength);
                Array.Copy(_buffer, 0, newBuffer, firstPartLength, _tail);
            }

            _buffer = newBuffer;
            _head = 0;
            _tail = _count;
        }
    }
}
