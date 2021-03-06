﻿using System;
using Raft.Server.BufferEvents;
using Raft.Server.Data;

namespace Raft.Tests.Unit.TestData.Commands
{
    internal static class TestEventFactory
    {
        public static CommandScheduled GetCommandEvent()
        {
            return new CommandScheduled
            {
                Command = new TestCommand()
            }.Translate(new CommandScheduled(), 1L);
        }

        public static CommandScheduled GetCommandEvent(long logIdx, byte[] data)
        {
            var @event = new CommandScheduled
            {
                Command = new TestCommand()
            }.Translate(new CommandScheduled(), 1L);

            @event.SetLogEntry(new LogEntry { Index = logIdx }, data);

            return @event;
        }

        public static CommandScheduled GetCommandEvent(long logIdx, byte[] data, Action executeAction)
        {
            var @event = new CommandScheduled
            {
                Command = new TestExecutableCommand(executeAction)
            }.Translate(new CommandScheduled(), 1L);

            @event.SetLogEntry(new LogEntry { Index = logIdx }, data);

            return @event;
        }
    }
}
