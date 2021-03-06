// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.stream_reader
{
    [TestFixture]
    public class when_handling_read_completed_and_eof : TestFixtureWithExistingEvents
    {
        private StreamEventReader _edp;
        private Guid _publishWithCorrelationId;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        [SetUp]
        public new void When()
        {
            _publishWithCorrelationId = Guid.NewGuid();
            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new StreamEventReader(
                _ioDispatcher, _bus, _distibutionPointCorrelationId, null, "stream", 10, new RealTimeProvider(), false);
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "stream", 100, 100, ReadStreamResult.Success,
                    new[]
                        {
                            new ResolvedEvent(
                        new EventRecord(
                            10, 50, Guid.NewGuid(), _firstEventId, 50, 0, "stream", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                            new ResolvedEvent(
                        new EventRecord(
                            11, 100, Guid.NewGuid(), _secondEventId, 100, 0, "stream", ExpectedVersion.Any,
                            DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}), null)
                        }, null, false, "", 12, 11, true, 200));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "stream", 100, 100, ReadStreamResult.Success, new ResolvedEvent[0]
                    , null, false, "", 12, 11, true, 400));
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_be_resumed()
        {
            _edp.Resume();
        }

        [Test]
        public void cannot_be_paused()
        {
            _edp.Pause();
        }

        [Test]
        public void publishes_correct_committed_event_received_messages()
        {
            Assert.AreEqual(
                3, _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().Count());
            var first =
                _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().First();
            var second =
                _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>()
                         .Skip(1)
                         .First();
            var third =
                _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>()
                         .Skip(2)
                         .First();
            Assert.IsNull(third.Data);
            Assert.AreEqual(400, third.SafeTransactionFileReaderJoinPosition);

            Assert.AreEqual("event_type1", first.Data.EventType);
            Assert.AreEqual("event_type2", second.Data.EventType);
            Assert.AreEqual(_firstEventId, first.Data.EventId);
            Assert.AreEqual(_secondEventId, second.Data.EventId);
            Assert.AreEqual(1, first.Data.Data[0]);
            Assert.AreEqual(2, first.Data.Metadata[0]);
            Assert.AreEqual(3, second.Data.Data[0]);
            Assert.AreEqual(4, second.Data.Metadata[0]);
            Assert.AreEqual("stream", first.Data.EventStreamId);
            Assert.AreEqual("stream", second.Data.EventStreamId);
            Assert.AreEqual(50, first.Data.Position.PreparePosition);
            Assert.AreEqual(100, second.Data.Position.PreparePosition);
            Assert.AreEqual(-1, first.Data.Position.CommitPosition);
            Assert.AreEqual(-1, second.Data.Position.CommitPosition);
            Assert.AreEqual(50, first.Data.OriginalPosition.PreparePosition);
            Assert.AreEqual(100, second.Data.OriginalPosition.PreparePosition);
            Assert.AreEqual(-1, first.Data.OriginalPosition.CommitPosition);
            Assert.AreEqual(-1, second.Data.OriginalPosition.CommitPosition);
            Assert.AreEqual(50, first.SafeTransactionFileReaderJoinPosition);
            Assert.AreEqual(100, second.SafeTransactionFileReaderJoinPosition);
        }

        [Test]
        public void publishes_schedule()
        {
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<TimerMessage.Schedule>().Count());
        }

        [Test]
        public void publishes_read_events_on_schedule_reply()
        {
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<TimerMessage.Schedule>().Count());
            var schedule = _consumer.HandledMessages.OfType<TimerMessage.Schedule>().Single();
            schedule.Reply();
            Assert.AreEqual(3, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
        }
    }
}
