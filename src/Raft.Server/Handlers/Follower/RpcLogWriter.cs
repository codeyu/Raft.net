﻿using System.IO;
using Disruptor;
using ProtoBuf;
using Raft.Core;
using Raft.Infrastructure.Journaler;
using Raft.Server.Events;
using Raft.Server.Log;
using Raft.Server.Registers;

namespace Raft.Server.Handlers.Follower
{
    internal class RpcLogWriter : IEventHandler<CommitCommandRequested>
    {
        private readonly IJournal _journal;
        private readonly IRaftNode _raftNode;
        private readonly CommandRegister _commandRegister;

        public RpcLogWriter(IJournal journal, IRaftNode raftNode, CommandRegister commandRegister)
        {
            _journal = journal;
            _raftNode = raftNode;
            _commandRegister = commandRegister;
        }

        public void OnNext(CommitCommandRequested data, long sequence, bool endOfBatch)
        {
            try
            {
                LogEntry decodedEntry;
                using (var stream = new MemoryStream(data.Entry, false))
                {
                    decodedEntry = Serializer.DeserializeWithLengthPrefix<LogEntry>(stream, PrefixStyle.Base128);
                }

                // TODO: Generate checksum and compare?.
                _journal.WriteBlock(data.Entry);
                _raftNode.CommitLogEntry(decodedEntry.Index); // TODO: ADD Term....
                _commandRegister.Add(decodedEntry.Term, decodedEntry.Index, decodedEntry.Command);
            }
            catch
            {
                //  TODO: Add logging and log error
                // _log.Error("Failed to apply log: {logIndex} for term: {term}")
            }
        }
    }
}
