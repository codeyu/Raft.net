﻿using System.Linq;

namespace Raft.Server.Handlers
{
    /// <summary>
    /// 1 of 4 EventHandlers for scheduled state machine commands.
    /// Order of execution:
    ///     NodeStateValidator
    ///     LogEncoder
    ///     LogReplicator*
    ///     LogWriter
    /// </summary>
    internal class LogReplicator : CommandScheduledEventHandler
    {
        public override void Handle(CommandScheduledEvent data)
        {
        // TODO
        }
    }
}