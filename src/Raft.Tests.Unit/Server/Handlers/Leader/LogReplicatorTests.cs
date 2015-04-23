﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using FluentAssertions;
using NUnit.Framework;
using Raft.Core.Cluster;
using Raft.Infrastructure;
using Raft.Server.Handlers.Leader;
using Raft.Tests.Unit.TestData.Commands;

namespace Raft.Tests.Unit.Server.Handlers.Leader
{
    [TestFixture]
    public class LogReplicatorTests
    {
        [Test]
        public void HandlingPeerJoinedClusterEventCreatesActorForPeer()
        {
            // Arrange
            var handler = new LogReplicator();
            var actorDictionary = GetActorDictionary(handler);

            var @event = new PeerJoinedCluster
            {
                PeerInfo = new PeerInfo {NodeId = Guid.NewGuid()}
            };

            // Act
            handler.Handle(@event);

            // Assert
            actorDictionary.Count.Should().Be(1);
            actorDictionary.ContainsKey(@event.PeerInfo.NodeId).Should().BeTrue();
            actorDictionary[@event.PeerInfo.NodeId].Should().NotBeNull();
        }

        [Test]
        public void HandlingCommandScheduledEventWithNoPeersThrowsException()
        {
            // Arrange
            var handler = new LogReplicator();

            var @event = TestEventFactory.GetCommandEvent();

            // Act
            Action actAction = () => handler.Handle(@event);

            // Assert
            actAction.ShouldThrow<InvalidOperationException>();
        }

        [Test]
        public void HandlingCommandScheduledEventWithPostsToBroadcastBlock()
        {
            // Arrange
            var actors = new ConcurrentDictionary<Guid, Actor<ReplicateRequest>>(
                new Dictionary<Guid, Actor<ReplicateRequest>>
                {
                    {Guid.NewGuid(), new TestActor()},
                    {Guid.NewGuid(), new TestActor()}
                });

            var handler = new LogReplicator();
            SetActorDictionary(handler, actors);

            var broadcastBlock = GetBroadcastBlock(handler);
            actors.Values.ToList().ForEach(x => x.AddSourceLink(broadcastBlock));

            var @event = TestEventFactory.GetCommandEvent(1L, new byte[8]);

            ReplicateRequest request;
            broadcastBlock.TryReceive(out request).Should().BeFalse();

            // Act
            handler.Handle(@event);

            // Assert
            broadcastBlock.TryReceive(out request).Should().BeTrue();
            request.Entry.SequenceEqual(@event.EncodedEntry).Should().BeTrue();
        }

        // They say regions just hide ugly code :)
        #region Horrible Reflection Mess
        private ConcurrentDictionary<Guid, Actor<ReplicateRequest>> GetActorDictionary(LogReplicator logReplicator)
        {
            var field = logReplicator.GetType()
                .GetField("_replicationActors",
                BindingFlags.NonPublic |
                BindingFlags.GetField |
                BindingFlags.Instance);

            if (field == null)
                throw new MemberAccessException(
                    "Failed to access field '_replicationActors'. " +
                    "If the name has been changed, the reflection in this test must be changed too.");

            return (ConcurrentDictionary<Guid, Actor<ReplicateRequest>>)field.GetValue(logReplicator);
        }

        private void SetActorDictionary(LogReplicator logReplicator, ConcurrentDictionary<Guid, Actor<ReplicateRequest>> dict)
        {
            var field = logReplicator.GetType()
                .GetField("_replicationActors",
                BindingFlags.NonPublic |
                BindingFlags.GetField |
                BindingFlags.Instance);

            if (field == null)
                throw new MemberAccessException(
                    "Failed to access field '_replicationActors'. " +
                    "If the name has been changed, the reflection in this test must be changed too.");

            field.SetValue(logReplicator, dict);
        }

        private BroadcastBlock<ReplicateRequest> GetBroadcastBlock(LogReplicator logReplicator)
        {
            var field = logReplicator.GetType()
                .GetField("_entryBroadcastBlock",
                BindingFlags.NonPublic |
                BindingFlags.GetField |
                BindingFlags.Instance);

            if (field == null)
                throw new MemberAccessException(
                    "Failed to access field '_entryBroadcastBlock'. " +
                    "If the name has been changed, the reflection in this test must be changed too.");

            return (BroadcastBlock<ReplicateRequest>)field.GetValue(logReplicator);
        }
        #endregion

        private class TestActor : Actor<ReplicateRequest>
        {
            public override void Handle(ReplicateRequest message)
            {
                message.SuccessAction();
            }
        }
    }
}
