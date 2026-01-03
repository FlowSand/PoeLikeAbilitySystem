using System.IO;
using Combat.Runtime;
using Combat.Runtime.Commands;
using Combat.Runtime.GraphRuntime;
using Combat.Runtime.Model;
using Combat.Runtime.Trace;
using NUnit.Framework;

namespace Combat.Runtime.Tests.Trace
{
    public sealed class TraceRecorderTests
    {
        [Test]
        public void BeginTrace_SetsMetadataCorrectly()
        {
            var recorder = new TraceRecorder(captureSlots: false);
            var ctx = new ExecutionContext
            {
                eventType = "OnHitEvent",
                rootEventId = 42,
                triggerDepth = 1,
                randomSeed = 12345,
                sourceGraphId = "test-graph-id",
                casterUnitId = new UnitId(1),
                targetUnitId = new UnitId(2)
            };

            recorder.BeginTrace(ctx, planHash: 0xDEADBEEF, sourceGraphId: "test-graph-id");
            var trace = recorder.GetTrace();

            Assert.AreEqual("OnHitEvent", trace.eventType);
            Assert.AreEqual(42, trace.rootEventId);
            Assert.AreEqual(1, trace.triggerDepth);
            Assert.AreEqual(12345u, trace.randomSeed);
            Assert.AreEqual("test-graph-id", trace.sourceGraphId);
            Assert.AreEqual(0xDEADBEEF, trace.planHash);
            Assert.AreEqual(new UnitId(1), trace.casterUnitId);
            Assert.AreEqual(new UnitId(2), trace.targetUnitId);
        }

        [Test]
        public void RecordOpEnd_AddsExecutionRecord()
        {
            var recorder = new TraceRecorder(captureSlots: false);
            var ctx = CreateTestContext();
            recorder.BeginTrace(ctx, planHash: 0x1234, sourceGraphId: "test");

            recorder.RecordOpBegin(0, OpCode.ConstNumber);
            recorder.RecordOpEnd(0, microseconds: 100);

            var trace = recorder.GetTrace();

            Assert.AreEqual(1, trace.opExecutions.Count);
            Assert.AreEqual(0, trace.opExecutions[0].opIndex);
            Assert.AreEqual("ConstNumber", trace.opExecutions[0].opCode);
            Assert.AreEqual(100, trace.opExecutions[0].microseconds);
            Assert.AreEqual(1, trace.totalOpsExecuted);
        }

        [Test]
        public void RecordOpEnd_MultipleOps_RecordsInOrder()
        {
            var recorder = new TraceRecorder(captureSlots: false);
            var ctx = CreateTestContext();
            recorder.BeginTrace(ctx, planHash: 0x1234, sourceGraphId: "test");

            recorder.RecordOpBegin(0, OpCode.ConstNumber);
            recorder.RecordOpEnd(0, microseconds: 100);

            recorder.RecordOpBegin(1, OpCode.Add);
            recorder.RecordOpEnd(1, microseconds: 50);

            recorder.RecordOpBegin(2, OpCode.MakeDamage);
            recorder.RecordOpEnd(2, microseconds: 200);

            var trace = recorder.GetTrace();

            Assert.AreEqual(3, trace.opExecutions.Count);
            Assert.AreEqual(3, trace.totalOpsExecuted);

            Assert.AreEqual(0, trace.opExecutions[0].opIndex);
            Assert.AreEqual("ConstNumber", trace.opExecutions[0].opCode);

            Assert.AreEqual(1, trace.opExecutions[1].opIndex);
            Assert.AreEqual("Add", trace.opExecutions[1].opCode);

            Assert.AreEqual(2, trace.opExecutions[2].opIndex);
            Assert.AreEqual("MakeDamage", trace.opExecutions[2].opCode);
        }

        [Test]
        public void RecordCommand_AddsCommandRecord()
        {
            var recorder = new TraceRecorder(captureSlots: false);
            var ctx = CreateTestContext();
            recorder.BeginTrace(ctx, planHash: 0x1234, sourceGraphId: "test");

            var spec = new DamageSpec(
                sourceUnitId: new UnitId(1),
                targetUnitId: new UnitId(2),
                baseValue: 100,
                damageType: DamageType.Physical
            );
            var command = new ApplyDamageCommand(spec);

            recorder.RecordCommand(command, opIndex: 3);

            var trace = recorder.GetTrace();

            Assert.AreEqual(1, trace.commands.Count);
            Assert.AreEqual(1, trace.totalCommandsEmitted);
            Assert.AreEqual("ApplyDamageCommand", trace.commands[0].commandType);
            Assert.AreEqual(3, trace.commands[0].emittedAtOpIndex);
            Assert.IsNotNull(trace.commands[0].commandData);
        }

        [Test]
        public void EndTrace_SetsTotalExecutionTime()
        {
            var recorder = new TraceRecorder(captureSlots: false);
            var ctx = CreateTestContext();
            recorder.BeginTrace(ctx, planHash: 0x1234, sourceGraphId: "test");

            recorder.EndTrace(totalMicroseconds: 5000);

            var trace = recorder.GetTrace();

            Assert.AreEqual(5000, trace.totalExecutionMicroseconds);
        }

        [Test]
        public void JsonRoundTrip_PreservesAllData()
        {
            // Create original trace
            var recorder = new TraceRecorder(captureSlots: false);
            var ctx = CreateTestContext();
            recorder.BeginTrace(ctx, planHash: 0xABCDEF, sourceGraphId: "test-graph");

            recorder.RecordOpBegin(0, OpCode.ConstNumber);
            recorder.RecordOpEnd(0, microseconds: 100);

            recorder.RecordOpBegin(1, OpCode.MakeDamage);
            recorder.RecordOpEnd(1, microseconds: 200);

            var command = new ApplyDamageCommand(
                new DamageSpec(
                    sourceUnitId: new UnitId(1),
                    targetUnitId: new UnitId(2),
                    baseValue: 100,
                    damageType: DamageType.Physical
                )
            );
            recorder.RecordCommand(command, opIndex: 1);

            recorder.EndTrace(totalMicroseconds: 300);

            var originalTrace = recorder.GetTrace();

            // Export to JSON
            string tempPath = Path.Combine(Path.GetTempPath(), "test_trace.json");
            try
            {
                string json = UnityEngine.JsonUtility.ToJson(originalTrace, prettyPrint: true);
                File.WriteAllText(tempPath, json);

                // Import from JSON
                string loadedJson = File.ReadAllText(tempPath);
                var loadedTrace = UnityEngine.JsonUtility.FromJson<ExecutionTrace>(loadedJson);

                // Verify all fields
                Assert.AreEqual(originalTrace.eventType, loadedTrace.eventType);
                Assert.AreEqual(originalTrace.rootEventId, loadedTrace.rootEventId);
                Assert.AreEqual(originalTrace.triggerDepth, loadedTrace.triggerDepth);
                Assert.AreEqual(originalTrace.randomSeed, loadedTrace.randomSeed);
                Assert.AreEqual(originalTrace.sourceGraphId, loadedTrace.sourceGraphId);
                Assert.AreEqual(originalTrace.planHash, loadedTrace.planHash);
                Assert.AreEqual(originalTrace.totalExecutionMicroseconds, loadedTrace.totalExecutionMicroseconds);
                Assert.AreEqual(originalTrace.totalOpsExecuted, loadedTrace.totalOpsExecuted);
                Assert.AreEqual(originalTrace.totalCommandsEmitted, loadedTrace.totalCommandsEmitted);

                // Verify Op executions
                Assert.AreEqual(originalTrace.opExecutions.Count, loadedTrace.opExecutions.Count);
                for (int i = 0; i < originalTrace.opExecutions.Count; i++)
                {
                    Assert.AreEqual(originalTrace.opExecutions[i].opIndex, loadedTrace.opExecutions[i].opIndex);
                    Assert.AreEqual(originalTrace.opExecutions[i].opCode, loadedTrace.opExecutions[i].opCode);
                    Assert.AreEqual(originalTrace.opExecutions[i].microseconds, loadedTrace.opExecutions[i].microseconds);
                }

                // Verify Commands
                Assert.AreEqual(originalTrace.commands.Count, loadedTrace.commands.Count);
                for (int i = 0; i < originalTrace.commands.Count; i++)
                {
                    Assert.AreEqual(originalTrace.commands[i].commandType, loadedTrace.commands[i].commandType);
                    Assert.AreEqual(originalTrace.commands[i].emittedAtOpIndex, loadedTrace.commands[i].emittedAtOpIndex);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Test]
        public void TraceExporter_ExportToJson_CreatesFile()
        {
            var recorder = new TraceRecorder(captureSlots: false);
            var ctx = CreateTestContext();
            recorder.BeginTrace(ctx, planHash: 0x1234, sourceGraphId: "test");

            recorder.RecordOpBegin(0, OpCode.ConstNumber);
            recorder.RecordOpEnd(0, microseconds: 100);
            recorder.EndTrace(totalMicroseconds: 100);

            var trace = recorder.GetTrace();

            string filePath = TraceExporter.ExportToJson(trace, fileName: "test_export.json");

            Assert.IsTrue(File.Exists(filePath), $"Trace file should exist at: {filePath}");

            // Cleanup
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        [Test]
        public void TraceExporter_ImportFromJson_ReturnsCorrectTrace()
        {
            var recorder = new TraceRecorder(captureSlots: false);
            var ctx = CreateTestContext();
            ctx.eventType = "TestEvent";
            ctx.rootEventId = 999;

            recorder.BeginTrace(ctx, planHash: 0xCAFEBABE, sourceGraphId: "import-test");
            recorder.RecordOpBegin(0, OpCode.GetCaster);
            recorder.RecordOpEnd(0, microseconds: 75);
            recorder.EndTrace(totalMicroseconds: 75);

            var originalTrace = recorder.GetTrace();

            string filePath = TraceExporter.ExportToJson(originalTrace, fileName: "test_import.json");

            try
            {
                var loadedTrace = TraceExporter.ImportFromJson(filePath);

                Assert.AreEqual("TestEvent", loadedTrace.eventType);
                Assert.AreEqual(999, loadedTrace.rootEventId);
                Assert.AreEqual(0xCAFEBABE, loadedTrace.planHash);
                Assert.AreEqual(1, loadedTrace.opExecutions.Count);
                Assert.AreEqual("GetCaster", loadedTrace.opExecutions[0].opCode);
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        private static ExecutionContext CreateTestContext()
        {
            return new ExecutionContext
            {
                eventType = "TestEvent",
                rootEventId = 1,
                triggerDepth = 0,
                randomSeed = 42,
                sourceGraphId = "test-graph",
                casterUnitId = new UnitId(1),
                targetUnitId = new UnitId(2)
            };
        }
    }
}
