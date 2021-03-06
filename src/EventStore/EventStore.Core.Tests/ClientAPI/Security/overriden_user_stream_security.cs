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

using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class overriden_user_stream_security : AuthenticationTestBase
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            
            var settings = new SystemSettings(userStreamAcl: new StreamAcl("user1", "user1", "user1", "user1", "user1"),
                                              systemStreamAcl: null);
            Connection.SetSystemSettings(settings, new UserCredentials("adm", "admpa$$"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_user_stream_succeeds_for_authorized_user()
        {
            const string stream = "user-authorized-user";
            ExpectNoException(() => ReadEvent(stream, "user1", "pa$$1"));
            ExpectNoException(() => ReadStreamForward(stream, "user1", "pa$$1"));
            ExpectNoException(() => ReadStreamBackward(stream, "user1", "pa$$1"));

            ExpectNoException(() => WriteStream(stream, "user1", "pa$$1"));
            ExpectNoException(() => TransStart(stream, "user1", "pa$$1"));
            {
                var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("user1", "pa$$1"));
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta(stream, "user1", "pa$$1"));
            ExpectNoException(() => WriteMeta(stream, "user1", "pa$$1", null));

            ExpectNoException(() => SubscribeToStream(stream, "user1", "pa$$1"));

            ExpectNoException(() => DeleteStream(stream, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_user_stream_fail_for_not_authorized_user()
        {
            const string stream = "user-not-authorized";
            Expect<AccessDeniedException>(() => ReadEvent(stream, "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => ReadStreamForward(stream, "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => ReadStreamBackward(stream, "user2", "pa$$2"));

            Expect<AccessDeniedException>(() => WriteStream(stream, "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => TransStart(stream, "user2", "pa$$2"));
            {
                var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("user2", "pa$$2"));
                ExpectNoException(() => trans.Write());
                Expect<AccessDeniedException>(() => trans.Commit());
            };

            Expect<AccessDeniedException>(() => ReadMeta(stream, "user2", "pa$$2"));
            Expect<AccessDeniedException>(() => WriteMeta(stream, "user2", "pa$$2", null));

            Expect<AccessDeniedException>(() => SubscribeToStream(stream, "user2", "pa$$2"));

            Expect<AccessDeniedException>(() => DeleteStream(stream, "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_user_stream_fail_for_anonymous_user()
        {
            const string stream = "user-anonymous-user";
            Expect<AccessDeniedException>(() => ReadEvent(stream, null, null));
            Expect<AccessDeniedException>(() => ReadStreamForward(stream, null, null));
            Expect<AccessDeniedException>(() => ReadStreamBackward(stream, null, null));

            Expect<AccessDeniedException>(() => WriteStream(stream, null, null));
            Expect<AccessDeniedException>(() => TransStart(stream, null, null));
            {
                var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId);
                ExpectNoException(() => trans.Write());
                Expect<AccessDeniedException>(() => trans.Commit());
            };

            Expect<AccessDeniedException>(() => ReadMeta(stream, null, null));
            Expect<AccessDeniedException>(() => WriteMeta(stream, null, null, null));

            Expect<AccessDeniedException>(() => SubscribeToStream(stream, null, null));

            Expect<AccessDeniedException>(() => DeleteStream(stream, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void operations_on_user_stream_succeed_for_admin()
        {
            const string stream = "user-admin";
            ExpectNoException(() => ReadEvent(stream, "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamForward(stream, "adm", "admpa$$"));
            ExpectNoException(() => ReadStreamBackward(stream, "adm", "admpa$$"));

            ExpectNoException(() => WriteStream(stream, "adm", "admpa$$"));
            ExpectNoException(() => TransStart(stream, "adm", "admpa$$"));
            {
                var transId = TransStart(stream, "adm", "admpa$$").TransactionId;
                var trans = Connection.ContinueTransaction(transId, new UserCredentials("adm", "admpa$$"));
                ExpectNoException(() => trans.Write());
                ExpectNoException(() => trans.Commit());
            };

            ExpectNoException(() => ReadMeta(stream, "adm", "admpa$$"));
            ExpectNoException(() => WriteMeta(stream, "adm", "admpa$$", null));

            ExpectNoException(() => SubscribeToStream(stream, "adm", "admpa$$"));

            ExpectNoException(() => DeleteStream(stream, "adm", "admpa$$"));
        }
    }
}