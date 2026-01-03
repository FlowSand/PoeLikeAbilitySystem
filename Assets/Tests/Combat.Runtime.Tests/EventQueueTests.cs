using NUnit.Framework;
using Combat.Runtime.Events;
using Combat.Runtime.Model;

namespace Combat.Runtime.Tests
{
    [TestFixture]
    public class EventQueueTests
    {
        [Test]
        public void EventQueue_InitialState_IsEmpty()
        {
            var queue = new EventQueue(initialCapacity: 4);

            Assert.AreEqual(0, queue.Count);
            Assert.IsFalse(queue.HasPendingEvents);
        }

        [Test]
        public void EventQueue_EnqueueDequeue_FIFO()
        {
            var queue = new EventQueue(initialCapacity: 4);

            var evt1 = new OnCastEvent(new UnitId(1));
            var evt2 = new OnCastEvent(new UnitId(2));
            var evt3 = new OnCastEvent(new UnitId(3));

            queue.Enqueue(evt1, rootEventId: 1, triggerDepth: 0, seed: 100);
            queue.Enqueue(evt2, rootEventId: 1, triggerDepth: 1, seed: 200);
            queue.Enqueue(evt3, rootEventId: 2, triggerDepth: 0, seed: 300);

            Assert.AreEqual(3, queue.Count);

            Assert.IsTrue(queue.TryDequeue(out EventEnvelope envelope1));
            Assert.AreEqual(1, envelope1.rootEventId);
            Assert.AreEqual(0, envelope1.triggerDepth);
            Assert.AreEqual(100u, envelope1.randomSeed);
            Assert.AreEqual(1, ((OnCastEvent)envelope1.payload).CasterUnitId.Value);

            Assert.IsTrue(queue.TryDequeue(out EventEnvelope envelope2));
            Assert.AreEqual(1, envelope2.rootEventId);
            Assert.AreEqual(1, envelope2.triggerDepth);

            Assert.IsTrue(queue.TryDequeue(out EventEnvelope envelope3));
            Assert.AreEqual(2, envelope3.rootEventId);

            Assert.AreEqual(0, queue.Count);
            Assert.IsFalse(queue.HasPendingEvents);
        }

        [Test]
        public void EventQueue_TryDequeue_EmptyQueue_ReturnsFalse()
        {
            var queue = new EventQueue(initialCapacity: 4);

            bool result = queue.TryDequeue(out EventEnvelope envelope);

            Assert.IsFalse(result);
            Assert.AreEqual(default(EventEnvelope), envelope);
        }

        [Test]
        public void EventQueue_AutoResize_WhenFull()
        {
            var queue = new EventQueue(initialCapacity: 2);

            var evt = new OnCastEvent(new UnitId(1));

            // Fill initial capacity
            queue.Enqueue(evt, rootEventId: 1, triggerDepth: 0, seed: 1);
            queue.Enqueue(evt, rootEventId: 2, triggerDepth: 0, seed: 2);

            Assert.AreEqual(2, queue.Count);

            // Trigger resize (should double to 4)
            queue.Enqueue(evt, rootEventId: 3, triggerDepth: 0, seed: 3);

            Assert.AreEqual(3, queue.Count);

            // Verify order is preserved after resize
            Assert.IsTrue(queue.TryDequeue(out EventEnvelope e1));
            Assert.AreEqual(1, e1.rootEventId);

            Assert.IsTrue(queue.TryDequeue(out EventEnvelope e2));
            Assert.AreEqual(2, e2.rootEventId);

            Assert.IsTrue(queue.TryDequeue(out EventEnvelope e3));
            Assert.AreEqual(3, e3.rootEventId);
        }

        [Test]
        public void EventQueue_Wraparound_PreservesOrder()
        {
            var queue = new EventQueue(initialCapacity: 4);
            var evt = new OnCastEvent(new UnitId(1));

            // Fill queue
            queue.Enqueue(evt, rootEventId: 1, triggerDepth: 0, seed: 1);
            queue.Enqueue(evt, rootEventId: 2, triggerDepth: 0, seed: 2);
            queue.Enqueue(evt, rootEventId: 3, triggerDepth: 0, seed: 3);
            queue.Enqueue(evt, rootEventId: 4, triggerDepth: 0, seed: 4);

            // Dequeue two
            queue.TryDequeue(out _);
            queue.TryDequeue(out _);

            // Enqueue two more (this will wrap around in the ring buffer)
            queue.Enqueue(evt, rootEventId: 5, triggerDepth: 0, seed: 5);
            queue.Enqueue(evt, rootEventId: 6, triggerDepth: 0, seed: 6);

            // Verify correct order: 3, 4, 5, 6
            Assert.IsTrue(queue.TryDequeue(out EventEnvelope e3));
            Assert.AreEqual(3, e3.rootEventId);

            Assert.IsTrue(queue.TryDequeue(out EventEnvelope e4));
            Assert.AreEqual(4, e4.rootEventId);

            Assert.IsTrue(queue.TryDequeue(out EventEnvelope e5));
            Assert.AreEqual(5, e5.rootEventId);

            Assert.IsTrue(queue.TryDequeue(out EventEnvelope e6));
            Assert.AreEqual(6, e6.rootEventId);

            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void EventQueue_MultipleResizes_PreservesOrder()
        {
            var queue = new EventQueue(initialCapacity: 2);
            var evt = new OnCastEvent(new UnitId(1));

            // Enqueue 10 events (will trigger multiple resizes: 2->4->8->16)
            for (int i = 1; i <= 10; i++)
            {
                queue.Enqueue(evt, rootEventId: i, triggerDepth: 0, seed: (uint)i);
            }

            Assert.AreEqual(10, queue.Count);

            // Verify correct FIFO order
            for (int i = 1; i <= 10; i++)
            {
                Assert.IsTrue(queue.TryDequeue(out EventEnvelope e));
                Assert.AreEqual(i, e.rootEventId);
            }

            Assert.AreEqual(0, queue.Count);
        }
    }
}
