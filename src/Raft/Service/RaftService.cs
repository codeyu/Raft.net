﻿using System.ServiceModel;
using Raft.Core.Commands;
using Raft.Core.StateMachine;
using Raft.Core.Timer;
using Raft.Infrastructure.Disruptor;
using Raft.Server.BufferEvents;
using Raft.Service.Contracts;
using Serilog;

namespace Raft.Service
{
    internal class RaftService : IRaftService
    {
        private readonly IPublishToBuffer<AppendEntriesRequested> _appendEntriesPublisher;
        
        private readonly IPublishToBuffer<InternalCommandScheduled> _nodePublisher;
        private readonly INodeTimer _timer;
        private readonly INode _node;
        private readonly ILogger _logger;

        public RaftService(IPublishToBuffer<AppendEntriesRequested> appendEntriesPublisher,
            IPublishToBuffer<InternalCommandScheduled> nodePublisher, INodeTimer timer,
            INode node, ILogger logger)
        {
            _appendEntriesPublisher = appendEntriesPublisher;
            _nodePublisher = nodePublisher;

            _timer = timer;
            _node = node;
            _logger = logger;
        }

        public RequestVoteResponse RequestVote(RequestVoteRequest voteRequest)
        {
            if (voteRequest.Term <= _node.Properties.CurrentTerm)
            {
                return new RequestVoteResponse
                {
                    Term = _node.Properties.CurrentTerm,
                    VoteGranted = false
                };
            }

            _nodePublisher.PublishEvent(new InternalCommandScheduled
            {
                Command = new SetNewTerm
                {
                    Term = voteRequest.Term
                }
            }).Wait();

            return null;
        }

        public AppendEntriesResponse AppendEntries(AppendEntriesRequest entriesRequest)
        {
            // If the node term is greater, return before updating timer. Eventually an election will trigger.
            if (_node.Properties.CurrentTerm > entriesRequest.Term)
            {
                _logger.Warning(
                    "The leaderNode's(id = {leaderId}) term({term}) is less than current term({currentTerm}). " +
                    "As a result, this AppendEntries call has not reset the current nodes timer.",
                    entriesRequest.LeaderId, entriesRequest.Term, _node.Properties.CurrentTerm);

                return AppendEntriesResponse(false);
            }

            _timer.ResetTimer();

            if (_node.Properties.CurrentTerm < entriesRequest.Term)
            {
                _nodePublisher.PublishEvent(new InternalCommandScheduled
                {
                    Command = new SetNewTerm
                    {
                        Term = entriesRequest.Term
                    }
                }).Wait();
            }

            if (_node.CurrentState == NodeState.Candidate)
            {
                _nodePublisher.PublishEvent(new InternalCommandScheduled
                {
                    Command = new CancelElection()
                }).Wait();
            }

            if (_node.CurrentState != NodeState.Follower)
                throw new FaultException<MultipleLeadersForTermFault>(
                    new MultipleLeadersForTermFault
                    {
                        Id = _node.Properties.NodeId
                    });

            var previousLogEntriesTerm = _node.Log.GetTermForEntry(entriesRequest.PreviousLogIndex);
            if (previousLogEntriesTerm != entriesRequest.PreviousLogTerm)
            {
                _logger.Information("Log matching failed. Expected term for entry at {rpcIndex} to be {rpcTerm}. " +
                                    "However, this nodes log had the term set as {nodeTerm}.",
                                    entriesRequest.PreviousLogIndex, entriesRequest.PreviousLogTerm, previousLogEntriesTerm);

                return AppendEntriesResponse(false);
            }

            _nodePublisher.PublishEvent(
                new InternalCommandScheduled {
                    Command = new SetLeaderInformation
                    {
                        LeaderId = entriesRequest.LeaderId
                    }
                });

            _appendEntriesPublisher.PublishEvent(new AppendEntriesRequested
            {
                PreviousLogIndex = entriesRequest.PreviousLogIndex,
                PreviousLogTerm = entriesRequest.PreviousLogTerm,
                LeaderCommit = entriesRequest.LeaderCommit,
                Entries = entriesRequest.Entries
            }).Wait();

            return AppendEntriesResponse(true);
        }

        private AppendEntriesResponse AppendEntriesResponse(bool success)
        {
            return new AppendEntriesResponse
            {
                Term = _node.Properties.CurrentTerm,
                Success = success
            };
        }
    }
}
