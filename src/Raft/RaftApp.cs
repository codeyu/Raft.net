﻿using System.Threading.Tasks;
using Raft.Contracts;
using Raft.Core.StateMachine;
using Raft.Exceptions;
using Raft.Infrastructure.Disruptor;
using Raft.Server.BufferEvents;

namespace Raft
{
    internal class RaftApp : IRaft
    {
        private readonly IPublishToBuffer<CommandScheduled> _commandPublisher;
        private readonly INode _node;

        public RaftApp(IPublishToBuffer<CommandScheduled> commandPublisher, INode node)
        {
            _commandPublisher = commandPublisher;
            _node = node;
        }

        public Task ExecuteCommand<T>(T command) where T : IRaftCommand, new()
        {
            if (_node.CurrentState != NodeState.Leader)
                throw new NotClusterLeaderException();

            return _commandPublisher.PublishEvent(
                new CommandScheduled
                {
                    Command = command
                });
        }

        public string GetClusterLeader()
        {
            // TODO: Expose method to get cluster leader.
            return string.Empty;
        }
    }
}
