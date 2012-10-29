using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class MassProjectionsScenario : ProjectionsKillScenario
    {
        public MassProjectionsScenario(Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int threads, int streams, int eventsPerStream, int streamDeleteStep) 
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
        }

        protected override void RunInternal()
        {
            var success = true;

            var nodeProcessId = StartNode();

            var countProjections = new List<string>();
            var bankProjections = new List<string>();

            while (GetIterationCode() < Streams / 3)
            {
                SetNextIterationCode();

                countProjections.Add(CreateCountItem());
                bankProjections.Add(CreateSumCheckForBankAccount0());

                Log.Info("Created {0} and {1}", bankProjections[bankProjections.Count - 1], 
                                                countProjections[countProjections.Count - 1]);
            }

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            var writeTask = WriteData();
            writeTask.Wait();

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            int count = 10;
            while (count > 0)
            {
                Log.Info("Stop and start projection, remaining iterations {0}, waiting for data to be written.", count);

                var t1 = StartOrStopProjection(countProjections, false);
                var t2 = StartOrStopProjection(bankProjections, false);

                Task.WaitAll(new[] { t1, t2 });
                Thread.Sleep(TimeSpan.FromSeconds(10));

                var t3 = StartOrStopProjection(countProjections, true);
                var t4 = StartOrStopProjection(bankProjections, true);

                Task.WaitAll(new[] { t3, t4 });
                var sleepTimeSeconds = 60 + Streams * EventsPerStream / 1000.0;
                Log.Info("Sleep 1 for {0} seconds", sleepTimeSeconds);
                Thread.Sleep(TimeSpan.FromSeconds(sleepTimeSeconds));

                if (writeTask.IsCompleted)
                    count -= 1;

                if (writeTask.IsFaulted)
                    throw new ApplicationException("Failed to write data", writeTask.Exception);

                success = CheckProjectionState(GetConnection(), 
                                                        bankProjections[bankProjections.Count - 1], 
                                                        "success", 
                                                        x => x == EventsPerStream.ToString());
                if (success)
                    break;
            }

            count = 10;
            success = false;
            while (!success && count > 0)
            {
                Log.Info("Wait until projections are computed, remaining iterations {0}", count);
                KillNode(nodeProcessId);
                nodeProcessId = StartNode();

                var sleepTimeSeconds = (Streams * EventsPerStream) / 50;
                Log.Info("Sleep 2 for {0} seconds", sleepTimeSeconds);
                Thread.Sleep(TimeSpan.FromSeconds(sleepTimeSeconds));

                success = CheckProjectionState(GetConnection(),
                                                        bankProjections[bankProjections.Count - 1],
                                                        "success",
                                                        x => x == EventsPerStream.ToString());
                count -= 1;
            }

            if (!success)
                throw new ApplicationException("Last bank projection failed");
        }

        private Task StartOrStopProjection(IEnumerable<string> projections, bool enable)
        {
            var tasks = new List<Task>();
            var store = GetConnection();

            foreach (string projection in projections)
            {

                //var isRunning = store.Projections.GetStatus(projection) == "Enabled";

                tasks.Add(enable
                              ? store.Projections.EnableAsync(projection)
                              : store.Projections.DisableAsync(projection));

                while (tasks.Count(x => !x.IsCompleted) > 4)
                    Thread.Sleep(50);
            }

            var task = Task.Factory.ContinueWhenAll(tasks.ToArray(), ts => { Task.WaitAll(ts); Log.Info("Projections enable/disable finished."); });
            return task;
        }

        private int _iterationCode;
        protected override int GetIterationCode()
        {
            return _iterationCode;
        }

        private void SetNextIterationCode()
        {
            _iterationCode += 1;
        }

        protected override TimeSpan IterationSleepInterval
        {
            get { return TimeSpan.FromMinutes(1); }
        }
    }
}