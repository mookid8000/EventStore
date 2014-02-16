﻿// Copyright (c) 2012, Event Store LLP
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

using System.Threading;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_delete
{
    [TestFixture]
    public class with_from_all_foreach_projection_running_and_no_indexing : specification_with_standard_projections_runnning
    {
        protected override bool GivenStandardProjectionsRunning()
        {
            return false;
        }

        protected override void Given()
        {
            base.Given();
            PostEvent("stream1", "type1", "{}");
            PostEvent("stream1", "type2", "{}");
            PostEvent("stream2", "type1", "{}");
            PostEvent("stream2", "type2", "{}");
            WaitIdle();
            PostProjection(@"
fromAll().foreachStream().when({
    $init: function(){return {}},
    type1: function(s,e){s.a=1},
    type2: function(s,e){s.a=1},
    $deleted: function(s,e){s.deleted=1;},
}).outputState();
");
        }

        protected override void When()
        {
            base.When();
            this.HardDeleteStream("stream1");
            WaitIdle();
        }

        [Test, Category("Network")]
        public void receives_deleted_notification()
        {
            AssertStreamTail(
                "$projections-test-projection-stream1-result", "Result:{\"a\":1}", "Result:{\"a\":1,\"deleted\":1}");
        }
    }
}